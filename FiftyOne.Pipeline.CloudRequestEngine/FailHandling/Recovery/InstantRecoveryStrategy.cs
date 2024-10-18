using FiftyOne.Pipeline.CloudRequestEngine.FailHandling.ExceptionCaching;

namespace FiftyOne.Pipeline.CloudRequestEngine.FailHandling.Recovery
{
    /// <summary>
    /// Always allows to make new server call
    /// regardless of previous failures.
    /// </summary>
    public class InstantRecoveryStrategy : IRecoveryStrategy
    {
        /// <summary>
        /// Called when querying the server failed.
        /// </summary>
        /// <param name="cachedException">
        /// Timestampted exception.
        /// </param>
        public void RecordFailure(CachedException cachedException) { /* nop */ }

        /// <summary>
        /// Whether the new request may be sent already.
        /// </summary>
        /// <returns>
        /// true -- send, false -- skip
        /// </returns>
        /// <param name="cachedException">
        /// Timestampted exception that prevents new requests.
        /// </param>>
        public bool MayTryNow(out CachedException cachedException)
        {
            cachedException = null;
            return true;
        }

        /// <summary>
        /// Called once the request succeeds (after recovery).
        /// </summary>
        public void Reset() { /* nop */ }
    }
}
