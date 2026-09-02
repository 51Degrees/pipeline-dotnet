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
using static FiftyOne.Did.Tests.FodIdTestFactory;

namespace FiftyOne.Did.Tests
{
    /// <summary>
    /// Tests for the non-throwing parse,
    /// <see cref="FodId.TryParse(string, out FodId, out FodIdParseStatus)"/>
    /// and its byte overload, and for the throwing surface built on the
    /// same walk. Every outcome is checked for the three facts a result
    /// carries, being whether the parse succeeded, whether a value came
    /// back, and the status.
    /// </summary>
    [TestClass]
    public class FodIdTryParseTests
    {
        private FodIdTestFactory _factory = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _factory = new FodIdTestFactory();
        }

        private static void AssertParsed(
            bool ok,
            FodId? fodId,
            FodIdParseStatus status)
        {
            Assert.IsTrue(ok, "the parse should have succeeded");
            Assert.IsNotNull(fodId, "a successful parse hands back a value");
            Assert.AreEqual(FodIdParseStatus.Parsed, status);
        }

        private static void AssertRefused(
            bool ok,
            FodId? fodId,
            FodIdParseStatus status,
            FodIdParseStatus expected)
        {
            Assert.IsFalse(ok, "the parse should have failed");
            Assert.IsNull(fodId, "a failed parse never hands back a value");
            Assert.AreEqual(expected, status);
        }

        private static byte[] PayloadOfType(IdType type, int length)
        {
            var payload = new byte[length];
            if (length > 0)
            {
                payload[FodId.FlagsOffset] = (byte)((byte)type << 6 | 0b101);
            }
            return payload;
        }

        // ----------------------------------------------------------------
        // Success, both surfaces and both alphabets
        // ----------------------------------------------------------------

        [TestMethod]
        public void TryParse_String_ValidIdentifier_Parses()
        {
            var ok = FodId.TryParse(
                _factory.SignedOwidBase64(CanonicalPayload()),
                out var fodId,
                out var status);

            AssertParsed(ok, fodId, status);
            Assert.AreEqual(CanonicalFlags, fodId!.Flags);
            Assert.AreEqual(CanonicalLicenseId, fodId.LicenseId);
            CollectionAssert.AreEqual(CanonicalHash, fodId.MatchKey);
            Assert.AreEqual(TestDomain, fodId.Domain);
        }

        [TestMethod]
        public void TryParse_Bytes_ValidIdentifier_Parses()
        {
            var ok = FodId.TryParse(
                _factory.SignedBytes(CanonicalPayload(), DateTime.UtcNow),
                out var fodId,
                out var status);

            AssertParsed(ok, fodId, status);
            CollectionAssert.AreEqual(CanonicalHash, fodId!.MatchKey);
        }

        [TestMethod]
        public void TryParse_UrlSafeAlphabet_Parses()
        {
            var standard = _factory.SignedOwidBase64(CanonicalPayload());

            var ok = FodId.TryParse(
                FodId.ToBase64Url(standard), out var fodId, out var status);

            AssertParsed(ok, fodId, status);
            Assert.AreEqual(standard, fodId!.AsBase64());
        }

        [TestMethod]
        public void TryParse_LongCreatorDomain_Parses()
        {
            // The creator domain is a deployment parameter, so a
            // self-hosted container may sign with a domain far longer than
            // the one the public cloud uses.
            const string domain =
                "a-very-long-self-hosted-creator-domain.internal.example.com";

            var ok = FodId.TryParse(
                _factory.SignedBytes(CanonicalPayload(), DateTime.UtcNow, domain: domain),
                out var fodId,
                out var status);

            AssertParsed(ok, fodId, status);
            Assert.AreEqual(domain, fodId!.Domain);
            CollectionAssert.AreEqual(CanonicalHash, fodId.MatchKey);
        }

