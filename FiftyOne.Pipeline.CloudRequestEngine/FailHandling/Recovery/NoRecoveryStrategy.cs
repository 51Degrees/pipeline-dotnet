using FiftyOne.Pipeline.CloudRequestEngine.FailHandling.ExceptionCaching;

namespace FiftyOne.Pipeline.CloudRequestEngine.FailHandling.Recovery
{
    /// <summary>
    /// Drops all server calls after first failure.
    /// </summary>
    public class NoRecoveryStrategy : IRecoveryStrategy
    {
        private volatile CachedException _cachedException = null;

        /// <summary>
        /// Called when querying the server failed.
        /// </summary>
        /// <param name="cachedException">
        /// Timestampted exception.
        /// </param>
        public void RecordFailure(CachedException cachedException)
        {
            _cachedException = cachedException;
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
            // volatile read, can’t be reordered with subsequent operations
            cachedException = _cachedException;
            return cachedException is null;
        }

        /// <summary>
        /// Called once the request succeeds (after recovery).
        /// </summary>
        public void Reset() => _cachedException = null;
    }
}
