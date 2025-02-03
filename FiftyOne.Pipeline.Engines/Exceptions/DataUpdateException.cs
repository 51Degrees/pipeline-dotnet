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

using FiftyOne.Pipeline.Engines.Services;
using System;

namespace FiftyOne.Pipeline.Engines.Exceptions
{
    /// <summary>
    /// Exception that can be thrown when a <see cref="DataUpdateService"/>
    /// fails to successfully complete an update.
    /// </summary>
    public class DataUpdateException : Exception
    {
        /// <summary>
        /// The status of the update process.
        /// </summary>
        public AutoUpdateStatus Status { get; private set; } = AutoUpdateStatus.UNSPECIFIED;

        /// <summary>
        /// Constructor
        /// </summary>
        public DataUpdateException() : base() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">
        /// The exception message
        /// </param>
        public DataUpdateException(
            string message)
            : base(message)
        { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">
        /// The exception message
        /// </param>
        /// <param name="innerException">
        /// The inner exception that triggered this exception.
        /// </param>
        public DataUpdateException(
            string message,
            Exception innerException) :
            base(message, innerException)
        { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">
        /// The exception message
        /// </param>
        /// <param name="status">
        /// The <see cref="AutoUpdateStatus"/> associated with this exception.
        /// </param>
        public DataUpdateException(
            string message,
            AutoUpdateStatus status) :
            base(message)
        {
            Status = status;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">
        /// The exception message
        /// </param>
        /// <param name="innerException">
        /// The inner exception that triggered this exception.
        /// </param>
        /// <param name="status">
        /// The <see cref="AutoUpdateStatus"/> associated with this exception.
        /// </param>
        public DataUpdateException(
            string message,
            Exception innerException,
            AutoUpdateStatus status) :
            base(message, innerException)
        {
            Status = status;
        }
    }
}
