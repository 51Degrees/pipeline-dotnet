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
using static FiftyOne.Did.Tests.FodIdTestFactory;

namespace FiftyOne.Did.Tests
{
    /// <summary>
    /// Tests for <see cref="FodId"/>.
    /// </summary>
    [TestClass]
    public class FodIdTests
    {
        private FodIdTestFactory _factory = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _factory = new FodIdTestFactory();
        }

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
            var fodId = new FodId(_factory.SignedOwidBase64(CanonicalPayload()));

            Assert.IsInstanceOfType<Owid.Client.Model.Owid>(fodId);
        }

        [TestMethod]
        public void Constructor_FromBase64_UnpacksAllThreeFields()
        {
            var base64 = _factory.SignedOwidBase64(CanonicalPayload());

            var fodId = new FodId(base64);

            Assert.AreEqual(CanonicalFlags, fodId.Flags);
            Assert.AreEqual(CanonicalLicenseId, fodId.LicenseId);
            CollectionAssert.AreEqual(CanonicalHash, fodId.MatchKey);
            Assert.AreEqual(TestDomain, fodId.Domain);
        }

        [TestMethod]
        public void ObsoleteHash_ReturnsMatchKey()
        {
            var fodId = new FodId(_factory.SignedOwidBase64(CanonicalPayload()));

#pragma warning disable CS0618 // deliberately exercising the obsolete alias
            CollectionAssert.AreEqual(fodId.MatchKey, fodId.Hash);
#pragma warning restore CS0618
        }

        [TestMethod]
        public void Constructor_FromBytes_UnpacksAllThreeFields()
        {
            var base64 = _factory.SignedOwidBase64(CanonicalPayload());
            var bytes = Convert.FromBase64String(base64);

            var fodId = new FodId(bytes);

            Assert.AreEqual(CanonicalFlags, fodId.Flags);
            Assert.AreEqual(CanonicalLicenseId, fodId.LicenseId);
            CollectionAssert.AreEqual(CanonicalHash, fodId.MatchKey);
            Assert.AreEqual(TestDomain, fodId.Domain);
        }

        [TestMethod]
        public void Constructor_FromOwid_UnpacksAllThreeFields()
        {
            var owid = _factory.SignedOwid(CanonicalPayload());

            var fodId = new FodId(owid);

            Assert.AreEqual(CanonicalFlags, fodId.Flags);
            Assert.AreEqual(CanonicalLicenseId, fodId.LicenseId);
            CollectionAssert.AreEqual(CanonicalHash, fodId.MatchKey);
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

            var fodId = new FodId(_factory.SignedOwidBase64(payload));

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

            var fodId = new FodId(_factory.SignedOwidBase64(payload));

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

            var fodId = new FodId(_factory.SignedOwidBase64(payload));

            Assert.AreEqual(0x8000_0000u, fodId.LicenseId);
        }

        [TestMethod]
        public void Flags_ZeroValue_Exposed()
        {
            var payload = CanonicalPayload();
            payload[FodId.FlagsOffset] = 0x00;

            var fodId = new FodId(_factory.SignedOwidBase64(payload));

            Assert.AreEqual(0x00, fodId.Flags);
        }

        [TestMethod]
        public void Flags_AllBitsSet_Exposed()
        {
            var payload = CanonicalPayload();
            payload[FodId.FlagsOffset] = 0xFF;

            var fodId = new FodId(_factory.SignedOwidBase64(payload));

            Assert.AreEqual(0xFF, fodId.Flags);
        }

        [TestMethod]
        public void Hash_IsDefensiveCopy()
        {
            var fodId = new FodId(_factory.SignedOwidBase64(CanonicalPayload()));

            fodId.MatchKey[0] = 0x00;
            fodId.MatchKey[FodId.HashLength - 1] = 0x00;

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
            var base64 = _factory.SignedOwidBase64(new byte[FodId.PayloadLength - 1]);

            Assert.ThrowsExactly<ArgumentException>(() => new FodId(base64));
        }

        [TestMethod]
        public void Constructor_PayloadEmpty_Throws()
        {
            var base64 = _factory.SignedOwidBase64(Array.Empty<byte>());

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

            var fodId = new FodId(_factory.SignedOwidBase64(payload));

            Assert.AreEqual(CanonicalFlags, fodId.Flags);
            Assert.AreEqual(CanonicalLicenseId, fodId.LicenseId);
            CollectionAssert.AreEqual(CanonicalHash, fodId.MatchKey);
            Assert.AreEqual(FodId.HashLength, fodId.MatchKey.Length);
        }

        [TestMethod]
        public async Task FodId_IsCryptographicallyVerifiable()
        {
            var fodId = new FodId(_factory.SignedOwidBase64(CanonicalPayload()));

            using var verifyKey = ECDsa.Create();
            verifyKey.ImportFromPem(_factory.PublicPem);
            Assert.IsTrue(await fodId.VerifyAsync(verifyKey));
        }

        [TestMethod]
        public void Base64Roundtrip_PreservesAllFields()
        {
            var fodId1 = new FodId(_factory.SignedOwidBase64(CanonicalPayload()));
            var fodId2 = new FodId(fodId1.AsBase64());

            Assert.AreEqual(fodId1.Flags, fodId2.Flags);
            Assert.AreEqual(fodId1.LicenseId, fodId2.LicenseId);
            CollectionAssert.AreEqual(fodId1.MatchKey, fodId2.MatchKey);
            Assert.AreEqual(fodId1.Domain, fodId2.Domain);
        }

        [TestMethod]
        [DataRow((byte)0b0000_0101, IdType.Probabilistic)]
        [DataRow((byte)0b1000_0101, IdType.HashedEmail)]
        [DataRow((byte)0b1100_0101, IdType.Reserved)]
        public void Type_DecodedFromTopTwoFlagBits(byte flags, IdType expected)
        {
            var payload = CanonicalPayload();
            payload[FodId.FlagsOffset] = flags;

            var fodId = new FodId(_factory.SignedOwidBase64(payload));

            Assert.AreEqual(expected, fodId.Type);
        }

        [TestMethod]
        public void Type_RandomWhenBits01()
        {
            var fodId = new FodId(_factory.SignedOwidBase64(CanonicalRandomPayload()));

            Assert.AreEqual(IdType.Random, fodId.Type);
        }

        [TestMethod]
        public void Constructor_RandomPayload21Bytes_Parses()
        {
            var fodId = new FodId(_factory.SignedOwidBase64(CanonicalRandomPayload()));

            Assert.AreEqual(CanonicalLicenseId, fodId.LicenseId);
            Assert.AreEqual(FodId.GuidLength, fodId.MatchKey.Length);
            CollectionAssert.AreEqual(
                Enumerable.Range(0x40, FodId.GuidLength)
                    .Select(i => (byte)i).ToArray(),
                fodId.MatchKey);
        }

        [TestMethod]
        public void Constructor_RandomPayloadOneByteShort_Throws()
        {
            var payload = CanonicalRandomPayload()
                .Take(FodId.RandomPayloadLength - 1).ToArray();

            Assert.ThrowsExactly<ArgumentException>(
                () => new FodId(_factory.SignedOwidBase64(payload)));
        }

        [TestMethod]
        public void Constructor_RandomPayloadLargerThanSpec_UsesFirst16ValueBytes()
        {
            var payload = new byte[FodId.PayloadLength];
            Array.Copy(
                CanonicalRandomPayload(), payload, FodId.RandomPayloadLength);
            for (int i = FodId.RandomPayloadLength; i < payload.Length; i++)
            {
                payload[i] = 0xCC;
            }

            var fodId = new FodId(_factory.SignedOwidBase64(payload));

            Assert.AreEqual(IdType.Random, fodId.Type);
            Assert.AreEqual(FodId.GuidLength, fodId.MatchKey.Length);
        }

        [TestMethod]
        public void Constructor_HemPayloadOneByteShort_Throws()
        {
            // CanonicalFlags (0xA5) carries the HashedEmail tag in bits 6-7,
            // so the 37-byte minimum still applies to this payload.
            var payload = CanonicalPayload()
                .Take(FodId.PayloadLength - 1).ToArray();

            Assert.ThrowsExactly<ArgumentException>(
                () => new FodId(_factory.SignedOwidBase64(payload)));
        }

        [TestMethod]
        public void Constructor_ReservedHeaderOnly_Parses()
        {
            var payload = new byte[FodId.HashOffset];
            payload[FodId.FlagsOffset] = 0b1100_0000;

            var fodId = new FodId(_factory.SignedOwidBase64(payload));

            Assert.AreEqual(IdType.Reserved, fodId.Type);
            Assert.AreEqual(0, fodId.MatchKey.Length);
        }

        [TestMethod]
        public void Constants_RandomLength_IsInternallyConsistent()
        {
#pragma warning disable MSTEST0032
            Assert.AreEqual(
                FodId.HashOffset + FodId.GuidLength,
                FodId.RandomPayloadLength);
#pragma warning restore MSTEST0032
        }

        // ----------------------------------------------------------------
        // Additional coverage for the reader's semantic guarantees, which the
        // cases above do not exercise: comparison by value, construction
        // without verification, a failing verification, and a bytes-first
        // round trip.
        // ----------------------------------------------------------------

        /// <summary>
        /// Two 51Dids issued for the same payload carry the same probabilistic
        /// value (Hash) even though their envelopes differ. This is the whole
        /// reason the reader exists, so compare hashes, never identifiers.
        /// </summary>
        [TestMethod]
        public void SamePayload_SameHash_DifferentEnvelope()
        {
            var a = new FodId(_factory.SignedOwidBase64(CanonicalPayload()));
            var b = new FodId(_factory.SignedOwidBase64(CanonicalPayload()));

            // The probabilistic value is stable across reissues.
            CollectionAssert.AreEqual(a.MatchKey, b.MatchKey);
            // The wrapping envelope is not (the signature is regenerated).
            Assert.IsFalse(a.Signature.SequenceEqual(b.Signature));
            Assert.AreNotEqual(a.AsBase64(), b.AsBase64());
            // The domain is part of the envelope but is stable here.
            Assert.AreEqual(a.Domain, b.Domain);
        }

        /// <summary>
        /// Constructing a <see cref="FodId"/> does not verify the signature.
        /// An unsigned OWID still constructs and exposes all three fields, so a
        /// later "verify on construction" change would be caught here.
        /// </summary>
        [TestMethod]
        public void Construction_DoesNotVerifySignature()
        {
            var unsigned = new Owid.Client.Model.Owid
            {
                Date = DateTime.UtcNow,
                Payload = CanonicalPayload(),
            };

            var fodId = new FodId(unsigned);

            Assert.AreEqual(CanonicalFlags, fodId.Flags);
            Assert.AreEqual(CanonicalLicenseId, fodId.LicenseId);
            CollectionAssert.AreEqual(CanonicalHash, fodId.MatchKey);
        }

        /// <summary>
        /// Verifying a 51Did against the wrong public key returns false rather
        /// than throwing. The existing verify test only covers the happy path.
        /// </summary>
        [TestMethod]
        public async Task Verify_WithWrongKey_ReturnsFalse()
        {
            var fodId = new FodId(_factory.SignedOwidBase64(CanonicalPayload()));

            // A freshly generated key that did not sign this 51Did.
            using var wrongKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            Assert.IsFalse(await fodId.VerifyAsync(wrongKey));
        }

        /// <summary>
        /// A 51Did parsed from raw bytes round-trips through base64 with all
        /// fields preserved. The other round-trip test starts from base64.
        /// </summary>
        [TestMethod]
        public void BytesConstructor_RoundTripsThroughBase64()
        {
            var bytes = Convert.FromBase64String(
                _factory.SignedOwidBase64(CanonicalPayload()));

            var fromBytes = new FodId(bytes);
            var roundTripped = new FodId(fromBytes.AsBase64());

            Assert.AreEqual(CanonicalFlags, roundTripped.Flags);
            Assert.AreEqual(CanonicalLicenseId, roundTripped.LicenseId);
            CollectionAssert.AreEqual(CanonicalHash, roundTripped.MatchKey);
        }
    }
}
