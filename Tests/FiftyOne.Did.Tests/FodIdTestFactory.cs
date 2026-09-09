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
        /// The canonical flags byte (0x85), being the personalized marketing
        /// usage in bits 0-2, the payload version 0 in bits 4-5 and the
        /// HashedEmail type tag in bits 6-7, so the 37-byte match key
        /// minimum applies.
        /// </summary>
        public const byte CanonicalFlags = 0b1000_0101;

        /// <summary>
        /// The Terms index a marketing identifier carries, being the Model
        /// Terms for Marketing version 2.
        /// </summary>
        public const byte MarketingTermsIndex = 1;

        /// <summary>
        /// The Terms index a non-marketing identifier carries, since the
        /// Model Terms govern marketing use and a non-marketing identifier
        /// is not created under them.
        /// </summary>
        public const byte NonMarketingTermsIndex = 0;

        /// <summary>The canonical little-endian License Id, 0x12345678.</summary>
        public const uint CanonicalLicenseId = 0x12345678u;

        /// <summary>The canonical 32-byte match key, bytes 0x20..0x3F.</summary>
        public static readonly byte[] CanonicalHash = Enumerable
            .Range(0, FodId.MatchKeyLength)
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
        /// A canonical 38-byte 51Did payload, being
        /// <see cref="CanonicalFlags"/>, <see cref="CanonicalLicenseId"/>
        /// (little-endian), <see cref="CanonicalHash"/> and the Terms byte
        /// a personalized marketing identifier carries. This is the
        /// creating side, so it writes every field an issuer writes,
        /// including the payload version in the flags byte.
        /// </summary>
        public static byte[] CanonicalPayload() =>
            WithTerms(PayloadEndingAtMatchKey(), MarketingTermsIndex);

        /// <summary>
        /// The canonical payload cut off at the end of the match key, so it
        /// carries no Terms byte. A reader takes that as a Terms of zero,
        /// and this is the fixture for that rule rather than anything an
        /// issuer would write.
        /// </summary>
        public static byte[] PayloadEndingAtMatchKey()
        {
            var payload = new byte[FodId.MinimumPayloadLength];
            payload[FodId.FlagsOffset] = CanonicalFlags;
            WriteCanonicalLicenseId(payload);
            Array.Copy(CanonicalHash, 0, payload, FodId.MatchKeyOffset, FodId.MatchKeyLength);
            return payload;
        }

        /// <summary>
        /// The given payload with the given bytes appended, which is where
        /// the Terms and anything after it sit, whatever the match key
        /// length.
        /// </summary>
        public static byte[] WithTerms(byte[] payload, params byte[] tail)
        {
            var extended = new byte[payload.Length + tail.Length];
            Array.Copy(payload, extended, payload.Length);
            Array.Copy(tail, 0, extended, payload.Length, tail.Length);
            return extended;
        }

        /// <summary>
        /// A canonical 22-byte Random payload, being the Random type tag in
        /// bits 6-7 with the payload version 0 in bits 4-5 and the
        /// non-marketing usage in bits 0-2, <see cref="CanonicalLicenseId"/>,
        /// a stable 16-byte GUID block (0x40..0x4F) and the Terms byte a
        /// non-marketing identifier carries.
        /// </summary>
        public static byte[] CanonicalRandomPayload() =>
            WithTerms(
                RandomPayloadEndingAtMatchKey(), NonMarketingTermsIndex);

        /// <summary>
        /// The canonical Random payload cut off at the end of its GUID, so
        /// it carries no Terms byte.
        /// </summary>
        public static byte[] RandomPayloadEndingAtMatchKey()
        {
            var payload = new byte[FodId.MinimumRandomPayloadLength];
            payload[FodId.FlagsOffset] = (byte)((byte)IdType.Random << 6 | 0b001);
            WriteCanonicalLicenseId(payload);
            for (int i = 0; i < FodId.GuidLength; i++)
            {
                payload[FodId.MatchKeyOffset + i] = (byte)(0x40 + i);
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
