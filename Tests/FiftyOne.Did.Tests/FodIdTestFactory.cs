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

using FiftyOne.Did.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Owid.Client.Model;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace FiftyOne.Did.Tests
{
    /// <summary>
    /// Shared test helper for the 51Did tests. Generates a fresh ECDsa P-256
    /// key pair per instance and signs real OWID envelopes with it, and builds
    /// the canonical payloads the tests assert against. Centralising this here
    /// avoids duplicating the key-generation, signing and payload code across
    /// the test classes.
    /// </summary>
    /// <remarks>
    /// The envelope bytes are written here by hand rather than through the
    /// OWID library, because an OWID can no longer be assembled by calling
    /// code and the library's creator stamps the current time, whereas
    /// these tests need to choose the date, the version and the domain. The
    /// layout written is the one the OWID reader reads, being the version
    /// byte, the ASCII domain with a zero terminator, the date (two big
    /// endian bytes of hours for version 1, otherwise four little endian
    /// bytes of minutes since 2020), the four byte little endian payload
    /// length, the payload and the 64 byte signature. The signature is
    /// ECDSA P-256 over SHA-256 of everything before it, which is what the
    /// library's creator produces.
    /// </remarks>
    internal sealed class FodIdTestFactory
    {
        /// <summary>The domain stamped into every signed test OWID.</summary>
        public const string TestDomain = "51degrees.com";

        /// <summary>
        /// The canonical flags byte (0xA5): usage bits plus the HashedEmail type
        /// tag in bits 6-7, so the 37-byte payload minimum applies.
        /// </summary>
        public const byte CanonicalFlags = 0b1010_0101;

        /// <summary>The canonical little-endian License Id, 0x12345678.</summary>
        public const uint CanonicalLicenseId = 0x12345678u;

        /// <summary>The canonical 32-byte match key, bytes 0x20..0x3F.</summary>
        public static readonly byte[] CanonicalHash = Enumerable
            .Range(0, FodId.HashLength)
            .Select(i => (byte)(0x20 + i))
            .ToArray();

        /// <summary>
        /// The length of an OWID signature, fixed by the OWID format.
        /// </summary>
        public const int SignatureLength = 64;

        private readonly string _privatePem;

        /// <summary>
        /// The PEM-encoded public key matching the private key used to sign,
        /// for signature-verification tests.
        /// </summary>
        public string PublicPem { get; }

        /// <summary>
        /// Generate a fresh ECDsa P-256 key pair for this instance.
        /// </summary>
        public FodIdTestFactory()
        {
            using var crypto = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            PublicPem = new string(PemEncoding.Write(
                "PUBLIC KEY", crypto.ExportSubjectPublicKeyInfo()));
            _privatePem = new string(PemEncoding.Write(
                "PRIVATE KEY", crypto.ExportPkcs8PrivateKey()));
        }

        /// <summary>
        /// A canonical 37-byte 51Did payload: <see cref="CanonicalFlags"/>,
        /// <see cref="CanonicalLicenseId"/> (little-endian) and
        /// <see cref="CanonicalHash"/>.
        /// </summary>
        public static byte[] CanonicalPayload()
        {
            var payload = new byte[FodId.PayloadLength];
            payload[FodId.FlagsOffset] = CanonicalFlags;
            WriteCanonicalLicenseId(payload);
            Array.Copy(CanonicalHash, 0, payload, FodId.HashOffset, FodId.HashLength);
            return payload;
        }

        /// <summary>
        /// A canonical 21-byte Random payload: the Random type tag in bits 6-7
        /// plus usage bits 0b001, <see cref="CanonicalLicenseId"/>, and a stable
        /// 16-byte GUID block (0x40..0x4F).
        /// </summary>
        public static byte[] CanonicalRandomPayload()
        {
            var payload = new byte[FodId.RandomPayloadLength];
            payload[FodId.FlagsOffset] = (byte)((byte)IdType.Random << 6 | 0b001);
            WriteCanonicalLicenseId(payload);
            for (int i = 0; i < FodId.GuidLength; i++)
            {
                payload[FodId.HashOffset + i] = (byte)(0x40 + i);
            }
            return payload;
        }

        /// <summary>
        /// Create and sign a real OWID with the given payload, using this
        /// instance's key pair.
        /// </summary>
        public Owid.Client.Model.Owid SignedOwid(byte[] payload) =>
            SignedOwid(payload, DateTime.UtcNow);

        /// <summary>
        /// Create and sign a real OWID with the given payload and date,
        /// using this instance's key pair, at the given envelope version,
        /// handed back through the OWID library's own parse so that what
        /// the tests hold is exactly what a caller would hold.
        /// </summary>
        public Owid.Client.Model.Owid SignedOwid(
            byte[] payload,
            DateTime date,
            OwidVersion version = OwidVersion.Version3,
            string domain = TestDomain)
        {
            var bytes = SignedBytes(payload, date, version, domain);
            Assert.IsTrue(
                Owid.Client.Model.Owid.TryParse(
                    bytes, out var owid, out var status),
                $"The factory wrote an envelope the OWID reader refused: {status}");
            return owid!;
        }

        /// <summary>
        /// Sign the given payload and return the OWID as base64.
        /// </summary>
        public string SignedOwidBase64(byte[] payload) =>
            Convert.ToBase64String(SignedBytes(
                payload, DateTime.UtcNow, OwidVersion.Version3, TestDomain));

        /// <summary>
        /// The raw bytes of a signed envelope, for tests that need to
        /// damage the envelope after signing.
        /// </summary>
        public byte[] SignedBytes(
            byte[] payload,
            DateTime date,
            OwidVersion version = OwidVersion.Version3,
            string domain = TestDomain)
        {
            var unsigned = UnsignedBytes(payload, date, version, domain);
            using var crypto = ECDsa.Create();
            crypto.ImportFromPem(_privatePem);
            var signature = crypto.SignData(unsigned, HashAlgorithmName.SHA256);
            Assert.AreEqual(SignatureLength, signature.Length);
            var bytes = new byte[unsigned.Length + signature.Length];
            unsigned.CopyTo(bytes, 0);
            signature.CopyTo(bytes, unsigned.Length);
            return bytes;
        }

        /// <summary>
        /// The bytes of an envelope up to but not including the signature,
        /// which is the data the signature covers.
        /// </summary>
        public static byte[] UnsignedBytes(
            byte[] payload,
            DateTime date,
            OwidVersion version = OwidVersion.Version3,
            string domain = TestDomain)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write((byte)version);
            writer.Write(Encoding.ASCII.GetBytes(domain));
            writer.Write((byte)0);
            var utc = date.Kind == DateTimeKind.Local
                ? date.ToUniversalTime()
                : DateTime.SpecifyKind(date, DateTimeKind.Utc);
            if (version == OwidVersion.Version1)
            {
                var hours = (int)(utc - FodId.DateBase).TotalHours;
                writer.Write((byte)(hours >> 8));
                writer.Write((byte)hours);
            }
            else
            {
                var minutes = (utc - FodId.DateBase).TotalMinutes;
                writer.Write(minutes <= 0 ? 0u : (uint)minutes);
            }
            writer.Write((uint)payload.Length);
            writer.Write(payload);
            writer.Flush();
            return stream.ToArray();
        }

        private static void WriteCanonicalLicenseId(byte[] payload)
        {
            // Little-endian: low byte first.
            payload[FodId.LicenseIdOffset + 0] = 0x78;
            payload[FodId.LicenseIdOffset + 1] = 0x56;
            payload[FodId.LicenseIdOffset + 2] = 0x34;
            payload[FodId.LicenseIdOffset + 3] = 0x12;
        }
    }
}
