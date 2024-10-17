using FiftyOne.Pipeline.CloudRequestEngine.FailHandling.ExceptionCaching;

namespace FiftyOne.Pipeline.CloudRequestEngine.FailHandling.Recovery
{
    /// <summary>
    /// Controls when to suspend requests
    /// due to recent query failures.
    /// </summary>
    public interface IRecoveryStrategy
    {
        /// <summary>
        /// Called when querying the server failed.
        /// </summary>
        /// <param name="cachedException">
        /// Timestampted exception.
        /// </param>
        void RecordFailure(CachedException cachedException);

        /// <summary>
        /// Whether the new request may be sent already.
        /// </summary>
        /// <returns>true -- send, false -- skip</returns>
        /// <param name="cachedException">
        /// Timestampted exception that prevents new requests.
        /// </param>
        bool MayTryNow(out CachedException cachedException);

        /// <summary>
        /// Called once the request succeeds (after recovery).
        /// </summary>
        void Reset();
    }
}
