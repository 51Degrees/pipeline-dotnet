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
using FiftyOne.Did.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static FiftyOne.Did.Tests.FodIdTestFactory;

namespace FiftyOne.Did.Tests
{
    /// <summary>
    /// Tests for the Terms, being the byte after the match key that names
    /// the terms document a 51Did was created under. The package turns
    /// that index into an address, so <see cref="FodId.Terms"/> answers
    /// with the address and a caller never handles the byte. The layout is
    /// at
    /// https://github.com/51Degrees/specifications/blob/main/did-specification/identifier-layout.md#terms
    /// </summary>
    /// <remarks>
    /// Every payload here is built byte by byte from the canonical ones,
    /// with the Terms written as the first byte after the match key. No
    /// offset is named, because the tail begins exactly where the payload
    /// that ends at the match key ends, so the same code covers the
    /// 32-byte match key and the 16-byte one and a reader that moved the
    /// byte would fail.
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
        /// The terms table is the one place the package says which index is
        /// which document, so these check the table itself rather than only
        /// its effect through a parsed identifier. Adding a document is a
        /// row here and a member of the enumeration, and if a later change
        /// puts the address somewhere else as well then two places can
        /// disagree, which these would not catch. They exist so that the
        /// table stays the definition.
        /// </summary>
        [TestMethod]
        public void TermsTable_IndexOne_IsTheModelTermsForMarketing2()
        {
            Assert.AreEqual(
                Terms.ModelTermsForMarketing2,
                TermsTable.Named(1));
            Assert.AreEqual(
                ModelTermsForMarketing2Url, TermsTable.AddressFor(1));
        }

        /// <summary>
        /// Zero is not stated in the identifier, which is a different
        /// answer from an index the package cannot name even though both
        /// give no address.
        /// </summary>
        [TestMethod]
        public void TermsTable_Zero_IsNotStatedAndNotUnknown()
        {
            Assert.AreEqual(Terms.NotStated, TermsTable.Named(0));
            Assert.AreNotEqual(Terms.Unknown, TermsTable.Named(0));
            Assert.IsNull(TermsTable.AddressFor(0));
            Assert.AreEqual(0, TermsTable.NotStatedIndex);
        }

        /// <summary>
        /// Every index the table does not carry is Unknown and answers with
        /// no address. This is the rule the specification warns hardest
        /// about, because reading such an index as NotStated would take an
        /// identifier created under terms for one created under none.
        /// </summary>
        [TestMethod]
        [DataRow((byte)2)]
        [DataRow((byte)3)]
        [DataRow((byte)127)]
        [DataRow((byte)128)]
        [DataRow((byte)200)]
        [DataRow((byte)255)]
        public void TermsTable_IndexNotInTheTable_IsUnknownWithNoAddress(
            byte index)
        {
            Assert.AreEqual(Terms.Unknown, TermsTable.Named(index));
            Assert.IsNull(TermsTable.AddressFor(index));
        }

        /// <summary>
        /// Every member of the enumeration is reachable from the table, so
        /// a member added without a row, or a row without a member, is
        /// caught here rather than by a caller reading no address for a
        /// document the package is supposed to know.
        /// </summary>
        [TestMethod]
        public void TermsTable_EveryNamedDocument_HasAnAddress()
        {
            foreach (Terms terms in Enum.GetValues(typeof(Terms)))
            {
                if (terms == Terms.NotStated
                    || terms == Terms.Unknown)
                {
                    // Neither names a document, so neither has an address.
                    Assert.IsNull(TermsTable.AddressFor((byte)terms));
                    continue;
                }
                var address = TermsTable.AddressFor((byte)terms);
                Assert.IsNotNull(
                    address,
                    $"{terms} names a document with no address in the "
                    + "table.");
                Assert.AreEqual(
                    terms,
                    TermsTable.Named((byte)terms),
                    $"{terms} does not read back from its own index.");
                StringAssert.StartsWith(address, "https://");
            }
        }

        /// <summary>
        /// An identifier whose payload ends at the match key carries no
        /// Terms byte and answers with no address. Nothing else about it
        /// changes.
        /// </summary>
        [TestMethod]
        public void Terms_PayloadEndingAtMatchKey_HasNoAddress()
        {
            var fodId = new FodId(
                _factory.SignedOwidBase64(PayloadEndingAtMatchKey()));

            Assert.IsNull(fodId.Terms);
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
                _factory.SignedOwidBase64(PayloadEndingAtMatchKey()));
            var zero = new FodId(_factory.SignedOwidBase64(
                WithTerms(PayloadEndingAtMatchKey(), 0)));

