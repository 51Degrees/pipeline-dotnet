/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2026 51 Degrees Mobile Experts Limited, Davidson House,
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

using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.Exceptions;
using System;

namespace FiftyOne.Did.Model
{
    /// <summary>
    /// Extension methods for resolving a 51Did value into a <see cref="FodId"/>
    /// in a single call.
    /// </summary>
    /// <remarks>
    /// The 51Did cloud engine surfaces each identifier as a raw base64 string
    /// (<c>IAspectPropertyValue&lt;string&gt;</c>). These overloads turn that
    /// value, or the unwrapped string, into a parsed <see cref="FodId"/> so a
    /// consumer can write, for example,
    /// <c>data.IdProbGlobal.As51Did()</c> or <c>idString.As51Did()</c>.
    /// <para>
    /// Error handling is delegated to the existing types rather than reinvented:
    /// a missing value surfaces the pipeline's own no-value exception (carrying
    /// the cloud's reason), and a value that is not a valid 51Did surfaces the
    /// <see cref="FodId"/> constructor's own exceptions. See the per-method
    /// remarks for the exact types.
    /// </para>
    /// </remarks>
    public static class FodIdExtensions
    {
        /// <summary>
        /// Parse a base64-encoded 51Did string into a <see cref="FodId"/>.
        /// </summary>
        /// <param name="value">
        /// The base64 OWID envelope string, as returned by the cloud service.
        /// </param>
        /// <returns>The parsed <see cref="FodId"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown (by the <see cref="FodId"/> constructor) when
        /// <paramref name="value"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown (by the <see cref="FodId"/> constructor) when
        /// <paramref name="value"/> is not valid Base64.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown (by the <see cref="FodId"/> constructor) when the decoded
        /// payload is shorter than the minimum for its identifier type.
        /// </exception>
        public static FodId As51Did(this string value)
        {
            // FodId's constructor already throws ArgumentNullException for a null
            // value, FormatException for invalid Base64 and ArgumentException for
            // a payload that is too short. Relay those rather than reinvent them.
            return new FodId(value);
        }

        /// <summary>
        /// Resolve a 51Did property value into a <see cref="FodId"/>.
        /// </summary>
        /// <param name="value">
        /// The property value produced by the 51Did engine, for example
        /// <c>data.IdProbGlobal</c>.
        /// </param>
        /// <returns>The parsed <see cref="FodId"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="value"/> itself is <c>null</c>.
        /// </exception>
        /// <exception cref="NoValueException">
        /// Thrown when the engine determined no value for this identifier
        /// (<see cref="IAspectPropertyValue.HasValue"/> is <c>false</c>), for
        /// example because the usage policy or resource key did not permit it.
        /// The exception message relays the cloud's reason
        /// (<see cref="IAspectPropertyValue.NoValueMessage"/>).
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown when the resolved value is not valid Base64.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the resolved value's payload is shorter than the minimum
        /// for its identifier type.
        /// </exception>
        public static FodId As51Did(this IAspectPropertyValue<string> value)
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!value.HasValue)
            {
                // No value was determined (for example the usage policy or
                // resource key did not permit this identifier). Surface the
                // cloud's reason explicitly rather than relying on the value
                // accessor to throw.
                throw new NoValueException(value.NoValueMessage);
            }
            // The value is present; the string overload relays the FodId
            // constructor's exceptions for a malformed value.
            return value.Value.As51Did();
        }
    }
}
