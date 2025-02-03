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
using FiftyOne.Pipeline.Core.Data;
using System;

namespace FiftyOne.Pipeline.Engines
{
    /// <summary>
    /// Exception thrown when a user requests a property that is not present
    /// in the <see cref="IFlowData"/>.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/exception-handling.md#property-missing-exception">Specification</see>
    /// </summary>
    public class PropertyMissingException : Exception
    {
        /// <summary>
        /// The reason the property is not present
        /// </summary>
        public MissingPropertyReason Reason { get; set; }
        /// <summary>
        /// The name of the property that is not present
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public PropertyMissingException() : base() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">
        /// The exception message
        /// </param>
        public PropertyMissingException(string message) : base(message) { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">
        /// The exception message
        /// </param>
        /// <param name="innerException">
        /// The inner exception that triggered this exception.
        /// </param>
        public PropertyMissingException(string message, 
            Exception innerException) 
            : base(message, innerException) { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="reason">
        /// The reason the property is not present
        /// </param>
        /// <param name="propertyName">
        /// The name of the property that is not present
        /// </param>
        /// <param name="message">
        /// The exception message
        /// </param>
        public PropertyMissingException(
            MissingPropertyReason reason, 
            string propertyName,
            string message) 
            : base(message)
        {
            Reason = reason;
            PropertyName = propertyName;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="reason">
        /// The reason the property is not present
        /// </param>
        /// <param name="propertyName">
        /// The name of the property that is not present
        /// </param>
        /// <param name="message">
        /// The exception message
        /// </param>
        /// <param name="innerException">
        /// The inner exception that triggered this exception.
        /// </param>
        public PropertyMissingException(
            MissingPropertyReason reason,
            string propertyName,
            string message,
            Exception innerException) :
            base(message, innerException)
        {
            Reason = reason;
            PropertyName = propertyName;
        }
        
    }

}
