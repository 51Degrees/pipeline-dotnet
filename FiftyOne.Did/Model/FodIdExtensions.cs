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

using System;

namespace FiftyOne.Did.Model
{
    /// <summary>
    /// Extension methods for resolving a 51Did value into a <see cref="FodId"/>
    /// in a single call.
    /// </summary>
    /// <remarks>
    /// The 51Did cloud engine surfaces each identifier as a raw base64 string.
    /// This overload turns that string into a parsed <see cref="FodId"/> so a
    /// consumer can write, for example, <c>idString.As51Did()</c>.
    /// <para>
    /// Error handling is delegated to the existing types rather than reinvented:
    /// a value that is not a valid 51Did surfaces the <see cref="FodId"/>
    /// constructor's own exceptions. See the per-method remarks for the exact
    /// types.
    /// </para>
    /// <para>
    /// Note this type intentionally takes no dependency on the pipeline: a
    /// caller holding an <c>IAspectPropertyValue&lt;string&gt;</c> already
    /// references the engines assembly and can unwrap it with its own no-value
    /// handling, e.g. <c>data.IdProbGlobal.Value.As51Did()</c>.
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
    }
}
