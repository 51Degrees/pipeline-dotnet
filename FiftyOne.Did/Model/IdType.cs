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

namespace FiftyOne.Did.Model
{
    /// <summary>
    /// The identifier type carried in bits 6-7 of the 51Did flags byte.
    /// Existing identifiers were issued with these bits zeroed, so they
    /// decode as <see cref="Probabilistic"/>.
    /// </summary>
    public enum IdType : byte
    {
        /// <summary>
        /// Derived from the device fingerprint and IP address.
        /// Payload carries a 32-byte SHA-256 value.
        /// </summary>
        Probabilistic = 0,

        /// <summary>
        /// A server-generated random GUID. Payload carries 16 GUID bytes.
        /// </summary>
        Random = 1,

        /// <summary>
        /// Derived from the caller-supplied email and salt.
        /// Payload carries a 32-byte SHA-256 value.
        /// </summary>
        HashedEmail = 2,

        /// <summary>
        /// Not yet assigned. Parsed best-effort: the header fields are
        /// unpacked and the remaining payload bytes are exposed as-is.
        /// </summary>
        Reserved = 3,
    }
}
