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
using System.Buffers.Binary;

namespace FiftyOne.Did.Model
{
    /// <summary>
    /// An OWID whose payload encodes the three fields of a 51Did: a 1-byte
    /// usage flags bitmask, a 4-byte little-endian License Id, and the
    /// 32-byte SHA-256 probabilistic identifier hash.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Payload layout (37 bytes):
    /// </para>
    /// <list type="table">
    ///   <listheader><term>Offset</term><term>Length</term><term>Field</term></listheader>
    ///   <item><term>0</term><term>1</term><term>Flags (uint8 bit-mask)</term></item>
    ///   <item><term>1</term><term>4</term><term>LicenseId (uint32 LE)</term></item>
    ///   <item><term>5</term><term>32</term><term>Hash (SHA-256)</term></item>
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
        /// Minimum byte length of a 51Did payload (Flags + LicenseId + Hash).
        /// </summary>
        public const int PayloadLength = HashOffset + HashLength;

        /// <summary>
        /// The 1-byte usage flags bit-mask from the payload.
        /// </summary>
        public byte Flags { get; private set; }

        /// <summary>
        /// The 4-byte little-endian License Id from the payload.
        /// </summary>
        public uint LicenseId { get; private set; }

        /// <summary>
        /// The 32-byte SHA-256 probabilistic identifier hash from the payload.
        /// </summary>
        public byte[] Hash { get; private set; } = Array.Empty<byte>();

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
        /// Thrown when the decoded payload is shorter than
        /// <see cref="PayloadLength"/> bytes.
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
        /// Thrown when the payload is shorter than <see cref="PayloadLength"/>
        /// bytes.
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
        /// <see cref="PayloadLength"/> bytes.
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
            // Payload can only be null on the FodId(Owid) promote path,
            // where the caller-supplied OWID may carry an explicit null.
            if (Payload == null || Payload.Length < PayloadLength)
            {
                throw new ArgumentException(
                    $"51Did payload must be at least {PayloadLength} bytes; " +
                    $"got {Payload?.Length ?? 0}.",
                    paramName);
            }
            Flags = Payload[FlagsOffset];
            LicenseId = BinaryPrimitives.ReadUInt32LittleEndian(
                Payload.AsSpan(LicenseIdOffset, LicenseIdLength));
            Hash = Payload.AsSpan(HashOffset, HashLength).ToArray();
        }
    }
}