        [TestMethod]
        public void TryParse_LongerContextSection_Parses()
        {
            // A creator context section follows the match key. Its length is
            // the cloud's business, so an older reader accepts it at any
            // length and still exposes the same three fields.
            var payload = new byte[FodId.PayloadLength + 300];
            CanonicalPayload().CopyTo(payload, 0);
            for (var i = FodId.PayloadLength; i < payload.Length; i++)
            {
                payload[i] = 0xCC;
            }

            var ok = FodId.TryParse(
                _factory.SignedOwidBase64(payload), out var fodId, out var status);

            AssertParsed(ok, fodId, status);
            Assert.AreEqual(CanonicalFlags, fodId!.Flags);
            Assert.AreEqual(CanonicalLicenseId, fodId.LicenseId);
            CollectionAssert.AreEqual(CanonicalHash, fodId.MatchKey);
            Assert.AreEqual(payload.Length, fodId.Payload.Length);
        }

        [TestMethod]
        [DataRow(IdType.Random, FodId.RandomPayloadLength + 1)]
        [DataRow(IdType.Random, FodId.PayloadLength)]
        [DataRow(IdType.Probabilistic, FodId.PayloadLength + 1)]
        [DataRow(IdType.HashedEmail, FodId.PayloadLength + 4000)]
        [DataRow(IdType.Reserved, FodId.HeaderLength + 9000)]
        public void TryParse_LongerPayload_IsNotRejectedForItsLength(
            IdType type,
            int length)
        {
            var ok = FodId.TryParse(
                _factory.SignedOwidBase64(PayloadOfType(type, length)),
                out var fodId,
                out var status);

            AssertParsed(ok, fodId, status);
            Assert.AreEqual(type, fodId!.Type);
            Assert.AreEqual(length, fodId.Payload.Length);
        }

        // ----------------------------------------------------------------
        // The 51Did payload rules
        // ----------------------------------------------------------------

        [TestMethod]
        public void TryParse_RandomOneByteShort_InvalidTypePayloadLength()
        {
            var payload = CanonicalRandomPayload()
                .Take(FodId.RandomPayloadLength - 1).ToArray();

            var ok = FodId.TryParse(
                _factory.SignedOwidBase64(payload), out var fodId, out var status);

            AssertRefused(ok, fodId, status, FodIdParseStatus.InvalidTypePayloadLength);
        }

        [TestMethod]
        [DataRow(IdType.Probabilistic)]
        [DataRow(IdType.HashedEmail)]
        public void TryParse_HashTypeOneByteShort_InvalidTypePayloadLength(
            IdType type)
        {
            var payload = PayloadOfType(type, FodId.PayloadLength - 1);

            var ok = FodId.TryParse(
                _factory.SignedOwidBase64(payload), out var fodId, out var status);

            AssertRefused(ok, fodId, status, FodIdParseStatus.InvalidTypePayloadLength);
        }

        [TestMethod]
        [DataRow(IdType.Probabilistic)]
        [DataRow(IdType.HashedEmail)]
        [DataRow(IdType.Random)]
        public void TryParse_HeaderOnly_InvalidTypePayloadLength(IdType type)
        {
            var payload = PayloadOfType(type, FodId.HeaderLength);

            var ok = FodId.TryParse(
                _factory.SignedBytes(payload, DateTime.UtcNow),
                out var fodId,
                out var status);

            AssertRefused(ok, fodId, status, FodIdParseStatus.InvalidTypePayloadLength);
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(FodId.HeaderLength - 1)]
        public void TryParse_ShorterThanHeader_PayloadTooShort(int length)
        {
            var payload = PayloadOfType(IdType.Probabilistic, length);

            var ok = FodId.TryParse(
                _factory.SignedOwidBase64(payload), out var fodId, out var status);

            AssertRefused(ok, fodId, status, FodIdParseStatus.PayloadTooShort);
        }

