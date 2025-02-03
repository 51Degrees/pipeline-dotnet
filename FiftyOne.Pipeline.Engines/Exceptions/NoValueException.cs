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

using System;
using FiftyOne.Pipeline.Engines.Data;

namespace FiftyOne.Pipeline.Engines.Exceptions
{
    /// <summary>
    /// Exception that can be thrown when an <see cref="AspectPropertyValue{T}"/>
    /// does not have a value.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/properties.md#null-values">Specification</see>
    /// </summary>
    public class NoValueException : InvalidOperationException
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public NoValueException() : base() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">
        /// The exception message
        /// </param>
        public NoValueException(
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
        public NoValueException(
            string message,
            Exception innerException) :
            base(message, innerException)
        { }
    }
}
