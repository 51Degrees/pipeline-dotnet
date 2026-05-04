/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2026 51 Degrees Mobile Experts Limited, Davidson House,
 * Forbury Square, Reading, Berkshire, United Kingdom RG1 3EU.
 *
 * This Original Work is licensed under the European Union Public Licence
 * (EUPL) v.1.2 and is subject to its terms as set out below.
 *
 * If a copy of the EUPL was not distributed with this file, You can obtain
 * one at https://opensource.org/licenses/EUPL-1.2.
 *
 * The 'Compatible Licences' set out in the Appendix to the EUPL (as may be
 * amended by the European Commission) shall be deemed incompatible for
 * the purposes of the Work and the provisions of the compatibility
 * clause in Article 5 of the EUPL shall not apply.
 *
 * If using the Work as, or as part of, a network application, by
 * including the attribution notice(s) required under Article 5 of the EUPL
 * in the end user terms of the application under an appropriate heading,
 * such notice(s) shall fulfill the requirements of that article.
 * ********************************************************************* */

using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FailHandling.ExceptionCaching;
using FiftyOne.Pipeline.Core.FailHandling.Recovery;
using FiftyOne.Pipeline.Core.FailHandling.Scope;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace FiftyOne.Pipeline.Core.FailHandling.Facade
{
    /// <summary>
    /// Tracks failures and throttles requests.
    /// Wraps <see cref="IRecoveryStrategy"/>.
    /// </summary>
    public class WindowedFailHandler: IFailHandler
    {
        private readonly IRecoveryStrategy _recoveryStrategy;
        private readonly int _failuresToEnterRecovery;
        private readonly TimeSpan _failuresWindow;
        private readonly ILogger _logger;
        private readonly string _messagePrefix;
        private readonly State _logMask;

        /// <summary>
        /// Only to be filled in
        /// <see cref="State.Active"/>
        /// or
        /// <see cref="State.WaitingForReset"/>.
        /// 
        /// Once reaches max size
        /// with all entries within last
        /// <see cref="_failuresWindow"/>
        /// should be cleared,
        /// and last entry forwarded to
        /// <see cref="_recoveryStrategy"/>'s
        /// <see cref="IRecoveryStrategy.RecordFailure(CachedException)"/>
        /// .
        /// </summary>
        private readonly Queue<CachedException> _failures;
        private UInt64 _requestsInProgress = 0;

        /// <summary>
        /// Internal state.
        /// </summary>
        [Flags]
        public enum State
        {
            /// <summary>
            /// Last strategy's decision was 'allow'.
            /// Queue is yet to be filled with valid errors.
            /// </summary>
            NoLogs,

            /// <summary>
            /// Last strategy's decision was 'allow'.
            /// Queue is yet to be filled with valid errors.
            /// </summary>
            Active,

            /// <summary>
            /// Last strategy's decision was 'allow'.
            /// Queue is filled and a signal to the strategy was sent.
            /// </summary>
            QueueFilled,

            /// <summary>
            /// Last strategy's decision was 'deny'.
            /// Wait for active requests to die off.
            /// </summary>
            ShuttingDown,

            /// <summary>
            /// Last strategy's decision was 'deny'.
            /// Wait for it to return 'allow'.
            /// </summary>
            WaitingForGreenLight,

            /// <summary>
            /// Last strategy's decision was 'allow' (after recovery).
            /// If some request succeeds -- reset the strategy.
            /// </summary>
            WaitingForReset,
        }
        private State _state = State.Active;

        /// <summary>
        /// Protects
        /// <see cref="_failures"/>,
        /// <see cref="_requestsInProgress"/>
        /// and
        /// <see cref="_state"/>.
        /// Does NOT protect
        /// <see cref="_recoveryStrategy"/>.
        /// </summary>
        private readonly object _lock = new object();

        /// <summary>
        /// Designated constructor.
        /// </summary>
        /// <param name="recoveryStrategy">
        /// Strategy to wrap.
        /// </param>
        /// <param name="failuresToEnterRecovery">
        /// How many failures should occur within
        /// <paramref name="failuresWindow"/>
        /// to pass the failure to
        /// <paramref name="recoveryStrategy"/>.
        /// </param>
        /// <param name="failuresWindow">
        /// How long should it take for errors
        /// to accumulate over
        /// <paramref name="failuresToEnterRecovery"/>
        /// before the event is passed to
        /// <paramref name="recoveryStrategy"/>.
        /// </param>
        /// <param name="logger">
        /// Logger to log state changes into
        /// </param>
        /// <param name="messagePrefix">
        /// Prefix for messages logged into <paramref name="logger"/>
        /// </param>
        /// <param name="logMask">
        /// What events to log into <paramref name="logger"/>
        /// </param>
        public WindowedFailHandler(
            IRecoveryStrategy recoveryStrategy,
            int failuresToEnterRecovery,
            TimeSpan failuresWindow,
            ILogger logger,
            string messagePrefix,
            State? logMask = null)
        {
            if ((_recoveryStrategy = recoveryStrategy) is null)
            {
                throw new ArgumentNullException(nameof(recoveryStrategy));
            }
            if ((_failuresToEnterRecovery = failuresToEnterRecovery) <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(failuresToEnterRecovery),
                    $"{nameof(failuresToEnterRecovery)} should be positive.");
            }
            if ((_failuresWindow = failuresWindow) <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(failuresWindow),
                    $"{nameof(failuresWindow)} should be positive.");
            }
            _failures = new Queue<CachedException>(failuresToEnterRecovery);
            _logger = logger;
            _messagePrefix = messagePrefix;
            _logMask = logMask ?? (
                State.Active
                | State.QueueFilled
                | State.ShuttingDown
                | State.WaitingForGreenLight
                | State.WaitingForReset);
            if (!(logger is null) && (messagePrefix is null))
            {
                throw new ArgumentException(
                    nameof(messagePrefix),
                    $"{nameof(messagePrefix)} must be provided when {nameof(logger)} is.");
            }
        }

        /// <inheritdoc cref="IFailHandler.CheckIfRecovered(Func{string, Exception, Exception})"/>
        public bool CheckIfRecovered(Func<string, Exception, Exception> exceptionFactory)
        {
            if (!_recoveryStrategy.MayTryNow(out var cachedException, out var suspensionStatus))
            {
                lock (_lock)
                {
                    // strategy decided to suppress further requests
                    switch (_state)
                    {
                        case State.ShuttingDown:
                            // nop -- continue shutting down.
                            break;

                        case State.WaitingForGreenLight:
                            // nop -- continue waiting for green light.
                            break;

                        case State.WaitingForReset:
                            // strategy disallowed a request 
                            // after allowing the previous one
                            // before that one produced result (?)
                            //
                            // probably a race condition.
                            //
                            // ignore the reset expectation.
                            goto case State.Active;

                        case State.Active:
                            // strategy disallowed a request 
                            // after allowing the previous one
                            // while the queue hasn't filled up (?)
                            //
                            // probably a race condition.
                            //
                            // ignore the not-yet-full queue.
                            goto case State.QueueFilled;

                        case State.QueueFilled:
                            if (_requestsInProgress > 0)
                            {
                                // wait for active requests to die out
                                // before adding new errors to the queue.
                                _state = State.ShuttingDown;
                                if ((_state & _logMask) != State.NoLogs)
                                {
                                    _logger?.LogInformation("{messagePrefix} is entering suspension -- {suspensionStatus}", _messagePrefix, suspensionStatus?.Invoke());
                                }
                            } else
                            {
                                // queue is immediately ready for new items.
                                _state = State.WaitingForGreenLight;
                                if ((_state & _logMask) != State.NoLogs)
                                {
                                    _logger?.LogInformation("{messagePrefix} is waiting for green light -- {suspensionStatus}", _messagePrefix, suspensionStatus?.Invoke());
                                }
                            }
                            break;
                    }
                }
                var ex = exceptionFactory?.Invoke(
                    $"Recovered exception from {(DateTime.Now - cachedException.DateTime).TotalSeconds}s ago.",
                    cachedException.Exception);
                if (!(ex is null))
                {
                    throw ex;
                }
                return false;
            }
            lock (_lock)
            {
                // strategy decided to allow new request
                switch (_state)
                {
                    case State.Active:
                        // nop -- already allowing the requests.
                        break;

                    case State.WaitingForReset:
                        // nop -- already re-trying
                        break;

                    case State.QueueFilled:
                        // strategy either ignored the full queue (?)
                        // or did not yet receive the signal
                        // due to race condition.
                        //
                        // nop -- keep allowing until further notice.
                        break;

                    case State.ShuttingDown:
                    case State.WaitingForGreenLight:
                        // reset strategy on next success
                        _state = State.WaitingForReset;
                        if ((_state & _logMask) != State.NoLogs)
                        {
                            _logger?.LogInformation("{messagePrefix} is reactivating", _messagePrefix);
                        }
                        break;
                }
            }
            return true;
        }

        /// <summary>
        /// Lets a consumer to wrap an attempt in `using` scope
        /// to implicitly report success 
        /// or explicitly provide exception on failure.
        /// </summary>
        /// <returns>
        /// Attempt scope that report to this handler once disposed.
        /// </returns>
        public IAttemptScope MakeAttemptScope()
        {
            lock (_lock)
            {
                ++_requestsInProgress;
            }
            return new AttemptScope(AttemptFinished);
        }

        private void AttemptFinished(CachedException cachedException)
        {
            bool shouldReset = false;
            CachedException exToRecord = null;
            lock (_lock)
            {
                if (--_requestsInProgress < 0)
                {
                    Debug.Fail($"Got into negative {_requestsInProgress}");
                    _requestsInProgress = 0;
                }
                if (cachedException is null)
                {
                    // no error
                    switch (_state)
                    {
                        case State.WaitingForReset:
                            // awaited success arrived
                            _state = State.Active;
                            shouldReset = true;
                            if ((_state & _logMask) != State.NoLogs)
                            {
                                _logger?.LogInformation("{messagePrefix} is reactivated", _messagePrefix);
                            }
                            break;

                        case State.QueueFilled:
                        case State.Active:
                            // nop -- no change needed.
                            break;

                        case State.ShuttingDown:
                            // nop -- continue shutting down.
                            break;

                        case State.WaitingForGreenLight:
                            // nop -- ignore success,
                            // continue waiting for green light.
                            break;
                    }
                }
                else
                {
                    // did fail the attempt.
                    switch (_state)
                    {
                        case State.WaitingForReset:
                        case State.Active:
                            while (_failures.Count >= _failuresToEnterRecovery)
                            {
                                Debug.Fail("Somehow reached the size limit");
                                _ = _failures.Dequeue();
                            }
                            exToRecord = UpdateFailures(cachedException);
                            // state is not updated here.
                            // wait for strategy
                            // to disallow some requests first.
                            break;

                        case State.QueueFilled:
                            // nop -- already sent a signal to the strategy.
                            break;

                        case State.ShuttingDown:
                            if (_requestsInProgress == 0)
                            {
                                // desired state reached.
                                _state = State.WaitingForGreenLight;
                                if ((_state & _logMask) != State.NoLogs)
                                {
                                    _logger?.LogInformation("{messagePrefix} is waiting for green light", _messagePrefix);
                                }
                            }
                            break;

                        case State.WaitingForGreenLight:
                            // nop -- continue waiting for green light.
                            break;
                    }
                }
            }
            if (exToRecord is null) {
                // no error to report to strategy
                if (shouldReset)
                {
                    _recoveryStrategy.Reset();
                }
                return;
            }
            _recoveryStrategy.RecordFailure(exToRecord);
        }

        /// <summary>
        /// Only to be called from locked context.
        /// Removes outdated entries and adds the new one.
        /// </summary>
        /// <param name="newFailure">
        /// The most recent failure.
        /// </param>
        /// <returns>
        /// The failure, if the queue reached the required length.
        /// </returns>
        private CachedException UpdateFailures(CachedException newFailure)
        {
            DateTime tooLongAgo = DateTime.Now - _failuresWindow;
            while (_failures.Count > 0)
            {
                var entry = _failures.Peek();
                if (entry.DateTime < tooLongAgo)
                {
                    _ = _failures.Dequeue();
                } 
                else
                {
                    break;
                }
            }
            _failures.Enqueue(newFailure);
            if (_failures.Count == _failuresToEnterRecovery)
            {
                // conditions met.
                // will report outside the lock.
                _failures.Clear();
                return newFailure;
            }
            return null;
        }
    }
}