        [TestMethod]
        public void TryParse_ReservedHeaderOnly_Parses()
        {
            // Reserved keeps the documented best-effort behaviour, taking
            // whatever follows the header, which may be nothing.
            var payload = PayloadOfType(IdType.Reserved, FodId.HeaderLength);

            var ok = FodId.TryParse(
                _factory.SignedOwidBase64(payload), out var fodId, out var status);

            AssertParsed(ok, fodId, status);
            Assert.AreEqual(0, fodId!.MatchKey.Length);
        }

        // ----------------------------------------------------------------
        // OWID failures carried through unchanged
        // ----------------------------------------------------------------

        [TestMethod]
        [DataRow("This is not valid Base64!@#$")]
        [DataRow("A")]
        [DataRow("====")]
        public void TryParse_InvalidBase64_ReportsTheOwidStatus(string value)
        {
            var ok = FodId.TryParse(value, out var fodId, out var status);

            AssertRefused(ok, fodId, status, FodIdParseStatus.InvalidBase64);
        }

        [TestMethod]
        public void TryParse_TrailingByte_ByteCountMismatchUnchanged()
        {
            // The declared payload count no longer agrees with the bytes
            // present. The OWID reader names that before any signature is
            // looked at, and the 51Did surface reports the same word on
            // both the string and the byte surface.
            var bytes = _factory.SignedBytes(CanonicalPayload(), DateTime.UtcNow);
            var damaged = bytes.Concat(new byte[] { 0x00 }).ToArray();

            var fromBytes = FodId.TryParse(damaged, out var a, out var first);
            var fromString = FodId.TryParse(
                Convert.ToBase64String(damaged), out var b, out var second);

            AssertRefused(fromBytes, a, first, FodIdParseStatus.ByteCountMismatch);
            AssertRefused(fromString, b, second, FodIdParseStatus.ByteCountMismatch);
        }

        [TestMethod]
        public void TryParse_DeclaredCountTooLarge_ByteCountMismatchUnchanged()
        {
            var bytes = _factory.SignedBytes(CanonicalPayload(), DateTime.UtcNow);
            // The four byte count sits after the version byte, the domain,
            // its terminator and the four date bytes.
            var countOffset = 1 + TestDomain.Length + 1 + 4;
            bytes[countOffset + 3] = 0x7F;

            var ok = FodId.TryParse(bytes, out var fodId, out var status);

            AssertRefused(ok, fodId, status, FodIdParseStatus.ByteCountMismatch);
        }

        [TestMethod]
        public void TryParse_UnknownVersion_UnsupportedVersionUnchanged()
        {
            var bytes = _factory.SignedBytes(CanonicalPayload(), DateTime.UtcNow);
            bytes[0] = 9;

            var ok = FodId.TryParse(bytes, out var fodId, out var status);

            AssertRefused(ok, fodId, status, FodIdParseStatus.UnsupportedVersion);
        }

        [TestMethod]
        public void TryParse_Truncated_UnexpectedEndUnchanged()
        {
            var bytes = _factory.SignedBytes(CanonicalPayload(), DateTime.UtcNow);
            // Cut inside the domain, before its terminator.
            var truncated = bytes.Take(3).ToArray();

            var ok = FodId.TryParse(truncated, out var fodId, out var status);

            AssertRefused(ok, fodId, status, FodIdParseStatus.UnexpectedEnd);
        }

        [TestMethod]
        public void TryParse_VersionZero_AbsentNodeUnchanged()
        {
            var ok = FodId.TryParse(new byte[] { 0 }, out var fodId, out var status);

            AssertRefused(ok, fodId, status, FodIdParseStatus.AbsentNode);
        }

        [TestMethod]
        public void TryParse_NullString_MissingInput()
        {
            var ok = FodId.TryParse((string?)null, out var fodId, out var status);

            AssertRefused(ok, fodId, status, FodIdParseStatus.MissingInput);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("\r\n")]
        public void TryParse_EmptyString_MissingInput(string value)
        {
            var ok = FodId.TryParse(value, out var fodId, out var status);

            AssertRefused(ok, fodId, status, FodIdParseStatus.MissingInput);
        }

