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
