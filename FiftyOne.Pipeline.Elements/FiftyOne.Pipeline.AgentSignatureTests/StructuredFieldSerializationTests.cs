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

using FiftyOne.Pipeline.AgentSignature.Parsing;
using FiftyOne.Pipeline.AgentSignature.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.AgentSignature.Tests
{
    /// <summary>
    /// Checks that <c>StructuredFieldSerializer</c> writes the one form
    /// RFC 8941 section 4.1 allows, and that the element rebuilds the
    /// signature base from that form rather than from the text as the
    /// signer wrote it, which RFC 9421 sections 2.1.2, 2.3 and 2.5
    /// require. The conformance check reuses the vendored HTTP Working
    /// Group suite, whose 'canonical' entries state the strict form of
    /// every input that is not already written in it.
    /// </summary>
    [TestClass]
    public class StructuredFieldSerializationTests
    {
        /// <summary>
        /// Supplied by the test framework and used to print how many cases
        /// the conformance check compared.
        /// </summary>
        public TestContext TestContext { get; set; }

        #region The vendored conformance suite

        /// <summary>
        /// Every case of the vendored suite that our parser accepts must
        /// serialise back to the strict form the suite states, being the
        /// 'canonical' text when the case carries one and the case's own
        /// input otherwise. A case the parser rejects is not judged here,
        /// because <see cref="StructuredFieldConformanceTests"/> already
        /// says which rejections are correct.
        /// </summary>
        /// <param name="testCase">The case to run.</param>
        [DataTestMethod]
        [DynamicData(
            nameof(StructuredFieldConformanceTests.Cases),
            typeof(StructuredFieldConformanceTests),
            DynamicDataSourceType.Property,
            DynamicDataDisplayName =
                nameof(StructuredFieldConformanceTests.CaseDisplayName),
            DynamicDataDisplayNameDeclaringType =
                typeof(StructuredFieldConformanceTests))]
        public void ParsedCasesSerialiseToTheStrictForm(
            StructuredFieldConformanceTests.ConformanceCase testCase)
        {
            var serialized = TrySerialise(testCase);
            if (serialized == null)
            {
                return;
            }
            Assert.AreEqual(
                testCase.Canonical,
                serialized,
                "The strict serialisation of " + testCase.File + " / " +
                testCase.Name + ", raw '" + testCase.Raw + "', should be '" +
                testCase.Canonical + "'.");
        }

        /// <summary>
        /// The conformance check above only bites if the suite holds
        /// plenty of cases the parser accepts, and in particular cases
        /// whose strict form differs from how the input was written,
        /// because those are the ones the raw text would get wrong. The
        /// floors here are far below the counts the suite holds today, so
        /// the test fails only when the suite stops being applied.
        /// </summary>
        [TestMethod]
        public void SuiteCoverageOfSerialisation()
        {
            var compared = 0;
            var respelled = 0;
            foreach (var arguments in StructuredFieldConformanceTests.Cases)
            {
                var testCase = (StructuredFieldConformanceTests
                    .ConformanceCase)arguments[0];
                var serialized = TrySerialise(testCase);
                if (serialized == null)
                {
                    continue;
                }
                compared++;
                if (string.Equals(
                    testCase.Canonical,
                    testCase.Raw,
                    StringComparison.Ordinal) == false)
                {
                    respelled++;
                }
            }
            TestContext.WriteLine(
                "Serialised {0} parsed conformance cases, of which {1} " +
                "were written in a legal form that differs from the " +
                "strict one.",
                compared,
                respelled);
            Assert.IsTrue(
                compared >= 200,
                "Only " + compared + " conformance cases were serialised, " +
                "so the suite is not being applied.");
            Assert.IsTrue(
                respelled >= 20,
                "Only " + respelled + " serialised cases differ from " +
                "their input, so legal respellings are not being " +
                "exercised.");
        }

        /// <summary>
        /// Parse one conformance case and serialise what was read, or
        /// answer null when the parser rejects the input.
        /// </summary>
        /// <param name="testCase">The case to run.</param>
        /// <returns>The strict serialisation, or null.</returns>
        private static string TrySerialise(
            StructuredFieldConformanceTests.ConformanceCase testCase)
        {
            if (string.Equals(
                testCase.HeaderType, "dictionary", StringComparison.Ordinal))
            {
                return StructuredFieldParser.TryParseDictionary(
                    testCase.Raw, out var dictionary)
                    ? StructuredFieldSerializer.Serialize(dictionary)
                    : null;
            }
            return StructuredFieldParser.TryParseItem(
                testCase.Raw, out var item)
                ? StructuredFieldSerializer.Serialize(item)
                : null;
        }

        #endregion

        #region Serialiser edge rules

        /// <summary>
        /// A decimal is rounded to three places, taking the even final
        /// digit when the value sits exactly half way, and the fraction is
        /// written without trailing zeros but never empty. RFC 8941
        /// section 4.1.5 spells each of these out, and the parser never
        /// produces a value that needs the rounding, so only a direct test
        /// reaches these rules.
        /// </summary>
        /// <param name="value">The decimal to serialise.</param>
        /// <param name="expected">The text RFC 8941 requires.</param>
        [DataTestMethod]
        [DataRow("1.2345", "1.234")]
        [DataRow("1.2355", "1.236")]
        [DataRow("0.0015", "0.002")]
        [DataRow("0.0025", "0.002")]
        [DataRow("-1.2345", "-1.234")]
        [DataRow("1.200", "1.2")]
        [DataRow("2.000", "2.0")]
        [DataRow("5", "5.0")]
        [DataRow("-0.5", "-0.5")]
        [DataRow("0.0001", "0.0")]
        public void DecimalRoundsBankerStyleAndKeepsOneFractionDigit(
            string value,
            string expected)
        {
            Assert.AreEqual(
                expected,
                SerialiseBare(decimal.Parse(
                    value,
                    System.Globalization.CultureInfo.InvariantCulture)),
                "The decimal " + value + " should serialise as '" +
                expected + "'.");
        }

        /// <summary>
        /// A decimal whose integer part rounds up into a thirteenth digit
        /// cannot be carried by a structured field at all. RFC 8941
        /// section 4.1.5 puts the digit check after the rounding for
        /// exactly this value.
        /// </summary>
        [TestMethod]
        public void DecimalRoundingIntoAThirteenthDigitIsRefused()
        {
            Assert.ThrowsException<ArgumentException>(
                () => SerialiseBare(999999999999.9995m),
                "A decimal that rounds up to thirteen integer digits " +
                "should be refused.");
        }

        /// <summary>
        /// An integer keeps its plain base 10 form, and a long that a
        /// structured field cannot carry, which the parser never reads, is
        /// refused rather than written. RFC 8941 section 4.1.4.
        /// </summary>
        [TestMethod]
        public void IntegerLimitsAreTheStandardsLimits()
        {
            Assert.AreEqual("999999999999999",
                SerialiseBare(999999999999999L));
            Assert.AreEqual("-999999999999999",
                SerialiseBare(-999999999999999L));
            Assert.AreEqual("0", SerialiseBare(0L));
            Assert.ThrowsException<ArgumentException>(
                () => SerialiseBare(1000000000000000L),
                "An integer of sixteen digits should be refused.");
        }

        /// <summary>
        /// A string escapes only the backslash and the double quote.
        /// RFC 8941 section 4.1.6.
        /// </summary>
        [TestMethod]
        public void StringEscapesOnlyBackslashAndQuote()
        {
            Assert.AreEqual(
                "\"he said \\\"ok\\\" and left a \\\\ behind\"",
                SerialiseBare("he said \"ok\" and left a \\ behind"));
            Assert.AreEqual("\"\"", SerialiseBare(string.Empty));
            Assert.ThrowsException<ArgumentException>(
                () => SerialiseBare("line one\nline two"),
                "A string holding a control character should be refused.");
        }

        /// <summary>
        /// A byte sequence is standard base64 between colons with the '='
        /// padding kept, whatever length the data has. RFC 8941 section
        /// 4.1.8 requires the padding, and the parser accepts input
        /// without it, so the two forms must not round trip as each other.
        /// </summary>
        [TestMethod]
        public void ByteSequenceKeepsItsPadding()
        {
            Assert.AreEqual("::", SerialiseBare(new byte[0]));
            Assert.AreEqual(":AQ==:", SerialiseBare(new byte[] { 1 }));
            Assert.AreEqual(":AQI=:", SerialiseBare(new byte[] { 1, 2 }));
            Assert.AreEqual(":AQID:", SerialiseBare(new byte[] { 1, 2, 3 }));
            Assert.IsTrue(
                StructuredFieldParser.TryParseItem(":aGVsbG8:", out var item),
                "The parser should accept a byte sequence sent without " +
                "its padding.");
            Assert.AreEqual(
                ":aGVsbG8=:",
                StructuredFieldSerializer.Serialize(item),
                "The padding the sender left off should come back in the " +
                "strict form.");
        }

        /// <summary>
        /// A parameter whose value is the boolean true is written as its
        /// bare key, and one whose value is false keeps '=?0'. RFC 8941
        /// section 4.1.1.2 step 2.3.
        /// </summary>
        [TestMethod]
        public void BooleanTrueParameterIsTheBareKey()
        {
            var item = new SfItem(
                new SfToken("a"),
                new List<SfParameter>
                {
                    new SfParameter("b", true),
                    new SfParameter("c", false),
                    new SfParameter("d", 5L),
                },
                "a;b=?1;c=?0;d=5");
            Assert.AreEqual(
                "a;b;c=?0;d=5",
                StructuredFieldSerializer.Serialize(item),
                "A true parameter should lose its '=?1' and a false one " +
                "should keep its '=?0'.");
        }

        /// <summary>
        /// A dictionary member whose value is the boolean true is written
        /// as its bare key at dictionary level, yet its member value alone
        /// is written '?1', because RFC 9421 section 2.1.2 puts the member
        /// value form on the signature base line of a component covered
        /// with the 'key' parameter.
        /// </summary>
        [TestMethod]
        public void BooleanTrueMemberIsTheBareKeyButItsValueIsTrue()
        {
            Assert.IsTrue(
                StructuredFieldParser.TryParseDictionary(
                    "a, b=?1, c;x=1", out var dictionary),
                "The dictionary should parse.");
            Assert.AreEqual(
                "a, b, c;x=1",
                StructuredFieldSerializer.Serialize(dictionary),
                "Both spellings of a true member should collapse to the " +
                "bare key at dictionary level.");
            Assert.IsTrue(
                dictionary.TryGetValue("a", out var bare),
                "The member 'a' should be present.");
            Assert.AreEqual(
                "?1",
                StructuredFieldSerializer.Serialize(bare),
                "The member value of 'a' alone should serialise as '?1'.");
            Assert.IsTrue(
                dictionary.TryGetValue("c", out var withParameter),
                "The member 'c' should be present.");
            Assert.AreEqual(
                "?1;x=1",
                StructuredFieldSerializer.Serialize(withParameter),
                "The member value of 'c' alone should serialise as '?1' " +
                "followed by its parameter.");
        }

        /// <summary>
        /// An inner list is written with single spaces between its items
        /// and no spaces around its parameters, however the sender spaced
        /// it. RFC 8941 sections 4.1.1.1 and 4.1.1.2.
        /// </summary>
        [TestMethod]
        public void InnerListSpacingIsNormalised()
        {
            Assert.IsTrue(
                StructuredFieldParser.TryParseDictionary(
                    "a=(1  2   3); x; y=2", out var dictionary),
                "The dictionary should parse.");
            Assert.AreEqual(
                "a=(1 2 3);x;y=2",
                StructuredFieldSerializer.Serialize(dictionary),
                "The inner list should be written with single spaces and " +
                "its parameters without any.");
        }

        /// <summary>
        /// Serialise a bare value by wrapping it in an item that has no
        /// parameters.
        /// </summary>
        /// <param name="value">The bare value.</param>
        /// <returns>The strict serialisation.</returns>
        private static string SerialiseBare(object value)
        {
            return StructuredFieldSerializer.Serialize(
                new SfItem(value, new List<SfParameter>(), null));
        }

        #endregion

        #region The element rebuilds from the strict form

        /// <summary>
        /// A compliant signer signs over the strict signature base and may
        /// then write the headers any legal way, so a request whose
        /// 'Signature-Input' and 'Signature-Agent' headers carry legal
        /// extra spacing, which RFC 8941 permits a parser to accept, must
        /// still read Verified. Before the element rebuilt the base from
        /// the strict serialisation this request read Invalid with the
        /// SignatureMismatch reason, because the base was rebuilt from the
        /// respelled text rather than from the text the agent signed.
        /// </summary>
        [TestMethod]
        public void LegalNonStrictHeaderSpacingStillVerifies()
        {
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var options = new SigningOptions
                {
                    // The header spells the member parameter with a space
                    // after the semicolon, whilst the signed base carries
                    // the strict form without one.
                    SignatureAgentHeaderParameters = "; type=directory",
                    SignatureAgentSignedParameters = ";type=directory",
                };
                options.ExtraComponents.Add(new KeyValuePair<string, string>(
                    "\"example-dict\";key=\"b\"", "2;x=1;y=2"));
                var signed = RequestSigner.Sign(options);

                // Respell the 'Signature-Input' header with spacing that
                // RFC 8941 permits, after the signature was made over the
                // strict form. A double space inside the inner list, and a
                // space after each parameter semicolon, both inside a
                // covered component identifier and on the member itself.
                signed.SignatureInput = signed.SignatureInput
                    .Replace("\" \"", "\"  \"")
                    .Replace(";key=", "; key=")
                    .Replace(";created=", "; created=")
                    .Replace(";expires=", ";  expires=")
                    .Replace(";keyid=", "; keyid=")
                    .Replace(";alg=", "; alg=")
                    .Replace(";tag=", "; tag=");

                var result = harness.Process(new Dictionary<string, string>
                {
                    { "header.signature", signed.Signature },
                    { "header.signature-input", signed.SignatureInput },
                    { "header.signature-agent", signed.SignatureAgent },
                    { "header.host", "example.com" },
                    { "header.protocol", "https" },
                    // The covered member 'b' is spelled with spaces after
                    // its parameter semicolons, which the signed base does
                    // not carry.
                    { "header.example-dict", "a=1, b=2; x=1; y=2, c" },
                });

                Assert.AreEqual(
                    Constants.STATUS_VERIFIED,
                    result.AgentSignature.Value,
                    "A signature made over the strict base should read " +
                    "Verified when the headers carry legal extra spacing, " +
                    "yet the element read " + result.AgentSignature.Value +
                    " with the reason " +
                    result.AgentSignatureReason.Value + ".");
                Assert.AreEqual(
                    Constants.REASON_VERIFIED,
                    result.AgentSignatureReason.Value,
                    "The reason should be Verified.");
            }
        }

        /// <summary>
        /// A dictionary member written without a value is the boolean
        /// true, so a signature covering that member through the 'key'
        /// parameter is checked against a base line holding '?1', which is
        /// what a compliant signer signed. RFC 9421 section 2.1.2 prints
        /// exactly this case for its Example-Dict member 'd'.
        /// </summary>
        [TestMethod]
        public void CoveredMemberWithNoValueResolvesAsBooleanTrue()
        {
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var options = new SigningOptions();
                options.ExtraComponents.Add(new KeyValuePair<string, string>(
                    "\"example-dict\";key=\"d\"", "?1"));
                options.ExtraComponents.Add(new KeyValuePair<string, string>(
                    "\"example-dict\";key=\"e\"", "?1;x=1"));
                var signed = RequestSigner.Sign(options);

                var result = harness.Process(new Dictionary<string, string>
                {
                    { "header.signature", signed.Signature },
                    { "header.signature-input", signed.SignatureInput },
                    { "header.signature-agent", signed.SignatureAgent },
                    { "header.host", "example.com" },
                    { "header.protocol", "https" },
                    // 'd' has no value at all and 'e' has only a
                    // parameter, so both stand for the boolean true.
                    { "header.example-dict", "a=1, d, e;x=1" },
                });

                Assert.AreEqual(
                    Constants.STATUS_VERIFIED,
                    result.AgentSignature.Value,
                    "A signature covering members written without a value " +
                    "should read Verified, because their lines carry " +
                    "'?1', yet the element read " +
                    result.AgentSignature.Value + " with the reason " +
                    result.AgentSignatureReason.Value + ".");
            }
        }

        /// <summary>
        /// Two spellings of one covered component identifier are still one
        /// component, so a signature listing both is refused the way a
        /// literal duplicate is. RFC 9421 section 2.5 step 2.1 makes the
        /// duplicate an error, and comparing raw text would let a
        /// respelled duplicate through.
        /// </summary>
        [TestMethod]
        public void RespelledDuplicateComponentIsRefused()
        {
            Assert.IsTrue(
                StructuredFieldParser.TryParseDictionary(
                    "sig1=(\"@authority\" \"@authority\" );c=1",
                    out var same),
                "The header holding a literal duplicate should parse.");
            Assert.IsTrue(same.TryGetValue("sig1", out var sameMember));
            Assert.IsFalse(
                Verification.SignatureBase.TryBuild(
                    sameMember.InnerList,
                    StructuredFieldSerializer.Serialize(sameMember),
                    new FixedResolver(),
                    out _),
                "A literal duplicate component should be refused.");

            // The same identifier twice, spelled once strictly and once
            // with legal extra spacing on its parameter.
            Assert.IsTrue(
                StructuredFieldParser.TryParseDictionary(
                    "sig1=(\"x\";key=\"a\" \"x\"; key=\"a\");c=1",
                    out var respelled),
                "The header holding a respelled duplicate should parse.");
            Assert.IsTrue(respelled.TryGetValue("sig1", out var member));
            Assert.IsFalse(
                Verification.SignatureBase.TryBuild(
                    member.InnerList,
                    StructuredFieldSerializer.Serialize(member),
                    new FixedResolver(),
                    out _),
                "A component listed twice in two legal spellings should " +
                "be refused the way a literal duplicate is.");
        }

        /// <summary>
        /// Answers every component with a fixed value, so that the tests
        /// on the base builder do not depend on any evidence.
        /// </summary>
        private sealed class FixedResolver :
            Verification.IComponentResolver
        {
            public bool TryResolve(
                string name,
                SfItem component,
                out string value)
            {
                value = "value";
                return true;
            }
        }

        #endregion
    }
}
