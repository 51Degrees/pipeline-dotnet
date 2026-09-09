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
using System;
using static FiftyOne.Did.Tests.FodIdTestFactory;

namespace FiftyOne.Did.Tests
{
    /// <summary>
    /// Tests for the payload version, being bits 4 and 5 of the flags
    /// byte. This package reads version 0 and refuses every other version
    /// rather than reading its fields, because a later version exists
    /// precisely because a field moved, so reading one here would answer
    /// with values that are wrong rather than absent. The layout is at
    /// https://github.com/51Degrees/specifications/blob/main/did-specification/identifier-layout.md#version
    /// </summary>
    [TestClass]
    public class FodIdVersionTests
    {
        private FodIdTestFactory _factory = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _factory = new FodIdTestFactory();
        }

        /// <summary>
        /// The payload with its version bits set to the given version,
        /// leaving every other bit of the flags byte alone.
        /// </summary>
        private static byte[] WithVersion(byte[] payload, byte version)
        {
            var withVersion = (byte[])payload.Clone();
            withVersion[FodId.FlagsOffset] = (byte)(
                (payload[FodId.FlagsOffset] & 0b1100_1111)
                | (version << 4));
            return withVersion;
        }

        /// <summary>
        /// A flags byte with bits 4 and 5 clear is version 0, which is the
        /// layout this package reads, so every field reads as it does on
        /// the canonical payload.
        /// </summary>
        [TestMethod]
        public void Version_Zero_ReadsEveryField()
        {
            var fodId = new FodId(
                _factory.SignedOwidBase64(CanonicalPayload()));

            Assert.AreEqual(IdType.HashedEmail, fodId.Type);
            Assert.AreEqual(Usage.Personalized, fodId.Usage);
            Assert.AreEqual(CanonicalLicenseId, fodId.LicenseId);
            CollectionAssert.AreEqual(CanonicalHash, fodId.MatchKey);
            Assert.AreEqual("https://m4ow.uk/mtm/2.txt", fodId.Terms);
        }

        /// <summary>
        /// Versions 1, 2 and 3 are not assigned, so a payload naming one
        /// is refused rather than read under the layout this package
        /// knows.
        /// </summary>
        [TestMethod]
        [DataRow((byte)1)]
        [DataRow((byte)2)]
        [DataRow((byte)3)]
        public void Version_NotZero_IsRefused(byte version)
        {
            var refused = FodId.TryParse(
                _factory.SignedOwidBase64(
                    WithVersion(CanonicalPayload(), version)),
                out var fodId,
                out var status);

            Assert.IsFalse(refused);
            Assert.AreEqual(
                FodIdParseStatus.UnsupportedPayloadVersion, status);
            Assert.IsNull(fodId);
        }

        /// <summary>
        /// The throwing surface names the version it found, so whoever
        /// reads the message knows which layout the identifier claims
        /// rather than only that some version was refused.
        /// </summary>
        [TestMethod]
        [DataRow((byte)1)]
        [DataRow((byte)2)]
        [DataRow((byte)3)]
        public void Version_NotZero_MessageNamesTheVersion(byte version)
        {
            var thrown = Assert.ThrowsExactly<ArgumentException>(
                () => new FodId(_factory.SignedOwidBase64(
                    WithVersion(CanonicalPayload(), version))));

            StringAssert.Contains(
                thrown.Message, $"version {version}");
            StringAssert.Contains(
                thrown.Message, "UnsupportedPayloadVersion");
        }

        /// <summary>
        /// A refused identifier hands back nothing at all, rather than a
        /// value with some fields filled in. There is no identifier to
        /// expose fields for when the layout was not understood.
        /// </summary>
        [TestMethod]
        public void Version_NotZero_ExposesNoFields()
        {
            Assert.IsFalse(FodId.TryParse(
                _factory.SignedOwidBase64(
                    WithVersion(CanonicalPayload(), 1)),
                out var fodId,
                out _));

            Assert.IsNull(fodId);
        }

        /// <summary>
        /// The version bits are read on their own, so an identifier of
        /// version 0 still reads whatever its usage and type bits hold.
        /// A reader masking the wrong bits would refuse some of these.
        /// </summary>
        [TestMethod]
        [DataRow((byte)0b0000_0000, IdType.Probabilistic, Usage.None)]
        [DataRow((byte)0b0000_0001, IdType.Probabilistic, Usage.NonMarketing)]
        [DataRow((byte)0b0000_1011, IdType.Probabilistic, Usage.Standard)]
        [DataRow((byte)0b0100_0111, IdType.Random, Usage.Personalized)]
        [DataRow((byte)0b1000_0101, IdType.HashedEmail, Usage.Personalized)]
        [DataRow((byte)0b1100_0011, IdType.Reserved, Usage.Standard)]
        public void Version_Zero_ReadsWithAnyUsageAndType(
            byte flags, IdType expectedType, Usage expectedUsage)
        {
            var payload = expectedType == IdType.Random
                ? RandomPayloadEndingAtMatchKey()
                : PayloadEndingAtMatchKey();
            payload[FodId.FlagsOffset] = flags;

            var fodId = new FodId(_factory.SignedOwidBase64(payload));

            Assert.AreEqual(expectedType, fodId.Type);
            Assert.AreEqual(expectedUsage, fodId.Usage);
        }

        /// <summary>
        /// The version is refused whatever the usage and type bits say, so
        /// no combination of the other bits lets a payload of a version
        /// this package does not know through.
        /// </summary>
        [TestMethod]
        [DataRow((byte)0b0001_0000)]
        [DataRow((byte)0b0010_0111)]
        [DataRow((byte)0b0111_0001)]
        [DataRow((byte)0b1011_0101)]
        [DataRow((byte)0b1101_0011)]
        public void Version_NotZero_IsRefusedWhateverTheOtherBits(byte flags)
        {
            var payload = PayloadEndingAtMatchKey();
            payload[FodId.FlagsOffset] = flags;

            Assert.IsFalse(FodId.TryParse(
                _factory.SignedOwidBase64(payload),
                out var fodId,
                out var status));

            Assert.AreEqual(
                FodIdParseStatus.UnsupportedPayloadVersion, status);
            Assert.IsNull(fodId);
        }
    }
}
