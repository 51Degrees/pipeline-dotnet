using FiftyOne.Pipeline.CloudRequestEngine.FailHandling.Scope;
using System;
using System.Collections.Generic;
using System.Text;

namespace FiftyOne.Pipeline.CloudRequestEngine.FailHandling.Facade
{
    /// <summary>
    /// Tracks failures and throttles requests.
    /// </summary>
    public interface IFailHandler
    {
        /// <summary>
        /// Throws if the strategy indicates that
        /// requests may not be sent now.
        /// </summary>
        /// <exception cref="CloudRequestEngineTemporarilyUnavailableException">
        /// </exception>
        void ThrowIfStillRecovering();

        /// <summary>
        /// Lets a consumer to wrap an attempt in `using` scope
        /// to implicitly report success 
        /// or explicitly provide exception on failure.
        /// </summary>
        /// <returns>
        /// Attempt scope that report to this handler once disposed.
        /// </returns>
        IAttemptScope MakeAttemptScope();
    }
}
