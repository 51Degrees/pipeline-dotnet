/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2023 51 Degrees Mobile Experts Limited, Davidson House,
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

using FiftyOne.Pipeline.Core.FailHandling.ExceptionCaching;
using System;

namespace FiftyOne.Pipeline.Core.FailHandling.Recovery
{
    /// <summary>
    /// Implements exponential backoff where delay doubles after each consecutive failure.
    /// First failure: wait initialDelay seconds
    /// Second failure: wait initialDelay * multiplier seconds
    /// Third failure: wait initialDelay * multiplier^2 seconds
    /// And so on, up to maxDelaySeconds.
    /// </summary>
    public class ExponentialBackoffRecoveryStrategy : IRecoveryStrategy
    {
        /// <summary>
        /// Initial delay in seconds for the first failure.
        /// </summary>
        public readonly double InitialDelaySeconds;

        /// <summary>
        /// Maximum delay in seconds to cap the exponential growth.
        /// </summary>
        public readonly double MaxDelaySeconds;

        /// <summary>
        /// Multiplier for exponential backoff (typically 2.0 for doubling).
        /// </summary>
        public readonly double Multiplier;

        /// <summary>
        /// Current delay in seconds based on consecutive failures.
        /// </summary>
        public double CurrentDelaySeconds { get; private set; }

        private CachedException _exception = null;
        private DateTime _recoveryDateTime = DateTime.MinValue;
        private int _consecutiveFailures = 0;
        private readonly object _lock = new object();

        /// <summary>
        /// Constructor with default exponential backoff parameters.
        /// </summary>
        /// <param name="initialDelaySeconds">
        /// Initial delay in seconds (default: INITIAL_DELAY_SECONDS_DEFAULT).
        /// </param>
        /// <param name="maxDelaySeconds">
        /// Maximum delay in seconds to cap growth (default: MAX_DELAY_SECONDS_DEFAULT).
        /// </param>
        /// <param name="multiplier">
        /// Exponential multiplier (default: MULTIPLIER_DEFAULT for doubling).
        /// </param>
        public ExponentialBackoffRecoveryStrategy(
            double initialDelaySeconds, 
            double maxDelaySeconds, 
            double multiplier)
        {
            if (initialDelaySeconds <= 0)
                throw new ArgumentException("Initial delay must be positive", nameof(initialDelaySeconds));
            if (maxDelaySeconds <= 0)
                throw new ArgumentException("Max delay must be positive", nameof(maxDelaySeconds));
            if (multiplier <= 1.0)
                throw new ArgumentException("Multiplier must be greater than 1.0", nameof(multiplier));

            InitialDelaySeconds = initialDelaySeconds;
            MaxDelaySeconds = maxDelaySeconds;
            Multiplier = multiplier;
            CurrentDelaySeconds = initialDelaySeconds;
        }

        /// <summary>
        /// Called when querying the server failed.
        /// Calculates the next delay using exponential backoff.
        /// </summary>
        /// <param name="cachedException">
        /// Timestamped exception.
        /// </param>
        public void RecordFailure(CachedException cachedException)
        {
            lock (_lock)
            {
                // Only increment failure count if this is a new failure, not a concurrent
                // recording of the same failure event. We consider it a new failure if:
                // 1. This is the first failure (_exception is null), or
                // 2. The new failure occurred after the current recovery time would have ended
                bool isNewFailure = _exception == null || 
                                   cachedException.DateTime >= _recoveryDateTime;

                if (isNewFailure)
                {
                    _consecutiveFailures++;
                }
                
                // Calculate new delay: initialDelay * multiplier^(failures-1)
                // For failures=1: initialDelay * multiplier^0 = initialDelay
                // For failures=2: initialDelay * multiplier^1 = initialDelay * multiplier
                // For failures=3: initialDelay * multiplier^2, etc.
                CurrentDelaySeconds = Math.Min(
                    InitialDelaySeconds * Math.Pow(Multiplier, _consecutiveFailures - 1),
                    MaxDelaySeconds);

                var newRecoveryTime = cachedException.DateTime.AddSeconds(CurrentDelaySeconds);
                
                _exception = cachedException;
                _recoveryDateTime = newRecoveryTime;
            }
        }

        /// <summary>
        /// Whether the new request may be sent already.
        /// </summary>
        /// <param name="cachedException">
        /// Timestamped exception that prevents new requests.
        /// </param>
        /// <returns>true -- send, false -- skip</returns>
        public bool MayTryNow(out CachedException cachedException)
        {
            DateTime recoveryDateTime;
            CachedException lastCachedException;
            
            lock (_lock)
            {
                recoveryDateTime = _recoveryDateTime;
                lastCachedException = _exception;
            }

            if (recoveryDateTime < DateTime.Now)
            {
                cachedException = null;
                return true;
            }
            else
            {
                cachedException = lastCachedException;
                return false;
            }
        }

        /// <summary>
        /// Called once the request succeeds (after recovery).
        /// Resets consecutive failures and delay back to initial value.
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _consecutiveFailures = 0;
                CurrentDelaySeconds = InitialDelaySeconds;
                _exception = null;
                _recoveryDateTime = DateTime.MinValue;
            }
        }
    }
}