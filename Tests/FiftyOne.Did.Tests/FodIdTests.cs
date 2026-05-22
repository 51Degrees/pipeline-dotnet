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
using Owid.Client;
using Owid.Client.Model;
using System;
using System.Buffers.Binary;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace FiftyOne.Did.Tests
{
    /// <summary>
    /// Tests for <see cref="FodId"/>.
    /// </summary>
    [TestClass]
    public class FodIdTests
    {
        private const string TestDomain = "51degrees.com";

        private static readonly byte[] CanonicalHash = Enumerable
            .Range(0, FodId.HashLength)
            .Select(i => (byte)(0x20 + i))
            .ToArray();

        private const byte CanonicalFlags = 0b1010_0101;
        private const uint CanonicalLicenseId = 0x12345678u;

        private static string PublicPem = string.Empty;
        private static string PrivatePem = string.Empty;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
        {
            using var crypto = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            PublicPem = new string(PemEncoding.Write(
                "PUBLIC KEY", crypto.ExportSubjectPublicKeyInfo()));
            PrivatePem = new string(PemEncoding.Write(
                "PRIVATE KEY", crypto.ExportPkcs8PrivateKey()));
        }

        /// <summary>
        /// A canonical 37-byte 51Did payload with flags = 0xA5,
        /// licenseId = 0x12345678 (little-endian), and a stable 32-byte hash
        /// (0x20..0x3F). All field-level assertions key off these values.
        /// </summary>
        private static byte[] CanonicalPayload()
        {
            var payload = new byte[FodId.PayloadLength];
            payload[FodId.FlagsOffset] = CanonicalFlags;
            payload[FodId.LicenseIdOffset + 0] = 0x78;
            payload[FodId.LicenseIdOffset + 1] = 0x56;
            payload[FodId.LicenseIdOffset + 2] = 0x34;
            payload[FodId.LicenseIdOffset + 3] = 0x12;
            Array.Copy(CanonicalHash, 0, payload, FodId.HashOffset, FodId.HashLength);
            return payload;
        }

        private static byte[] PayloadWithLicenseId(uint licenseId)
        {
            var payload = CanonicalPayload();
            BinaryPrimitives.WriteUInt32LittleEndian(
                payload.AsSpan(FodId.LicenseIdOffset, FodId.LicenseIdLength),
                licenseId);
            return payload;
        }

        private static byte[] PayloadWithFlags(byte flags)
        {
            var payload = CanonicalPayload();
            payload[FodId.FlagsOffset] = flags;
            return payload;
        }

        /// <summary>
        /// Create and sign a real OWID with the given payload using a freshly
        /// generated ECDsa P-256 key pair.
        /// </summary>
        private Owid.Client.Model.Owid SignedOwid(byte[] payload)
        {
            using var crypto = ECDsa.Create();
            crypto.ImportFromPem(PrivatePem);
            var creator = new Creator(TestDomain, crypto);
            var owid = new Owid.Client.Model.Owid
            {
                Date = DateTime.UtcNow,
                Payload = payload,
            };
            creator.Sign(owid);
            return owid;
        }

        private string SignedOwidBase64(byte[] payload) =>
            SignedOwid(payload).AsBase64();

        [TestMethod]
        public void Constants_AreInternallyConsistent()
        {
            // Guards against someone changing one offset/length constant
            // without updating the others. The MSTEST0032 analyzer sees these
            // as constant-folded so flags them — suppress locally.
#pragma warning disable MSTEST0032
            Assert.AreEqual(
                FodId.HashOffset + FodId.HashLength,
                FodId.PayloadLength);
            Assert.AreEqual(
                FodId.LicenseIdOffset + FodId.LicenseIdLength,
                FodId.HashOffset);
#pragma warning restore MSTEST0032
        }

        [TestMethod]
        public void FodId_IsAnOwid()
        {
            var fodId = new FodId(SignedOwidBase64(CanonicalPayload()));

            Assert.IsInstanceOfType<Owid.Client.Model.Owid>(fodId);
        }

        [TestMethod]
        public void Constructor_FromBase64_UnpacksAllThreeFields()
        {
            var base64 = SignedOwidBase64(CanonicalPayload());

            var fodId = new FodId(base64);

            Assert.AreEqual(CanonicalFlags, fodId.Flags);
            Assert.AreEqual(CanonicalLicenseId, fodId.LicenseId);
            CollectionAssert.AreEqual(CanonicalHash, fodId.Hash);
            Assert.AreEqual(TestDomain, fodId.Domain);
        }

        [TestMethod]
        public void Constructor_FromBytes_UnpacksAllThreeFields()
        {
            var base64 = SignedOwidBase64(CanonicalPayload());
            var bytes = Convert.FromBase64String(base64);

            var fodId = new FodId(bytes);

            Assert.AreEqual(CanonicalFlags, fodId.Flags);
            Assert.AreEqual(CanonicalLicenseId, fodId.LicenseId);
            CollectionAssert.AreEqual(CanonicalHash, fodId.Hash);
            Assert.AreEqual(TestDomain, fodId.Domain);
        }

        [TestMethod]
        public void Constructor_FromOwid_UnpacksAllThreeFields()
        {
            var owid = SignedOwid(CanonicalPayload());

            var fodId = new FodId(owid);

            Assert.AreEqual(CanonicalFlags, fodId.Flags);
            Assert.AreEqual(CanonicalLicenseId, fodId.LicenseId);
            CollectionAssert.AreEqual(CanonicalHash, fodId.Hash);
            Assert.AreEqual(owid.Domain, fodId.Domain);
            Assert.AreEqual(owid.Date, fodId.Date);
            Assert.AreEqual(owid.Version, fodId.Version);
            // Payload and Signature are reference-copied to avoid re-parsing.
            Assert.AreSame(owid.Payload, fodId.Payload);
            Assert.AreSame(owid.Signature, fodId.Signature);
        }

        [TestMethod]
        public void Constructor_NullOwid_Throws()
        {
            Assert.ThrowsExactly<ArgumentNullException>(
                () => new FodId((Owid.Client.Model.Owid)null!));
        }

        [TestMethod]
        [DataRow(1u)]
        [DataRow(uint.MaxValue)]
        [DataRow(0x8000_0000u)]
        public void LicenseId_RoundTripsAsUnsignedLittleEndian(uint licenseId)
        {
            var fodId = new FodId(SignedOwidBase64(PayloadWithLicenseId(licenseId)));

            Assert.AreEqual(licenseId, fodId.LicenseId);
        }

        [TestMethod]
        [DataRow((byte)0x00)]
        [DataRow((byte)0xFF)]
        public void Flags_RoundTripsAsByte(byte flags)
        {
            var fodId = new FodId(SignedOwidBase64(PayloadWithFlags(flags)));

            Assert.AreEqual(flags, fodId.Flags);
        }

        [TestMethod]
        public void Hash_IsDefensiveCopy()
        {
            var fodId = new FodId(SignedOwidBase64(CanonicalPayload()));

            fodId.Hash[0] = 0x00;
            fodId.Hash[FodId.HashLength - 1] = 0x00;

            Assert.AreEqual(CanonicalHash[0], fodId.Payload[FodId.HashOffset]);
            Assert.AreEqual(
                CanonicalHash[FodId.HashLength - 1],
                fodId.Payload[FodId.HashOffset + FodId.HashLength - 1]);
        }

        [TestMethod]
        public void Constructor_PayloadOneByteShort_Throws()
        {
            var base64 = SignedOwidBase64(new byte[FodId.PayloadLength - 1]);

            Assert.ThrowsExactly<ArgumentException>(() => new FodId(base64));
        }

        [TestMethod]
        public void Constructor_PayloadEmpty_Throws()
        {
            var base64 = SignedOwidBase64(Array.Empty<byte>());

            Assert.ThrowsExactly<ArgumentException>(() => new FodId(base64));
        }

        [TestMethod]
        public void Constructor_NullBase64_Throws()
        {
            Assert.ThrowsExactly<ArgumentNullException>(
                () => new FodId((string)null!));
        }

        [TestMethod]
        public void Constructor_NullBuffer_Throws()
        {
            Assert.ThrowsExactly<ArgumentNullException>(
                () => new FodId((byte[])null!));
        }

        [TestMethod]
        public void Constructor_InvalidBase64_Throws()
        {
            Assert.ThrowsExactly<FormatException>(
                () => new FodId("This is not valid Base64!@#$"));
        }

        [TestMethod]
        public void Constructor_PayloadLargerThanSpec_UsesFirstPayloadLengthBytes()
        {
            // Trailing bytes past PayloadLength must be ignored.
            var payload = new byte[64];
            Array.Copy(CanonicalPayload(), payload, FodId.PayloadLength);
            Array.Fill(payload, (byte)0xCC, FodId.PayloadLength, payload.Length - FodId.PayloadLength);

            var fodId = new FodId(SignedOwidBase64(payload));

            Assert.AreEqual(CanonicalFlags, fodId.Flags);
            Assert.AreEqual(CanonicalLicenseId, fodId.LicenseId);
            CollectionAssert.AreEqual(CanonicalHash, fodId.Hash);
            Assert.AreEqual(FodId.HashLength, fodId.Hash.Length);
        }

        [TestMethod]
        public async Task FodId_IsCryptographicallyVerifiable()
        {
            var fodId = new FodId(SignedOwidBase64(CanonicalPayload()));

            using var verifyKey = ECDsa.Create();
            verifyKey.ImportFromPem(PublicPem);
            Assert.IsTrue(await fodId.VerifyAsync(verifyKey));
        }

        [TestMethod]
        public void Base64Roundtrip_PreservesAllFields()
        {
            var fodId1 = new FodId(SignedOwidBase64(CanonicalPayload()));
            var fodId2 = new FodId(fodId1.AsBase64());

            Assert.AreEqual(fodId1.Flags, fodId2.Flags);
            Assert.AreEqual(fodId1.LicenseId, fodId2.LicenseId);
            CollectionAssert.AreEqual(fodId1.Hash, fodId2.Hash);
            Assert.AreEqual(fodId1.Domain, fodId2.Domain);
        }
    }
}
