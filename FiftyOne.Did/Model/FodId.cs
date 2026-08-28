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

using Owid.Client;
using System;

namespace FiftyOne.Did.Model
{
    /// <summary>
    /// An OWID whose payload encodes the three fields of a 51Did: a 1-byte
    /// flags bitmask (usage tier and identifier type), a 4-byte
    /// little-endian License Id, and the identifier value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Terminology. A 51Did is described at three levels. The 51Did is
    /// the identifier as a whole, meaning the concept and its rules. The
    /// envelope is the data model that carries it, this OWID, holding the
    /// version, domain, date, payload and signature, re-issued fresh on
    /// every call. The value is the part of the envelope that is stable
    /// and comparable, the payload bytes after Flags and LicenseId,
    /// exposed as <see cref="Hash"/>. Two responses for the same inputs
    /// share the same value but differ at the byte level because the
    /// envelope embeds a fresh date and signature on each call. Compare
    /// values, never envelopes.
    /// </para>
    /// <para>
    /// Payload layout. The header (offsets 0-4) is shared by every
    /// identifier type; bits 6-7 of Flags select the type and the length
    /// of the value that follows:
    /// </para>
    /// <list type="table">
    ///   <listheader><term>Offset</term><term>Length</term><term>Field</term></listheader>
    ///   <item><term>0</term><term>1</term><term>Flags (bits 0-2 usage, bits 6-7 type)</term></item>
    ///   <item><term>1</term><term>4</term><term>LicenseId (uint32 LE)</term></item>
    ///   <item><term>5</term><term>32</term><term>Value: SHA-256 (Probabilistic, HashedEmail)</term></item>
    ///   <item><term>5</term><term>16</term><term>Value: GUID (Random)</term></item>
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
        /// Byte offset of the Hash field within the payload.
        /// </summary>
        public const int HashOffset = 5;

        /// <summary>
        /// Byte length of the Hash field (SHA-256).
        /// </summary>
        public const int HashLength = 32;

        /// <summary>
        /// Byte length of the payload header (Flags + LicenseId) that is
        /// common to every identifier type.
        /// </summary>
        public const int HeaderLength = HashOffset;

        /// <summary>
        /// Byte length of the GUID value carried by Random identifiers.
        /// </summary>
        public const int GuidLength = 16;

        /// <summary>
        /// Minimum byte length of a Random 51Did payload
        /// (Flags + LicenseId + GUID).
        /// </summary>
        public const int RandomPayloadLength = HeaderLength + GuidLength;

        /// <summary>
        /// Minimum byte length of a Probabilistic or HashedEmail 51Did
        /// payload (Flags + LicenseId + Hash). Random payloads are
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
        /// The 4-byte little-endian License Id from the payload, as the
        /// raw value of the field. On an identifier carrying a creator
        /// context these four bytes hold an encrypted value that only
        /// 51Degrees can turn back into a licence identifier, so the
        /// property identifies nothing outside 51Degrees and two
        /// identifiers for the same licence need not share it.
        /// </summary>
        public uint LicenseId { get; private set; }

        /// <summary>
        /// The value bytes from the payload, a 32-byte SHA-256 for
        /// Probabilistic and HashedEmail identifiers, or 16 GUID bytes for
        /// Random ones. This is the stable, comparable part of the
        /// envelope. Two 51Dids for the same inputs share the same value
        /// even though their envelopes (date, signature) differ on every
        /// issue. Treat it as the cache / dedup key. SHA-256 is the
        /// underlying hash function for the probabilistic and hashed-email
        /// types, and the property is named Hash to reflect that.
        /// </summary>
        public byte[] Hash { get; private set; } = Array.Empty<byte>();

