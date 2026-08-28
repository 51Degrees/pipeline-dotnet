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

namespace FiftyOne.Did.Client
{
    /// <summary>
    /// One entry of the 51Did signing key schedule as the cloud publishes
    /// it. A key is in force from <see cref="StartsAt"/> until the next
    /// entry starts, so the entry whose start is latest on or before an
    /// identifier's creation time is the one that signed it.
    /// </summary>
    public sealed class DidPublicKey
    {
        /// <summary>
        /// Creates an entry.
        /// </summary>
        /// <param name="startsAt">When the key comes into force, UTC.</param>
        /// <param name="publicKeyPem">The public key as SPKI PEM.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="publicKeyPem"/> is null.
        /// </exception>
        public DidPublicKey(DateTime startsAt, string publicKeyPem)
        {
            StartsAt = startsAt;
            PublicKeyPem = publicKeyPem
                ?? throw new ArgumentNullException(nameof(publicKeyPem));
        }

        /// <summary>
        /// When the key comes into force, UTC. Keys are published up to
        /// three months ahead of their start, so an entry may be in the
        /// future.
        /// </summary>
        public DateTime StartsAt { get; }

        /// <summary>
        /// The public key in SPKI PEM form, as accepted by
        /// <c>ECDsa.ImportFromPem</c>.
        /// </summary>
        public string PublicKeyPem { get; }
    }
}
