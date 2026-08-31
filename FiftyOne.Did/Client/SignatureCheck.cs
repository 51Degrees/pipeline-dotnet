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

namespace FiftyOne.Did.Client
{
    /// <summary>
    /// The outcome of an offline signature check by
    /// <see cref="DidClient.VerifySignatureDetailedAsync"/>. Only
    /// <see cref="Verified"/> means the identifier is genuine. The other
    /// values say why the check did not pass, for diagnosis.
    /// </summary>
    public enum SignatureCheck
    {
        /// <summary>
        /// The signature verifies under a signing key in force at the
        /// identifier's creation time.
        /// </summary>
        Verified,

        /// <summary>
        /// The signature does not verify under any key in force at the
        /// identifier's creation time.
        /// </summary>
        Invalid,

        /// <summary>
        /// No published signing key covers the identifier's creation time,
        /// which happens when the date precedes the whole schedule.
        /// </summary>
        NoKeyForDate,

        /// <summary>The envelope is not OWID version 3.</summary>
        UnsupportedVersion,

        /// <summary>
        /// The payload is shorter than the base length for the identifier
        /// type.
        /// </summary>
        InvalidLength,
    }
}
