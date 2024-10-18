using FiftyOne.Pipeline.CloudRequestEngine.FailHandling.ExceptionCaching;
using FiftyOne.Pipeline.CloudRequestEngine.FailHandling.Recovery;
using FiftyOne.Pipeline.CloudRequestEngine.FailHandling.Scope;
using System;
using System.Collections.Generic;
using System.Text;

namespace FiftyOne.Pipeline.CloudRequestEngine.FailHandling.Facade
{
    /// <summary>
    /// Tracks failures and throttles requests.
    /// Wraps <see cref="IRecoveryStrategy"/>.
    /// </summary>
    public class SimpleFailHandler: IFailHandler
    {
        private readonly IRecoveryStrategy _recoveryStrategy;

        /// <summary>
        /// Designated constructor.
        /// </summary>
        /// <param name="recoveryStrategy">
        /// Strategy to wrap.
        /// </param>
        public SimpleFailHandler(IRecoveryStrategy recoveryStrategy)
        {
            _recoveryStrategy = recoveryStrategy;
        }

        /// <summary>
        /// Throws if the strategy indicates that
        /// requests may not be sent now.
        /// </summary>
        /// <exception cref="CloudRequestEngineTemporarilyUnavailableException">
        /// </exception>
        public void ThrowIfStillRecovering()
        {
            if (!_recoveryStrategy.MayTryNow(out var cachedException))
            {
                throw new Exception(
                    $"Recovered exception from {(DateTime.Now - cachedException.DateTime).TotalSeconds}s ago.", 
                    cachedException.Exception);
            }
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
            return new AttemptScope(AttemptFinished);
        }

        private void AttemptFinished(CachedException cachedException)
        {
            if (cachedException is null)
            {
                return; // ignore successful requests
            }
            _recoveryStrategy.RecordFailure(cachedException);
        }
    }
}
