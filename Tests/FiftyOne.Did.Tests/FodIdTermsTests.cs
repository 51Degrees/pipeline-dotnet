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
    /// Tests for the Terms, being the byte after the match key that names
    /// the terms document a 51Did was created under. The layout is at
    /// https://github.com/51Degrees/specifications/blob/main/did-specification/identifier-layout.md#terms
    /// </summary>
    /// <remarks>
    /// Every payload here is built byte by byte from the canonical ones,
    /// with the Terms written as the first byte after the match key. No
    /// offset is named, because the tail begins exactly where the base
    /// payload ends, so the same code covers the 32-byte match key and
    /// the 16-byte one and a reader that moved the byte would fail.
    /// </remarks>
    [TestClass]
    public class FodIdTermsTests
    {
        /// <summary>
        /// The address index 1 stands for, written out rather than taken
        /// from the reader, because a test comparing the reader with
        /// itself would pass whatever the reader said.
        /// </summary>
        private const string ModelTermsForMarketing2Url =
            "https://m4ow.uk/mtm/2.txt";

        private FodIdTestFactory _factory = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _factory = new FodIdTestFactory();
        }

        /// <summary>
        /// The given payload with the given bytes appended, which is
        /// where the Terms and anything after it sit.
        /// </summary>
        private static byte[] WithTail(byte[] payload, params byte[] tail)
        {
            var extended = new byte[payload.Length + tail.Length];
            Array.Copy(payload, extended, payload.Length);
            Array.Copy(tail, 0, extended, payload.Length, tail.Length);
            return extended;
        }

        /// <summary>
        /// An identifier whose payload ends at the match key reads as
        /// terms that are not stated with no address. Nothing else about
        /// it changes.
        /// </summary>
        [TestMethod]
        public void Terms_PayloadEndingAtMatchKey_IsNotStatedWithNoAddress()
        {
            var fodId = new FodId(
                _factory.SignedOwidBase64(CanonicalPayload()));

            Assert.AreEqual(Terms.NotStated, fodId.Terms);
            Assert.AreEqual((byte)0, fodId.TermsIndex);
            Assert.IsNull(fodId.TermsUrl);
            Assert.AreEqual(CanonicalFlags, fodId.Flags);
            Assert.AreEqual(CanonicalLicenseId, fodId.LicenseId);
            CollectionAssert.AreEqual(CanonicalHash, fodId.MatchKey);
        }

        /// <summary>
        /// A Terms byte of zero says the same thing as no byte at all, so
        /// the two need never be told apart.
        /// </summary>
        [TestMethod]
        public void Terms_ExplicitZeroByte_ReadsAsAbsenceDoes()
        {
            var absent = new FodId(
                _factory.SignedOwidBase64(CanonicalPayload()));
            var zero = new FodId(_factory.SignedOwidBase64(
                WithTail(CanonicalPayload(), 0)));

            Assert.AreEqual(absent.Terms, zero.Terms);
            Assert.AreEqual(absent.TermsIndex, zero.TermsIndex);
            Assert.AreEqual(absent.TermsUrl, zero.TermsUrl);
            Assert.AreEqual(Terms.NotStated, zero.Terms);
            Assert.IsNull(zero.TermsUrl);
        }

        /// <summary>
        /// Index 1 is the Model Terms for Marketing, version 2, and the
        /// address is the versioned one. An address whose contents could
        /// be edited afterwards would not say what the identifier was
        /// created under.
        /// </summary>
        [TestMethod]
        public void Terms_IndexOne_IsTheModelTermsWithItsAddress()
        {
            var fodId = new FodId(_factory.SignedOwidBase64(
                WithTail(CanonicalPayload(), 1)));

            Assert.AreEqual(Terms.ModelTermsForMarketing2, fodId.Terms);
            Assert.AreEqual((byte)1, fodId.TermsIndex);
            Assert.AreEqual(ModelTermsForMarketing2Url, fodId.TermsUrl);
        }

        /// <summary>
        /// An index added to the table after this package was released is
        /// reported as the index it is, answers with no address, and is
        /// not read as zero.
        /// </summary>
        [TestMethod]
        public void Terms_IndexNotKnown_ReportsTheIndexWithNoAddress()
        {
            var fodId = new FodId(_factory.SignedOwidBase64(
                WithTail(CanonicalPayload(), 200)));

            Assert.AreEqual((byte)200, fodId.TermsIndex);
            Assert.AreEqual(Terms.Unknown, fodId.Terms);
            Assert.IsNull(fodId.TermsUrl);
            Assert.AreNotEqual(Terms.NotStated, fodId.Terms);
        }

        /// <summary>
        /// Zero and an index this package does not know are different
        /// answers. Zero says no terms are stated whilst an unknown index
        /// says terms are stated that this package cannot name, and a
        /// receiver confusing the two would read an identifier created
        /// under terms as one created under none.
        /// </summary>
        [TestMethod]
        public void Terms_NotStatedAndNotKnown_AreToldApart()
        {
            var notStated = new FodId(
                _factory.SignedOwidBase64(CanonicalPayload()));
            var notKnown = new FodId(_factory.SignedOwidBase64(
                WithTail(CanonicalPayload(), 200)));

            Assert.AreNotEqual(notStated.Terms, notKnown.Terms);
            Assert.AreNotEqual(notStated.TermsIndex, notKnown.TermsIndex);
            // Neither has an address, so the address alone cannot tell
            // them apart and the named value has to.
            Assert.IsNull(notStated.TermsUrl);
            Assert.IsNull(notKnown.TermsUrl);
        }

        /// <summary>
        /// A 51Did carrying a creator context has a section after the
        /// Terms, and the match key and the Terms are still read
        /// correctly. Reading one byte late here would give 0x99, being
        /// 153, which is an index this package does not know, so a wrong
        /// offset cannot pass.
        /// </summary>
        [TestMethod]
        public void Terms_FollowedByCreatorContext_IsStillReadCorrectly()
        {
            var tail = new byte[41];
            tail[0] = 1;
            for (var i = 1; i < tail.Length; i++)
            {
                tail[i] = 0x99;
            }

            var fodId = new FodId(_factory.SignedOwidBase64(
                WithTail(CanonicalPayload(), tail)));

            Assert.AreEqual((byte)1, fodId.TermsIndex);
            Assert.AreEqual(Terms.ModelTermsForMarketing2, fodId.Terms);
            Assert.AreEqual(ModelTermsForMarketing2Url, fodId.TermsUrl);
            Assert.AreEqual(FodId.MatchKeyLength, fodId.MatchKey.Length);
            CollectionAssert.AreEqual(CanonicalHash, fodId.MatchKey);
            Assert.AreEqual(CanonicalLicenseId, fodId.LicenseId);
        }

        /// <summary>
        /// The Terms sits after a 32-byte match key on a HashedEmail
        /// identifier.
        /// </summary>
        [TestMethod]
        [DataRow((byte)0, Terms.NotStated)]
        [DataRow((byte)1, Terms.ModelTermsForMarketing2)]
        [DataRow((byte)200, Terms.Unknown)]
        public void Terms_AfterAThirtyTwoByteMatchKey(
            byte index, Terms expected)
        {
            var fodId = new FodId(_factory.SignedOwidBase64(
                WithTail(CanonicalPayload(), index)));

            Assert.AreEqual(IdType.HashedEmail, fodId.Type);
            Assert.AreEqual(FodId.MatchKeyLength, fodId.MatchKey.Length);
            CollectionAssert.AreEqual(CanonicalHash, fodId.MatchKey);
            Assert.AreEqual(index, fodId.TermsIndex);
            Assert.AreEqual(expected, fodId.Terms);
        }

        /// <summary>
        /// The Terms sits after a 16-byte match key on a Random
        /// identifier, which is 16 bytes earlier in the payload. A reader
        /// with one fixed offset would fail here.
        /// </summary>
        [TestMethod]
        [DataRow((byte)0, Terms.NotStated)]
        [DataRow((byte)1, Terms.ModelTermsForMarketing2)]
        [DataRow((byte)200, Terms.Unknown)]
        public void Terms_AfterASixteenByteMatchKey(
            byte index, Terms expected)
        {
            var fodId = new FodId(_factory.SignedOwidBase64(
                WithTail(CanonicalRandomPayload(), index)));

            Assert.AreEqual(IdType.Random, fodId.Type);
            Assert.AreEqual(FodId.GuidLength, fodId.MatchKey.Length);
            Assert.AreEqual(index, fodId.TermsIndex);
            Assert.AreEqual(expected, fodId.Terms);
        }

        /// <summary>
        /// A Random identifier carrying the Terms keeps its GUID intact,
        /// so the byte is not taken from the end of the match key.
        /// </summary>
        [TestMethod]
        public void Terms_AfterASixteenByteMatchKey_LeavesTheGuidIntact()
        {
            var expected = new byte[FodId.GuidLength];
            for (var i = 0; i < expected.Length; i++)
            {
                expected[i] = (byte)(0x40 + i);
            }

            var fodId = new FodId(_factory.SignedOwidBase64(
                WithTail(CanonicalRandomPayload(), 1)));

            CollectionAssert.AreEqual(expected, fodId.MatchKey);
            Assert.AreEqual(Terms.ModelTermsForMarketing2, fodId.Terms);
        }

        /// <summary>
        /// A Reserved identifier takes everything after the header as its
        /// value, so no byte is left for the Terms and it reads as not
        /// stated. The match key length of that type is not defined, and
        /// an identifier of a type this package cannot lay out is one
        /// whose Terms it cannot place either.
        /// </summary>
        [TestMethod]
        public void Terms_ReservedType_IsNotStated()
        {
            var payload = new byte[FodId.MatchKeyOffset + 50];
            payload[FodId.FlagsOffset] = 0b1100_0000;
            payload[payload.Length - 1] = 1;

            var fodId = new FodId(_factory.SignedOwidBase64(payload));

            Assert.AreEqual(IdType.Reserved, fodId.Type);
            Assert.AreEqual(50, fodId.MatchKey.Length);
            Assert.AreEqual(Terms.NotStated, fodId.Terms);
            Assert.AreEqual((byte)0, fodId.TermsIndex);
            Assert.IsNull(fodId.TermsUrl);
        }

        /// <summary>
        /// The Terms survives the base64 round trip, since it is payload
        /// bytes and the payload is carried whole.
        /// </summary>
        [TestMethod]
        public void Terms_SurvivesABase64RoundTrip()
        {
            var first = new FodId(_factory.SignedOwidBase64(
                WithTail(CanonicalPayload(), 1)));

            var second = new FodId(first.AsBase64Url());

            Assert.AreEqual(first.TermsIndex, second.TermsIndex);
            Assert.AreEqual(first.Terms, second.Terms);
            Assert.AreEqual(first.TermsUrl, second.TermsUrl);
        }
    }
}
