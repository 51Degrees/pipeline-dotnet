/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2025 51 Degrees Mobile Experts Limited, Davidson House,
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