        [TestMethod]
        public void TryParse_NullBytes_MissingInput()
        {
            var ok = FodId.TryParse((byte[]?)null, out var fodId, out var status);

            AssertRefused(ok, fodId, status, FodIdParseStatus.MissingInput);
        }

        [TestMethod]
        public void TryParse_EmptyBytes_MissingInput()
        {
            var ok = FodId.TryParse(
                Array.Empty<byte>(), out var fodId, out var status);

            AssertRefused(ok, fodId, status, FodIdParseStatus.MissingInput);
        }

        [TestMethod]
        public void ParseStatus_CarriesEveryOwidStatusByNameAndValue()
        {
            // The 51Did vocabulary is the OWID one plus two. A rename or a
            // renumbering on either side would let a status be reported as
            // a different one, which this catches.
            foreach (var owid in Enum.GetValues<OwidParseStatus>())
            {
                Assert.IsTrue(
                    Enum.TryParse<FodIdParseStatus>(
                        owid.ToString(), out var fodId),
                    $"FodIdParseStatus lacks {owid}");
                Assert.AreEqual((int)owid, (int)fodId, owid.ToString());
            }
            var extra = Enum.GetNames<FodIdParseStatus>()
                .Except(Enum.GetNames<OwidParseStatus>())
                .OrderBy(name => name)
                .ToArray();
            CollectionAssert.AreEqual(
                new[] { "InvalidTypePayloadLength", "PayloadTooShort" },
                extra);
        }

        // ----------------------------------------------------------------
        // A date the runtime cannot hold
        // ----------------------------------------------------------------

        /// <summary>
        /// A signed envelope whose four byte minute count is 0xFFFFFFFF,
        /// which the wire format allows and which lands on 15 February
        /// 10186, past the end of the year 9999 where
        /// <see cref="DateTime"/> stops. The bytes are changed after
        /// signing, which is fine because the read refuses the date before
        /// any signature is looked at.
        /// </summary>
        private byte[] DatedPastTheYear9999()
        {
            var bytes = _factory.SignedBytes(CanonicalPayload(), DateTime.UtcNow);
            // The four little endian date bytes sit after the version byte,
            // the domain and its terminator.
            var dateOffset = 1 + TestDomain.Length + 1;
            for (var i = 0; i < 4; i++)
            {
                bytes[dateOffset + i] = 0xFF;
            }
            return bytes;
        }

        [TestMethod]
        public void TryParse_DatePastTheYear9999_ImplementationCapacityExceeded()
        {
            // The OWID reader judges the count before the arithmetic, so
            // the read answers with a status instead of throwing, and the
            // 51Did surface carries that status through unchanged on both
            // the byte and the string surface. The same bytes read fine
            // where the date type is wider, so the status is the runtime's
            // limit and not a fault in the data.
            var bytes = DatedPastTheYear9999();
            Assert.IsFalse(
                Owid.Client.Model.Owid.TryParse(bytes, out _, out var owidStatus));
            Assert.AreEqual(
                OwidParseStatus.ImplementationCapacityExceeded, owidStatus);

            var fromBytes = FodId.TryParse(bytes, out var a, out var first);
            var fromString = FodId.TryParse(
                Convert.ToBase64String(bytes), out var b, out var second);

            AssertRefused(
                fromBytes, a, first,
                FodIdParseStatus.ImplementationCapacityExceeded);
            AssertRefused(
                fromString, b, second,
                FodIdParseStatus.ImplementationCapacityExceeded);
        }

        [TestMethod]
        public void Throwing_DatePastTheYear9999_Format()
        {
            // The throwing surface runs the same walk, so the date the
            // runtime cannot hold is a format problem with the status in
            // the message, and never the ArgumentOutOfRangeException that
            // the arithmetic would have thrown.
            var bytes = DatedPastTheYear9999();

            var fromString = Assert.ThrowsExactly<FormatException>(
                () => FodId.FromBase64(Convert.ToBase64String(bytes)));
            var fromBytes = Assert.ThrowsExactly<FormatException>(
                () => new FodId(bytes));

            StringAssert.Contains(
                fromString.Message,
                nameof(FodIdParseStatus.ImplementationCapacityExceeded));
            StringAssert.Contains(
                fromBytes.Message,
                nameof(FodIdParseStatus.ImplementationCapacityExceeded));
        }

