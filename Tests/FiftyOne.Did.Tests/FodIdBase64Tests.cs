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
using System;
using System.Linq;
using static FiftyOne.Did.Tests.FodIdTestFactory;

namespace FiftyOne.Did.Tests
{
    /// <summary>
    /// Tests for the two base64 alphabets <see cref="FodId"/> accepts and
    /// produces, and for <see cref="FodId.DateMinutes"/>.
    /// </summary>
    [TestClass]
    public class FodIdBase64Tests
    {
        private FodIdTestFactory _factory = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _factory = new FodIdTestFactory();
        }

        /// <summary>
        /// A signed envelope whose standard base64 contains both characters
        /// that differ between the alphabets, so the conversion is actually
        /// exercised. The signature is random, so an envelope is signed
        /// until one qualifies, which takes one or two tries.
        /// </summary>
        private string StandardWithBothSpecials()
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                var base64 = _factory.SignedOwidBase64(CanonicalPayload());
                if (base64.Contains('+') && base64.Contains('/'))
                {
                    return base64;
                }
            }
            throw new InvalidOperationException(
                "No signed envelope contained both + and / in 100 tries.");
        }

        [TestMethod]
        public void FromBase64_AcceptsStandardUrlSafeAndUnpadded()
        {
            var standard = StandardWithBothSpecials();
            // The canonical envelope is 124 bytes, so the standard form ends
            // in two padding characters and the unpadded form differs.
            Assert.IsTrue(standard.EndsWith("==", StringComparison.Ordinal));
            var urlSafePadded = standard.Replace('+', '-').Replace('/', '_');
            var urlSafe = urlSafePadded.TrimEnd('=');

            var fromStandard = FodId.FromBase64(standard);
            var fromUrlSafePadded = FodId.FromBase64(urlSafePadded);
            var fromUrlSafe = FodId.FromBase64(urlSafe);

            Assert.AreEqual(standard, fromStandard.AsBase64());
            Assert.AreEqual(standard, fromUrlSafePadded.AsBase64());
            Assert.AreEqual(standard, fromUrlSafe.AsBase64());
            CollectionAssert.AreEqual(CanonicalHash, fromUrlSafe.Hash);
            Assert.AreEqual(CanonicalLicenseId, fromUrlSafe.LicenseId);
        }

        [TestMethod]
        public void Constructor_And_As51Did_AcceptUrlSafe()
        {
            var urlSafe = FodId.ToBase64Url(StandardWithBothSpecials());

            Assert.AreEqual(
                CanonicalLicenseId, new FodId(urlSafe).LicenseId);
            Assert.AreEqual(
                CanonicalLicenseId, urlSafe.As51Did().LicenseId);
        }

        [TestMethod]
        public void AsBase64Url_RoundTrips()
        {
            var fodId = FodId.FromBase64(StandardWithBothSpecials());

            var url = fodId.AsBase64Url();

            Assert.IsFalse(url.Contains('+'));
            Assert.IsFalse(url.Contains('/'));
            Assert.IsFalse(url.Contains('='));
            Assert.AreEqual(fodId.AsBase64(), FodId.FromBase64(url).AsBase64());
            Assert.AreEqual(url, FodId.FromBase64(url).AsBase64Url());
        }

        [TestMethod]
        [DataRow("YQ", "YQ==")]
        [DataRow("YWI", "YWI=")]
        [DataRow("YWJj", "YWJj")]
        [DataRow("-_", "+/==")]
        [DataRow("+/==", "+/==")]
        [DataRow("", "")]
        public void NormaliseBase64_RestoresAlphabetAndPadding(
            string input, string expected)
        {
            Assert.AreEqual(expected, FodId.NormaliseBase64(input));
        }

        [TestMethod]
        public void NormaliseBase64_NullGivesNull()
        {
            Assert.IsNull(FodId.NormaliseBase64(null));
        }

        [TestMethod]
        public void ToBase64Url_ConvertsAndStripsPadding()
        {
            Assert.AreEqual("-_", FodId.ToBase64Url("+/=="));
            Assert.AreEqual("YWJj", FodId.ToBase64Url("YWJj"));
            Assert.ThrowsExactly<ArgumentNullException>(
                () => FodId.ToBase64Url(null!));
        }

        [TestMethod]
        public void FromBase64_InvalidInEitherAlphabet_Throws()
        {
            Assert.ThrowsExactly<FormatException>(
                () => FodId.FromBase64("not base64 in any alphabet!"));
        }

        [TestMethod]
        public void DateMinutes_EqualsTheEnvelopeDateField()
        {
            const uint minutes = 3_456_789u;
            var owid = _factory.SignedOwid(
                CanonicalPayload(), FodId.DateBase.AddMinutes(minutes));

            var fodId = FodId.FromBase64(owid.AsBase64());

            Assert.AreEqual(minutes, fodId.DateMinutes);
            // The same value read straight off the wire: after the version
            // byte and the zero-terminated domain comes the little-endian
            // 32-bit minute count.
            var bytes = owid.AsByteArray();
            var dateOffset = 1 + bytes.Skip(1).ToList().IndexOf(0) + 1;
            var onTheWire = BitConverter.ToUInt32(bytes, dateOffset);
            Assert.AreEqual(onTheWire, fodId.DateMinutes);
        }

        [TestMethod]
        public void DateMinutes_DateBeforeBase_IsZero()
        {
            var fodId = new FodId(new Owid.Client.Model.Owid
            {
                Date = FodId.DateBase.AddMinutes(-5),
                Payload = CanonicalPayload(),
            });

            Assert.AreEqual(0u, fodId.DateMinutes);
        }

        [TestMethod]
        public void DateMinutes_SubMinuteIsTruncated()
        {
            var fodId = new FodId(new Owid.Client.Model.Owid
            {
                Date = FodId.DateBase.AddMinutes(10).AddSeconds(59),
                Payload = CanonicalPayload(),
            });

            Assert.AreEqual(10u, fodId.DateMinutes);
        }
    }
}