            Assert.AreEqual(absent.Terms, zero.Terms);
            Assert.IsNull(zero.Terms);
        }

        /// <summary>
        /// Index 1 is the Model Terms for Marketing, version 2, and the
        /// address is the versioned one. An address whose contents could
        /// be edited afterwards would not say what the identifier was
        /// created under.
        /// </summary>
        [TestMethod]
        public void Terms_IndexOne_IsTheModelTermsAddress()
        {
            var fodId = new FodId(_factory.SignedOwidBase64(
                WithTerms(PayloadEndingAtMatchKey(), 1)));

            Assert.AreEqual(ModelTermsForMarketing2Url, fodId.Terms);
        }

        /// <summary>
        /// An index added to the table after this package was released
        /// answers with no address. No package may build an address from
        /// an index it does not know, because that would name a document
        /// nobody wrote and a receiver would record having accepted terms
        /// that do not exist.
        /// </summary>
        [TestMethod]
        [DataRow((byte)2)]
        [DataRow((byte)127)]
        [DataRow((byte)200)]
        [DataRow((byte)255)]
        public void Terms_IndexNotKnown_HasNoAddress(byte index)
        {
            var fodId = new FodId(_factory.SignedOwidBase64(
                WithTerms(PayloadEndingAtMatchKey(), index)));

            Assert.IsNull(fodId.Terms);
        }

        /// <summary>
        /// Zero and an index this package does not know give a caller the
        /// same answer, which is deliberate, because both say the
        /// identifier does not tell the caller which terms it was created
        /// under and the answer has to come from somewhere else.
        /// </summary>
        [TestMethod]
        public void Terms_NotStatedAndNotKnown_BothAnswerWithNoAddress()
        {
            var notStated = new FodId(_factory.SignedOwidBase64(
                WithTerms(PayloadEndingAtMatchKey(), 0)));
            var notKnown = new FodId(_factory.SignedOwidBase64(
                WithTerms(PayloadEndingAtMatchKey(), 200)));

            Assert.IsNull(notStated.Terms);
            Assert.IsNull(notKnown.Terms);
        }

        /// <summary>
        /// A 51Did carrying a creator context has a section after the
        /// Terms, and the match key and the Terms are still read
        /// correctly. Reading one byte late here would give 0x99, being
        /// 153, which is an index this package does not know and answers
        /// with no address, so a wrong offset cannot pass.
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
                WithTerms(PayloadEndingAtMatchKey(), tail)));

            Assert.AreEqual(ModelTermsForMarketing2Url, fodId.Terms);
            Assert.AreEqual(FodId.MatchKeyLength, fodId.MatchKey.Length);
            CollectionAssert.AreEqual(CanonicalHash, fodId.MatchKey);
            Assert.AreEqual(CanonicalLicenseId, fodId.LicenseId);
        }

        /// <summary>
        /// The Terms sits after a 32-byte match key on a HashedEmail
        /// identifier.
        /// </summary>
        [TestMethod]
        [DataRow((byte)0, null)]
        [DataRow((byte)1, ModelTermsForMarketing2Url)]
        [DataRow((byte)200, null)]
        public void Terms_AfterAThirtyTwoByteMatchKey(
            byte index, string? expected)
        {
            var fodId = new FodId(_factory.SignedOwidBase64(
                WithTerms(PayloadEndingAtMatchKey(), index)));

            Assert.AreEqual(IdType.HashedEmail, fodId.Type);
            Assert.AreEqual(FodId.MatchKeyLength, fodId.MatchKey.Length);
            CollectionAssert.AreEqual(CanonicalHash, fodId.MatchKey);
            Assert.AreEqual(expected, fodId.Terms);
        }

        /// <summary>
        /// The Terms sits after a 16-byte match key on a Random
        /// identifier, which is 16 bytes earlier in the payload. A reader
        /// with one fixed offset would fail here.
        /// </summary>
        [TestMethod]
        [DataRow((byte)0, null)]
        [DataRow((byte)1, ModelTermsForMarketing2Url)]
        [DataRow((byte)200, null)]
        public void Terms_AfterASixteenByteMatchKey(
            byte index, string? expected)
        {
            var fodId = new FodId(_factory.SignedOwidBase64(
                WithTerms(RandomPayloadEndingAtMatchKey(), index)));

            Assert.AreEqual(IdType.Random, fodId.Type);
            Assert.AreEqual(FodId.GuidLength, fodId.MatchKey.Length);
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
                WithTerms(RandomPayloadEndingAtMatchKey(), 1)));

            CollectionAssert.AreEqual(expected, fodId.MatchKey);
            Assert.AreEqual(ModelTermsForMarketing2Url, fodId.Terms);
        }

        /// <summary>
        /// A Reserved identifier takes everything after the header as its
        /// value, so no byte is left for the Terms and it answers with no
        /// address. The match key length of that type is not defined, and
        /// an identifier of a type this package cannot lay out is one
        /// whose Terms it cannot place either.
        /// </summary>
        [TestMethod]
        public void Terms_ReservedType_HasNoAddress()
        {
            var payload = new byte[FodId.MatchKeyOffset + 50];
            payload[FodId.FlagsOffset] = 0b1100_0000;
            payload[payload.Length - 1] = 1;

            var fodId = new FodId(_factory.SignedOwidBase64(payload));

            Assert.AreEqual(IdType.Reserved, fodId.Type);
            Assert.AreEqual(50, fodId.MatchKey.Length);
            Assert.IsNull(fodId.Terms);
        }

        /// <summary>
        /// The Terms survives the base64 round trip, since it is payload
        /// bytes and the payload is carried whole.
        /// </summary>
        [TestMethod]
        public void Terms_SurvivesABase64RoundTrip()
        {
            var first = new FodId(_factory.SignedOwidBase64(
                WithTerms(PayloadEndingAtMatchKey(), 1)));

            var second = new FodId(first.AsBase64Url());

            Assert.AreEqual(first.Terms, second.Terms);
            Assert.AreEqual(ModelTermsForMarketing2Url, second.Terms);
        }
    }
}