        // ----------------------------------------------------------------
        // Parsing is not verification
        // ----------------------------------------------------------------

        [TestMethod]
        public void TryParse_AlteredSignature_ParsesThenSignatureInvalid()
        {
            var bytes = _factory.SignedBytes(CanonicalPayload(), DateTime.UtcNow);
            bytes[bytes.Length - 10] ^= 0x01;

            var ok = FodId.TryParse(bytes, out var fodId, out var status);

            AssertParsed(ok, fodId, status);
            Assert.AreEqual(
                OwidSignatureStatus.SignatureInvalid,
                fodId!.SignatureStatus(_factory.PublicPem));
        }

        [TestMethod]
        public void TryParse_AlteredPayload_ParsesThenSignatureInvalid()
        {
            var bytes = _factory.SignedBytes(CanonicalPayload(), DateTime.UtcNow);
            var matchKeyOffset = bytes.Length - SignatureLength - FodId.MatchKeyLength;
            bytes[matchKeyOffset] ^= 0xFF;

            var ok = FodId.TryParse(bytes, out var fodId, out var status);

            AssertParsed(ok, fodId, status);
            Assert.AreEqual(
                OwidSignatureStatus.SignatureInvalid,
                fodId!.SignatureStatus(_factory.PublicPem));
        }

        [TestMethod]
        public void TryParse_WrongKey_ParsesThenSignatureInvalid()
        {
            var other = new FodIdTestFactory();

            var ok = FodId.TryParse(
                _factory.SignedOwidBase64(CanonicalPayload()),
                out var fodId,
                out var status);

            AssertParsed(ok, fodId, status);
            Assert.AreEqual(
                OwidSignatureStatus.SignatureInvalid,
                fodId!.SignatureStatus(other.PublicPem));
            Assert.AreEqual(
                OwidSignatureStatus.SignatureValid,
                fodId!.SignatureStatus(_factory.PublicPem));
        }

        [TestMethod]
        public void SignatureStatus_KeyUnavailable_IsNotSignatureInvalid()
        {
            var fodId = FodId.FromBase64(
                _factory.SignedOwidBase64(CanonicalPayload()));

            Assert.AreEqual(
                OwidSignatureStatus.KeyUnavailable,
                fodId.SignatureStatus((ECDsa)null!));
            Assert.AreEqual(
                OwidSignatureStatus.KeyUnavailable,
                fodId.SignatureStatus(string.Empty));
            Assert.AreEqual(
                OwidSignatureStatus.InvalidKey,
                fodId.SignatureStatus("not a pem"));
        }

        // ----------------------------------------------------------------
        // The throwing surface, built on the same walk
        // ----------------------------------------------------------------

        [TestMethod]
        public void Throwing_NullString_ArgumentNull()
        {
            Assert.ThrowsExactly<ArgumentNullException>(
                () => new FodId((string)null!));
            Assert.ThrowsExactly<ArgumentNullException>(
                () => FodId.FromBase64(null!));
            Assert.ThrowsExactly<ArgumentNullException>(
                () => ((string)null!).As51Did());
        }

        [TestMethod]
        public void Throwing_NullBytes_ArgumentNull()
        {
            Assert.ThrowsExactly<ArgumentNullException>(
                () => new FodId((byte[])null!));
        }

        [TestMethod]
        public void Throwing_NullOwid_ArgumentNull()
        {
            Assert.ThrowsExactly<ArgumentNullException>(
                () => new FodId((Owid.Client.Model.Owid)null!));
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("  ")]
        public void Throwing_EmptyString_Argument(string value)
        {
            var error = Assert.ThrowsExactly<ArgumentException>(
                () => new FodId(value));

            Assert.AreEqual("base64", error.ParamName);
        }

