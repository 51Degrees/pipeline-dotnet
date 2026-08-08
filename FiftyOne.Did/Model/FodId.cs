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
    /// An OWID whose payload encodes the three fields of a 51Did: a 1-byte
    /// flags bitmask (usage tier and identifier type), a 4-byte
    /// little-endian License Id, and the match key.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Terminology. A 51Did is described at three levels. The 51Did is
    /// the identifier as a whole, meaning the concept and its rules. The
    /// envelope is the data model that carries it, this OWID, holding the
    /// version, domain, date, payload and signature, re-issued fresh on
    /// every call. The match key is the part of the envelope that is stable
    /// and comparable, the payload bytes after Flags and LicenseId,
    /// exposed as <see cref="MatchKey"/>. Two responses for the same inputs
    /// share the same match key but differ at the byte level because the
    /// envelope embeds a fresh date and signature on each call. Compare
    /// match keys, never envelopes.
    /// </para>
    /// <para>
    /// Payload layout. The header (offsets 0-4) is shared by every
    /// identifier type; bits 6-7 of Flags select the type and the length
    /// of the match key that follows:
    /// </para>
    /// <list type="table">
    ///   <listheader><term>Offset</term><term>Length</term><term>Field</term></listheader>
    ///   <item><term>0</term><term>1</term><term>Flags (bits 0-2 usage, bits 6-7 type)</term></item>
    ///   <item><term>1</term><term>4</term><term>LicenseId (uint32 LE)</term></item>
    ///   <item><term>5</term><term>32</term><term>Match key: SHA-256 (Probabilistic, HashedEmail)</term></item>
    ///   <item><term>5</term><term>16</term><term>Match key: GUID (Random)</term></item>
    /// </list>
    /// <para>
    /// Inherits <see cref="Owid"/> so callers can use OWID-level features
    /// (signature verification, base64 round-tripping) directly on a
    /// <see cref="FodId"/> instance.
    /// </para>
    /// <para>
    /// This class does NOT verify the OWID signature on construction. Callers
    /// wanting cryptographic verification should call the extension methods on
    /// <see cref="Owid"/> on this instance after construction.
    /// </para>
    /// </remarks>
    public class FodId : Owid.Client.Model.Owid
    {
        /// <summary>
        /// Byte offset of the Flags field within the payload.
        /// </summary>
        public const int FlagsOffset = 0;

        /// <summary>
        /// Byte offset of the License Id field within the payload.
        /// </summary>
        public const int LicenseIdOffset = 1;

        /// <summary>
        /// Byte length of the License Id field.
        /// </summary>
        public const int LicenseIdLength = 4;

        /// <summary>
        /// Byte offset of the match key field within the payload.
        /// </summary>
        public const int HashOffset = 5;

        /// <summary>
        /// Byte length of the match key field (SHA-256).
        /// </summary>
        public const int HashLength = 32;

        /// <summary>
        /// Byte length of the payload header (Flags + LicenseId) that is
        /// common to every identifier type.
        /// </summary>
        public const int HeaderLength = HashOffset;

        /// <summary>
        /// Byte length of the GUID match key carried by Random identifiers.
        /// </summary>
        public const int GuidLength = 16;

        /// <summary>
        /// Minimum byte length of a Random 51Did payload
        /// (Flags + LicenseId + GUID).
        /// </summary>
        public const int RandomPayloadLength = HeaderLength + GuidLength;

        /// <summary>
        /// Minimum byte length of a Probabilistic or HashedEmail 51Did
        /// payload (Flags + LicenseId + MatchKey). Random payloads are
        /// shorter - see <see cref="RandomPayloadLength"/>.
        /// </summary>
        public const int PayloadLength = HashOffset + HashLength;

        /// <summary>
        /// The 1-byte usage flags bit-mask from the payload.
        /// </summary>
        public byte Flags { get; private set; }

        /// <summary>
        /// The identifier type carried in bits 6-7 of <see cref="Flags"/>.
        /// </summary>
        public IdType Type => (IdType)((Flags >> 6) & 0b11);

        /// <summary>
        /// The 4-byte little-endian License Id from the payload.
        /// </summary>
        public uint LicenseId { get; private set; }

        /// <summary>
        /// The match key from the payload, a 32-byte SHA-256 for
        /// Probabilistic and HashedEmail identifiers, or 16 GUID bytes for
        /// Random ones. This is the stable, comparable part of the
        /// envelope: two 51Dids for the same inputs share the same match key
        /// even though their envelopes (date, signature) differ on every
        /// issue. Treat it as the cache / dedup key.
        /// </summary>
        public byte[] MatchKey { get; private set; } = Array.Empty<byte>();

        /// <summary>
        /// Obsolete alias for <see cref="MatchKey"/>. The stable, comparable
        /// part of a 51Did is now called the match key, mirroring the Model
        /// Terms for Marketing vocabulary.
        /// </summary>
        [Obsolete("Renamed to MatchKey. This alias will be removed in a future release.")]
        public byte[] Hash => MatchKey;

        /// <summary>
        /// Parse a 51Did from its base64-encoded OWID string.
        /// </summary>
        /// <param name="base64">
        /// Base64 of the full OWID envelope (version + domain + date +
        /// length-prefixed payload + 64-byte signature) as produced by the
        /// 51Degrees cloud service.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="base64"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown by the underlying OWID parser when <paramref name="base64"/>
        /// is not valid Base64.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the decoded payload is shorter than the minimum
        /// for its identifier type.
        /// </exception>
        public FodId(string base64) : base(base64) => Unpack(nameof(base64));

        /// <summary>
        /// Parse a 51Did from the raw bytes of an OWID envelope.
        /// </summary>
        /// <param name="buffer">The OWID envelope bytes.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="buffer"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the payload is shorter than the minimum for its
        /// identifier type.
        /// </exception>
        public FodId(byte[] buffer) : base(buffer) => Unpack(nameof(buffer));

        /// <summary>
        /// Promote an already-parsed OWID into a 51Did by unpacking its
        /// payload fields. The OWID's Version, Domain, Date, Payload and
        /// Signature are copied by reference onto the new instance.
        /// </summary>
        /// <param name="owid">The already-parsed OWID envelope.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="owid"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="owid"/>'s payload is shorter than
        /// the minimum for its identifier type.
        /// </exception>
        public FodId(Owid.Client.Model.Owid owid) : base()
        {
            if (owid == null)
            {
                throw new ArgumentNullException(nameof(owid));
            }
            Version = owid.Version;
            Domain = owid.Domain;
            Date = owid.Date;
            Payload = owid.Payload;
            Signature = owid.Signature;
            Unpack(nameof(owid));
        }

        private void Unpack(string paramName)
        {
            if (Payload == null || Payload.Length < HeaderLength)
            {
                throw new ArgumentException(
                    $"51Did payload must be at least {HeaderLength} bytes; " +
                    $"got {Payload?.Length ?? 0}.",
                    paramName);
            }
            Flags = Payload[FlagsOffset];
            LicenseId = (uint)(
                Payload[LicenseIdOffset]
                | (Payload[LicenseIdOffset + 1] << 8)
                | (Payload[LicenseIdOffset + 2] << 16)
                | (Payload[LicenseIdOffset + 3] << 24));
            var valueLength = Type switch
            {
                IdType.Random => GuidLength,
                IdType.Reserved => Payload.Length - HeaderLength,
                _ => HashLength,
            };
            if (Payload.Length < HeaderLength + valueLength)
            {
                throw new ArgumentException(
                    $"51Did payload for the {Type} type must be at least " +
                    $"{HeaderLength + valueLength} bytes; got {Payload.Length}.",
                    paramName);
            }
            MatchKey = new byte[valueLength];
            Array.Copy(Payload, HashOffset, MatchKey, 0, valueLength);
        }
    }
}
