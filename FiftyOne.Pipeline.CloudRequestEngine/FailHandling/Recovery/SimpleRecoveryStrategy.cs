using FiftyOne.Pipeline.CloudRequestEngine.FailHandling.ExceptionCaching;
using System;

namespace FiftyOne.Pipeline.CloudRequestEngine.FailHandling.Recovery
{
    /// <summary>
    /// Disallows calling the server for
    /// <see cref="RecoverySeconds"/>.
    /// </summary>
    public class SimpleRecoveryStrategy : IRecoveryStrategy
    {
        /// <summary>
        /// For how long to disallow server calls after failure.
        /// </summary>
        public readonly double RecoverySeconds;

        private CachedException _exception = null;
        private DateTime _recoveryDateTime = DateTime.MinValue;
        private readonly object _lock = new object();

        /// <summary>
        /// Designated constructor.
        /// </summary>
        /// <param name="recoverySeconds">
        /// For how long to disallow server calls after failure.
        /// </param>
        public SimpleRecoveryStrategy(double recoverySeconds)
        {
            RecoverySeconds = recoverySeconds;
        }

        /// <summary>
        /// Called when querying the server failed.
        /// </summary>
        /// <param name="cachedException">
        /// Timestampted exception.
        /// </param>
        public void RecordFailure(CachedException cachedException)
        {
            var newRecoveryTime = cachedException.DateTime.AddSeconds(RecoverySeconds);
            lock (_lock)
            {
                _exception = cachedException;
                _recoveryDateTime = newRecoveryTime;
            }
        }

        /// <summary>
        /// Whether the new request may be sent already.
        /// </summary>
        /// <returns>true -- send, false -- skip</returns>
        /// <param name="cachedException">
        /// Timestampted exception that prevents new requests.
        /// </param>
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
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _exception = null;
                _recoveryDateTime = DateTime.MinValue;
            }
        }
    }
}