        /// <summary>
        /// The moment the envelope's date field counts minutes from,
        /// 2020-01-01T00:00:00Z. See <see cref="DateMinutes"/>.
        /// </summary>
        public static readonly DateTime DateBase =
            new DateTime(2020, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// The envelope's own date as the unsigned 32-bit count of minutes
        /// since <see cref="DateBase"/>, which is the value the field holds
        /// on the wire and the value the OWID <c>public-key?date=</c>
        /// parameter takes. Callers comparing creation times want this
        /// integer rather than the converted
        /// <see cref="global::Owid.Client.Model.Owid.Date"/>.
        /// A date before the base gives zero.
        /// </summary>
        public uint DateMinutes
        {
            get
            {
                var minutes = (Date - DateBase).TotalMinutes;
                if (minutes <= 0)
                {
                    return 0;
                }
                return minutes >= uint.MaxValue ? uint.MaxValue : (uint)minutes;
            }
        }

        /// <summary>
        /// Parse a 51Did from its base64-encoded OWID string, in either
        /// base64 alphabet.
        /// </summary>
        /// <param name="base64">
        /// Base64 of the full OWID envelope (version + domain + date +
        /// length-prefixed payload + 64-byte signature) as produced by the
        /// 51Degrees cloud service. The standard alphabet with padding, as
        /// the cloud issues it, and the URL-safe alphabet with or without
        /// padding, as a page puts it in a link, are both accepted. See
        /// <see cref="NormaliseBase64"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="base64"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown by the underlying OWID parser when <paramref name="base64"/>
        /// is not valid Base64 in either alphabet.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the decoded payload is shorter than the minimum
        /// for its identifier type. Anything beyond that minimum is a
        /// creator context section, whose exact lengths belong to the
        /// cloud, so any longer payload is accepted here.
        /// </exception>
        public FodId(string base64) : base(NormaliseBase64(base64)!)
            => Unpack(nameof(base64));

        /// <summary>
        /// Parse a 51Did from its base64-encoded OWID string, in either
        /// base64 alphabet. The same as the string constructor, named so
        /// the parse reads as one call.
        /// </summary>
        /// <param name="base64">The envelope as base64.</param>
        /// <returns>The parsed 51Did.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="base64"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown when <paramref name="base64"/> is not valid Base64 in
        /// either alphabet.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the decoded payload is shorter than the minimum
        /// for its identifier type. Any longer payload is accepted.
        /// </exception>
        public static FodId FromBase64(string base64) => new FodId(base64);

        /// <summary>
        /// The envelope in the URL-safe base64 alphabet without padding,
        /// so it can be put in a URL without further conversion. The
        /// inverse of <see cref="NormaliseBase64"/>, and accepted back by
        /// <see cref="FromBase64"/> and every cloud endpoint.
        /// </summary>
        /// <returns>The URL-safe form.</returns>
        public string AsBase64Url() => ToBase64Url(this.AsBase64());

        /// <summary>
        /// Restores a base64 string in either alphabet to the standard
        /// alphabet with padding, as <c>Convert.FromBase64String</c>
        /// expects. Leading and trailing whitespace is removed first, so a
        /// value read from a file or a header with a trailing newline
        /// behaves as the clean value does, then <c>-</c> becomes
        /// <c>+</c>, <c>_</c> becomes <c>/</c>, and padding is added when
        /// the trimmed length modulo 4 is 2 or 3. A value already in the
        /// standard form is returned unchanged apart from that trim.
        /// </summary>
        /// <param name="value">The base64 in either alphabet.</param>
        /// <returns>
        /// The standard form, or <c>null</c> when <paramref name="value"/>
        /// is <c>null</c>.
        /// </returns>
        public static string? NormaliseBase64(string? value)
        {
            if (value == null)
            {
                return null;
            }
            // Trim before anything else, because the padding below is
            // decided by the length and whitespace would make that length
            // wrong.
            var base64 = value.Trim().Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2: return base64 + "==";
                case 3: return base64 + "=";
                default: return base64;
            }
        }

        /// <summary>
        /// Converts standard base64 to the URL-safe alphabet without
        /// padding: <c>+</c> becomes <c>-</c>, <c>/</c> becomes <c>_</c>,
        /// and trailing <c>=</c> is removed.
        /// </summary>
        /// <param name="base64">The standard base64.</param>
        /// <returns>The URL-safe form.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="base64"/> is <c>null</c>.
        /// </exception>
        public static string ToBase64Url(string base64)
        {
            if (base64 == null)
            {
                throw new ArgumentNullException(nameof(base64));
            }
            return base64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        /// <summary>
        /// Parse a 51Did from the raw bytes of an OWID envelope.
        /// </summary>
        /// <param name="buffer">The OWID envelope bytes.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="buffer"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the payload is shorter than the minimum for its
        /// identifier type. Any longer payload is accepted.
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
        /// the minimum for its identifier type. Any longer payload is
        /// accepted.
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
            // Only a lower bound is applied here. Anything beyond the base
            // length for the type is a creator context section, whose exact
            // lengths belong to the cloud, so any longer payload is
            // accepted and left to the cloud to judge. The Reserved type
            // takes everything after the header, at whatever length a
            // future context version brings.
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
            Hash = new byte[valueLength];
            Array.Copy(Payload, HashOffset, Hash, 0, valueLength);
        }
    }
}
