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

        private string PublicPem = string.Empty;
        private string PrivatePem = string.Empty;

        [TestInitialize]
        public void TestInitialize()
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
            // Little-endian: low byte first
            payload[FodId.LicenseIdOffset + 0] = 0x78;
            payload[FodId.LicenseIdOffset + 1] = 0x56;
            payload[FodId.LicenseIdOffset + 2] = 0x34;
            payload[FodId.LicenseIdOffset + 3] = 0x12;
            Array.Copy(CanonicalHash, 0, payload, FodId.HashOffset, FodId.HashLength);
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
        public void LicenseId_IsLittleEndian()
        {
            var payload = CanonicalPayload();
            // 0x01 0x00 0x00 0x00 little-endian -> 1
            payload[FodId.LicenseIdOffset + 0] = 0x01;
            payload[FodId.LicenseIdOffset + 1] = 0x00;
            payload[FodId.LicenseIdOffset + 2] = 0x00;
            payload[FodId.LicenseIdOffset + 3] = 0x00;

            var fodId = new FodId(SignedOwidBase64(payload));

            Assert.AreEqual(1u, fodId.LicenseId);
        }

        [TestMethod]
        public void LicenseId_MaxValue_IsLittleEndian()
        {
            var payload = CanonicalPayload();
            payload[FodId.LicenseIdOffset + 0] = 0xFF;
            payload[FodId.LicenseIdOffset + 1] = 0xFF;
            payload[FodId.LicenseIdOffset + 2] = 0xFF;
            payload[FodId.LicenseIdOffset + 3] = 0xFF;

            var fodId = new FodId(SignedOwidBase64(payload));

            Assert.AreEqual(uint.MaxValue, fodId.LicenseId);
        }

        [TestMethod]
        public void LicenseId_HighBitSet_StaysUnsigned()
        {
            var payload = CanonicalPayload();
            // 0x80000000 little-endian: 00 00 00 80
            payload[FodId.LicenseIdOffset + 0] = 0x00;
            payload[FodId.LicenseIdOffset + 1] = 0x00;
            payload[FodId.LicenseIdOffset + 2] = 0x00;
            payload[FodId.LicenseIdOffset + 3] = 0x80;

            var fodId = new FodId(SignedOwidBase64(payload));

            Assert.AreEqual(0x8000_0000u, fodId.LicenseId);
        }

        [TestMethod]
        public void Flags_ZeroValue_Exposed()
        {
            var payload = CanonicalPayload();
            payload[FodId.FlagsOffset] = 0x00;

            var fodId = new FodId(SignedOwidBase64(payload));

            Assert.AreEqual(0x00, fodId.Flags);
        }

        [TestMethod]
        public void Flags_AllBitsSet_Exposed()
        {
            var payload = CanonicalPayload();
            payload[FodId.FlagsOffset] = 0xFF;

            var fodId = new FodId(SignedOwidBase64(payload));

            Assert.AreEqual(0xFF, fodId.Flags);
        }

        [TestMethod]
        public void Hash_IsDefensiveCopy()
        {
            var fodId = new FodId(SignedOwidBase64(CanonicalPayload()));

            fodId.Hash[0] = 0x00;
            fodId.Hash[FodId.HashLength - 1] = 0x00;

            // The inherited Payload bytes must not have been mutated.
            Assert.AreEqual(CanonicalHash[0], fodId.Payload[FodId.HashOffset]);
            Assert.AreEqual(
                CanonicalHash[FodId.HashLength - 1],
                fodId.Payload[FodId.HashOffset + FodId.HashLength - 1]);
        }

        [TestMethod]
        public void Constructor_PayloadOneByteShort_Throws()
        {
            // 36 bytes — one short of the minimum 37.
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
        public void Constructor_PayloadLargerThanSpec_UsesFirst37Bytes()
        {
            // Build a 64-byte payload whose first 37 bytes match canonical;
            // remaining bytes are 0xCC and should be ignored.
            var payload = new byte[64];
            Array.Copy(CanonicalPayload(), payload, FodId.PayloadLength);
            for (int i = FodId.PayloadLength; i < payload.Length; i++)
            {
                payload[i] = 0xCC;
            }

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
