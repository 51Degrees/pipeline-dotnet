using System;
using System.Collections.Generic;
using System.Text;

namespace FiftyOne.Pipeline.CloudRequestEngine.FailHandling.Scope
{
    /// <summary>
    /// A scope within which an attempt will be made.
    /// Call <see cref="RecordFailure(Exception)"/>
    /// to indicate the failure and cache the reason.
    /// </summary>
    public interface IAttemptScope: IDisposable
    {
        /// <summary>
        /// Signals that attempt failed.
        /// </summary>
        /// <param name="exception">
        /// The cause of failure.
        /// </param>
        void RecordFailure(Exception exception);
    }
}