        [TestMethod]
        public void Throwing_EmptyBytes_Argument()
        {
            var error = Assert.ThrowsExactly<ArgumentException>(
                () => new FodId(Array.Empty<byte>()));

            Assert.AreEqual("buffer", error.ParamName);
        }

        [TestMethod]
        public void Throwing_InvalidBase64_Format()
        {
            var error = Assert.ThrowsExactly<FormatException>(
                () => new FodId("not base64 in any alphabet!"));

            StringAssert.Contains(
                error.Message, nameof(FodIdParseStatus.InvalidBase64));
        }

        [TestMethod]
        public void Throwing_NotAnEnvelope_Format()
        {
            var bytes = _factory.SignedBytes(CanonicalPayload(), DateTime.UtcNow);
            var damaged = bytes.Concat(new byte[] { 0x00 }).ToArray();

            var fromBytes = Assert.ThrowsExactly<FormatException>(
                () => new FodId(damaged));
            var fromString = Assert.ThrowsExactly<FormatException>(
                () => new FodId(Convert.ToBase64String(damaged)));

            StringAssert.Contains(
                fromBytes.Message, nameof(FodIdParseStatus.ByteCountMismatch));
            StringAssert.Contains(
                fromString.Message, nameof(FodIdParseStatus.ByteCountMismatch));
        }

        [TestMethod]
        public void Throwing_ShorterThanHeader_Argument()
        {
            var base64 = _factory.SignedOwidBase64(new byte[2]);

            var error = Assert.ThrowsExactly<ArgumentException>(
                () => new FodId(base64));

            Assert.AreEqual("base64", error.ParamName);
            StringAssert.Contains(
                error.Message, nameof(FodIdParseStatus.PayloadTooShort));
        }

        [TestMethod]
        public void Throwing_ShortForType_Argument()
        {
            var payload = CanonicalRandomPayload()
                .Take(FodId.RandomPayloadLength - 1).ToArray();
            var owid = _factory.SignedOwid(payload);

            var fromString = Assert.ThrowsExactly<ArgumentException>(
                () => new FodId(owid.AsBase64()));
            var fromBytes = Assert.ThrowsExactly<ArgumentException>(
                () => new FodId(owid.AsByteArray()));
            var fromOwid = Assert.ThrowsExactly<ArgumentException>(
                () => new FodId(owid));

            Assert.AreEqual("base64", fromString.ParamName);
            Assert.AreEqual("buffer", fromBytes.ParamName);
            Assert.AreEqual("owid", fromOwid.ParamName);
            StringAssert.Contains(
                fromOwid.Message,
                nameof(FodIdParseStatus.InvalidTypePayloadLength));
        }

        [TestMethod]
        public void Throwing_AndTryParse_AgreeOnEveryInput()
        {
            // One walk serves both surfaces, so an input either parses on
            // both or is refused on both with the exception the status
            // maps to.
            var good = _factory.SignedBytes(CanonicalPayload(), DateTime.UtcNow);
            var trailing = good.Concat(new byte[] { 1 }).ToArray();
            var shortRandom = _factory.SignedBytes(
                CanonicalRandomPayload().Take(FodId.RandomPayloadLength - 1).ToArray(),
                DateTime.UtcNow);
            foreach (var bytes in new[] { good, trailing, shortRandom, Array.Empty<byte>() })
            {
                var ok = FodId.TryParse(bytes, out var parsed, out var status);
                try
                {
                    var thrown = new FodId(bytes);
                    Assert.IsTrue(ok, $"the constructor accepted what TryParse refused ({status})");
                    Assert.AreEqual(parsed!.AsBase64(), thrown.AsBase64());
                }
                catch (Exception e) when (e is FormatException || e is ArgumentException)
                {
                    Assert.IsFalse(ok, "the constructor refused what TryParse accepted");
                    StringAssert.Contains(e.Message, status.ToString());
                }
            }
        }
    }
}
