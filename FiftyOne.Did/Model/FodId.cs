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
    /// Only a lower bound is applied to the payload. Anything after the
    /// value is a creator context section whose lengths belong to the
    /// cloud, so a longer payload is accepted here and the same three
    /// fields are exposed.
    /// </para>
    /// <para>
    /// How an instance comes to exist. An OWID cannot be assembled by a
    /// caller, because an unsigned one would be indistinguishable from a
    /// signed one downstream. A <see cref="FodId"/> therefore reaches
    /// calling code only by parsing bytes that were already a complete,
    /// signed envelope, through
    /// <see cref="TryParse(string, out FodId, out FodIdParseStatus)"/>,
    /// <see cref="TryParse(byte[], out FodId, out FodIdParseStatus)"/>,
    /// or the throwing constructors and <see cref="FromBase64"/> built on
    /// them. Every route runs the same walk, so the rules cannot drift.
    /// </para>
    /// <para>
    /// Parsing is not verification. A parsed <see cref="FodId"/> is
    /// structurally a 51Did and nothing more. Whether the signature is
    /// genuine is a separate question, answered by
    /// <c>SignatureStatus</c> from the OWID library on this instance, or
    /// by <c>DidClient.VerifySignatureDetailedAsync</c>, which also picks
    /// the signing key in force at the identifier's date.
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
        /// <remarks>
        /// This constant predates the OWID library's instance property of
        /// the same name, which reports how many bytes a particular
        /// payload holds, and it is kept because callers reference it by
        /// type. On a <see cref="FodId"/> reference the name resolves to
        /// this constant. The actual length of an instance's payload is
        /// <c>Payload.Length</c>, or the inherited property through an
        /// <see cref="global::Owid.Client.Model.Owid"/> reference.
        /// </remarks>
        public new const int PayloadLength = HashOffset + HashLength;

        /// <summary>
        /// The 1-byte usage flags bit-mask from the payload.
        /// </summary>
        public byte Flags { get; }

        /// <summary>
        /// The identifier type carried in bits 6-7 of <see cref="Flags"/>.
        /// </summary>
        public IdType Type => TypeOf(Flags);

        /// <summary>
        /// The 4-byte little-endian License Id from the payload, as the
        /// raw value of the field. On an identifier carrying a creator
        /// context these four bytes hold an encrypted value that only
        /// 51Degrees can turn back into a licence identifier, so the
        /// property identifies nothing outside 51Degrees and two
        /// identifiers for the same licence need not share it.
        /// </summary>
        public uint LicenseId { get; }

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
        public byte[] Hash { get; }

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
        /// Reads a 51Did from its base64 form, in either alphabet, saying
        /// why rather than throwing when the value is not one.
        /// </summary>
        /// <param name="value">
        /// The encoded envelope, which may be anything at all. The standard
        /// alphabet with padding, as the cloud issues it, and the URL-safe
        /// alphabet with or without padding, as a page puts it in a link,
        /// are both accepted, and surrounding whitespace is ignored. See
        /// <see cref="NormaliseBase64"/>.
        /// </param>
        /// <param name="fodId">
        /// The 51Did when this returns true, otherwise null. A value is
        /// never handed back for a failure, however far the read got.
        /// </param>
        /// <param name="status">
        /// <see cref="FodIdParseStatus.Parsed"/> when this returns true,
        /// otherwise why the value is not a 51Did. A failure the OWID
        /// reader found carries the OWID status unchanged.
        /// </param>
        /// <returns>
        /// True when the value is a complete, structurally valid 51Did
        /// whose payload meets the minimum for its type. This says nothing
        /// about whether the signature is genuine, which is a separate
        /// question with its own answer.
        /// </returns>
        public static bool TryParse(
            string? value,
            out FodId? fodId,
            out FodIdParseStatus status)
        {
            // The alphabet is restored before the OWID reader sees the
            // string, because the OWID reader accepts the standard alphabet
            // only and the URL-safe form is how a page puts a 51Did in a
            // link.
            if (Owid.Client.Model.Owid.TryParse(
                    NormaliseBase64(value),
                    out var owid,
                    out var owidStatus) == false)
            {
                fodId = null;
                status = (FodIdParseStatus)owidStatus;
                return false;
            }
            return TryUnpack(owid!, out fodId, out status);
        }

        /// <summary>
        /// Reads a 51Did from the raw bytes of an envelope, saying why
        /// rather than throwing when the bytes are not one.
        /// </summary>
        /// <param name="buffer">
        /// The bytes, which must be one whole envelope and nothing else.
        /// </param>
        /// <param name="fodId">
        /// The 51Did when this returns true, otherwise null.
        /// </param>
        /// <param name="status">
        /// <see cref="FodIdParseStatus.Parsed"/> when this returns true,
        /// otherwise why the bytes are not a 51Did. A failure the OWID
        /// reader found carries the OWID status unchanged.
        /// </param>
        /// <returns>
        /// True when the bytes are a complete, structurally valid 51Did
        /// whose payload meets the minimum for its type. The signature has
        /// not been checked.
        /// </returns>
        public static bool TryParse(
            byte[]? buffer,
            out FodId? fodId,
            out FodIdParseStatus status)
        {
            if (Owid.Client.Model.Owid.TryParse(
                    buffer,
                    out var owid,
                    out var owidStatus) == false)
            {
                fodId = null;
                status = (FodIdParseStatus)owidStatus;
                return false;
            }
            return TryUnpack(owid!, out fodId, out status);
        }

        /// <summary>
        /// Parse a 51Did from its base64-encoded OWID string, in either
        /// base64 alphabet. The throwing form of
        /// <see cref="TryParse(string, out FodId, out FodIdParseStatus)"/>,
        /// which runs the same walk and turns a failure into the exception
        /// documented below.
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
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="base64"/> is empty, or when the
        /// decoded payload is shorter than the minimum for its identifier
        /// type. Anything beyond that minimum is a creator context section,
        /// whose exact lengths belong to the cloud, so any longer payload
        /// is accepted here.
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown when <paramref name="base64"/> is not valid Base64 in
        /// either alphabet, or decodes to bytes that are not an OWID
        /// envelope. The message names the
        /// <see cref="FodIdParseStatus"/> found.
        /// </exception>
        public FodId(string base64) : this(Parse(base64, nameof(base64)))
        {
        }

        /// <summary>
        /// Parse a 51Did from its base64-encoded OWID string, in either
        /// base64 alphabet. The same as the string constructor, named so
        /// the parse reads as one call.
        /// </summary>
        /// <param name="base64">The envelope as base64.</param>
        /// <returns>The parsed 51Did, its signature not yet checked.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="base64"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="base64"/> is empty, or when the
        /// decoded payload is shorter than the minimum for its identifier
        /// type. Any longer payload is accepted.
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown when <paramref name="base64"/> is not valid Base64 in
        /// either alphabet, or decodes to bytes that are not an OWID
        /// envelope.
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
        /// Parse a 51Did from the raw bytes of an OWID envelope. The
        /// throwing form of
        /// <see cref="TryParse(byte[], out FodId, out FodIdParseStatus)"/>.
        /// </summary>
        /// <param name="buffer">The OWID envelope bytes.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="buffer"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="buffer"/> is empty, or when the
        /// payload is shorter than the minimum for its identifier type.
        /// Any longer payload is accepted.
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown when the bytes are not an OWID envelope. The message
        /// names the <see cref="FodIdParseStatus"/> found.
        /// </exception>
        public FodId(byte[] buffer) : this(Parse(buffer, nameof(buffer)))
        {
        }

        /// <summary>
        /// Promote an already-parsed OWID into a 51Did by unpacking its
        /// payload fields. The OWID's Version, Domain, Date, Payload and
        /// Signature are carried onto the new instance. An OWID only
        /// exists as the result of a successful parse or of a creator
        /// signing one, so the envelope is complete and signed by the time
        /// the payload rules run here.
        /// </summary>
        /// <param name="owid">The already-parsed OWID envelope.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="owid"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the payload of <paramref name="owid"/> is shorter
        /// than the minimum for its identifier type. Any longer payload is
        /// accepted.
        /// </exception>
        public FodId(Owid.Client.Model.Owid owid)
            : this(Promote(owid, nameof(owid)))
        {
        }

        /// <summary>
        /// The one constructor that sets state, reached only with fields
        /// that <see cref="TryUnpack"/> has already accepted. The envelope
        /// fields are carried by reference from the parsed OWID, which is
        /// safe because the OWID library never exposes those arrays
        /// without copying.
        /// </summary>
        private FodId(in Unpacked unpacked)
        {
            Version = unpacked.Owid.Version;
            Domain = unpacked.Owid.Domain;
            Date = unpacked.Owid.Date;
            PayloadInternal = unpacked.Owid.PayloadInternal;
            SignatureInternal = unpacked.Owid.SignatureInternal;
            Flags = unpacked.Flags;
            LicenseId = unpacked.LicenseId;
            Hash = unpacked.Hash;
        }

        /// <summary>
        /// The fields of a 51Did once the payload rules have accepted the
        /// envelope, so that the constructor that sets state cannot be
        /// reached with anything unchecked.
        /// </summary>
        private readonly struct Unpacked
        {
            public Unpacked(
                Owid.Client.Model.Owid owid,
                byte flags,
                uint licenseId,
                byte[] hash)
            {
                Owid = owid;
                Flags = flags;
                LicenseId = licenseId;
                Hash = hash;
            }

            public Owid.Client.Model.Owid Owid { get; }
            public byte Flags { get; }
            public uint LicenseId { get; }
            public byte[] Hash { get; }
        }

        private static IdType TypeOf(byte flags) => (IdType)((flags >> 6) & 0b11);

        /// <summary>
        /// Builds the instance once <see cref="Unpack"/> has accepted the
        /// envelope, or reports why the envelope is not a 51Did.
        /// </summary>
        private static bool TryUnpack(
            Owid.Client.Model.Owid owid,
            out FodId? fodId,
            out FodIdParseStatus status)
        {
            status = Unpack(owid, out var unpacked);
            if (status != FodIdParseStatus.Parsed)
            {
                fodId = null;
                return false;
            }
            fodId = new FodId(unpacked);
            return true;
        }

        /// <summary>
        /// The 51Did payload rules, applied to an OWID the OWID reader has
        /// already accepted. Every route into a <see cref="FodId"/> passes
        /// through here, so there is one walk and not two.
        /// </summary>
        /// <returns>
        /// <see cref="FodIdParseStatus.Parsed"/> with
        /// <paramref name="unpacked"/> filled, or the reason the payload
        /// is not a 51Did with <paramref name="unpacked"/> at its default.
        /// </returns>
        private static FodIdParseStatus Unpack(
            Owid.Client.Model.Owid owid,
            out Unpacked unpacked)
        {
            unpacked = default;
            var payload = owid.PayloadInternal;
            if (payload.Length < HeaderLength)
            {
                return FodIdParseStatus.PayloadTooShort;
            }
            var flags = payload[FlagsOffset];
            // Only a lower bound is applied. Anything beyond the base
            // length for the type is a creator context section, whose
            // exact lengths belong to the cloud, so any longer payload is
            // accepted and left to the cloud to judge. The Reserved type
            // takes everything after the header, at whatever length a
            // future context version brings.
            var valueLength = TypeOf(flags) switch
            {
                IdType.Random => GuidLength,
                IdType.Reserved => payload.Length - HeaderLength,
                _ => HashLength,
            };
            if (payload.Length < HeaderLength + valueLength)
            {
                return FodIdParseStatus.InvalidTypePayloadLength;
            }
            var licenseId = (uint)(
                payload[LicenseIdOffset]
                | (payload[LicenseIdOffset + 1] << 8)
                | (payload[LicenseIdOffset + 2] << 16)
                | (payload[LicenseIdOffset + 3] << 24));
            var hash = new byte[valueLength];
            Array.Copy(payload, HashOffset, hash, 0, valueLength);
            unpacked = new Unpacked(owid, flags, licenseId, hash);
            return FodIdParseStatus.Parsed;
        }

        private static Unpacked Parse(string base64, string paramName)
        {
            if (base64 == null)
            {
                throw new ArgumentNullException(paramName);
            }
            if (Owid.Client.Model.Owid.TryParse(
                    NormaliseBase64(base64),
                    out var owid,
                    out var owidStatus) == false)
            {
                throw ExceptionFor((FodIdParseStatus)owidStatus, paramName);
            }
            return Promote(owid!, paramName);
        }

        private static Unpacked Parse(byte[] buffer, string paramName)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(paramName);
            }
            if (Owid.Client.Model.Owid.TryParse(
                    buffer,
                    out var owid,
                    out var owidStatus) == false)
            {
                throw ExceptionFor((FodIdParseStatus)owidStatus, paramName);
            }
            return Promote(owid!, paramName);
        }

        /// <summary>
        /// Runs the payload rules for the throwing surface, so that the
        /// throwing constructors and the non-throwing parse share one
        /// walk.
        /// </summary>
        private static Unpacked Promote(
            Owid.Client.Model.Owid owid,
            string paramName)
        {
            if (owid == null)
            {
                throw new ArgumentNullException(paramName);
            }
            var status = Unpack(owid, out var unpacked);
            if (status != FodIdParseStatus.Parsed)
            {
                throw ExceptionFor(status, paramName);
            }
            return unpacked;
        }

        /// <summary>
        /// The exception the throwing surface documents for each failure.
        /// Nothing supplied and a payload that breaks the 51Did rules are
        /// argument problems. A value that is not an envelope at all,
        /// whether the base64 or the bytes under it, is a format problem.
        /// </summary>
        private static Exception ExceptionFor(
            FodIdParseStatus status,
            string paramName)
        {
            switch (status)
            {
                case FodIdParseStatus.MissingInput:
                    return new ArgumentException(
                        $"A 51Did is required ({status}).", paramName);
                case FodIdParseStatus.PayloadTooShort:
                    return new ArgumentException(
                        $"51Did payload must be at least {HeaderLength} "
                        + $"bytes ({status}).",
                        paramName);
                case FodIdParseStatus.InvalidTypePayloadLength:
                    return new ArgumentException(
                        "51Did payload is shorter than the minimum for its "
                        + $"identifier type ({status}).",
                        paramName);
                default:
                    return new FormatException(
                        $"The value is not a 51Did envelope ({status}).");
            }
        }
    }
}
