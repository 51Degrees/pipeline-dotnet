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
    /// entry starts, which is <see cref="EndsAt"/> where the service gives
    /// it, so the entry whose start is latest on or before an identifier's
    /// creation time is the one that signed it, unless it had ended by
    /// then.
    /// </summary>
    public sealed class DidPublicKey
    {
        /// <summary>
        /// Creates an entry whose end is not known.
        /// </summary>
        /// <param name="startsAt">When the key comes into force, UTC.</param>
        /// <param name="publicKeyPem">The public key as SPKI PEM.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="publicKeyPem"/> is null.
        /// </exception>
        public DidPublicKey(DateTime startsAt, string publicKeyPem)
            : this(startsAt, publicKeyPem, null)
        {
        }

        /// <summary>
        /// Creates an entry.
        /// </summary>
        /// <param name="startsAt">When the key comes into force, UTC.</param>
        /// <param name="publicKeyPem">The public key as SPKI PEM.</param>
        /// <param name="endsAt">
        /// When the key is scheduled to stop being in force, UTC, or null
        /// where that is not known.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="publicKeyPem"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="endsAt"/> is not after
        /// <paramref name="startsAt"/>.
        /// </exception>
        public DidPublicKey(
            DateTime startsAt,
            string publicKeyPem,
            DateTime? endsAt)
        {
            if (endsAt <= startsAt)
            {
                throw new ArgumentException(
                    "A key must end after it starts.", nameof(endsAt));
            }
            StartsAt = startsAt;
            PublicKeyPem = publicKeyPem
                ?? throw new ArgumentNullException(nameof(publicKeyPem));
            EndsAt = endsAt;
        }

        /// <summary>
        /// When the key comes into force, UTC. An entry may start in the
        /// future, because a key can be published before its start.
        /// </summary>
        public DateTime StartsAt { get; }

        /// <summary>
        /// When the key is scheduled to stop being in force, UTC, being the
        /// next key's start, or null where the service does not say. The
        /// newest entry carries it although the next key is not published
        /// yet. A key may be replaced before this moment, and the service
        /// then moves it earlier to the replacement's start.
        /// </summary>
        public DateTime? EndsAt { get; }

        /// <summary>
        /// The public key in SPKI PEM form, as accepted by
        /// <c>ECDsa.ImportFromPem</c>.
        /// </summary>
        public string PublicKeyPem { get; }
    }
}
