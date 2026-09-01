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
using System.Collections.Generic;
using System.Text;

namespace FiftyOne.Pipeline.AgentSignature.Tests
{
    /// <summary>
    /// Proves that the three Web Bot Auth headers are read the way RFC 8941
    /// and RFC 9421 say they must be. The covered components, their order and
    /// the exact text of the signature parameters all decide what gets
    /// signed, so each of them is checked against the architecture test
    /// vectors rather than against what the parser happens to produce.
    /// Malformed headers are checked through the element itself, because the
    /// promise made to a caller is that a bad header reads Invalid and
    /// Malformed rather than throwing.
    /// </summary>
    [TestClass]
    public class ParsingTests
    {
        /// <summary>
        /// The name of the fixture holding the four requests whose
        /// signatures have already expired.
        /// </summary>
        private const string SetV1 = "architecture v1";

        /// <summary>
        /// The name of the fixture holding the four requests whose
        /// signatures are valid until the year 2124.
        /// </summary>
        private const string SetV2 = "architecture v2";

        /// <summary>
        /// The eight 'Signature-Input' values of the architecture vectors,
        /// with what each one says written out in full so that a failure
        /// names the vector and the expectation.
        /// </summary>
        /// <returns>
        /// The case name, the fixture name, the position in that fixture,
        /// the label, the covered component names, the 'key' parameter on
        /// each covered component, the 'created' and 'expires' parameters
        /// and the 'alg' parameter.
        /// </returns>
        public static IEnumerable<object[]> SignatureInputCases()
        {
            yield return new object[]
            {
                "v1 rsa authority only", SetV1, 0, "sig1",
                new[] { "@authority" },
                new string[] { null },
                1735689600L, 1735693200L, "rsa-pss-sha512",
            };
            yield return new object[]
            {
                "v1 rsa with signature agent", SetV1, 1, "sig2",
                new[] { "@authority", "signature-agent" },
                new string[] { null, null },
                1735689600L, 1735693200L, "rsa-pss-sha512",
            };
            yield return new object[]
            {
                "v1 ed25519 authority only", SetV1, 2, "sig1",
                new[] { "@authority" },
                new string[] { null },
                1735689600L, 1735693200L, "ed25519",
            };
            yield return new object[]
            {
                "v1 ed25519 with signature agent", SetV1, 3, "sig2",
                new[] { "@authority", "signature-agent" },
                new string[] { null, null },
                1735689600L, 1735693200L, "ed25519",
            };
            yield return new object[]
            {
                "v2 rsa authority only", SetV2, 0, "sig1",
                new[] { "@authority" },
                new string[] { null },
                1735689600L, 4889289600L, "rsa-pss-sha512",
            };
            yield return new object[]
            {
                "v2 rsa with labelled signature agent", SetV2, 1, "sig2",
                new[] { "@authority", "signature-agent" },
                new string[] { null, "agent2" },
                1735689600L, 4889289600L, "rsa-pss-sha512",
            };
            yield return new object[]
            {
                "v2 ed25519 authority only", SetV2, 2, "sig1",
                new[] { "@authority" },
                new string[] { null },
                1735689600L, 4889289600L, "ed25519",
            };
            yield return new object[]
            {
                "v2 ed25519 with labelled signature agent", SetV2, 3, "sig2",
                new[] { "@authority", "signature-agent" },
                new string[] { null, "agent2" },
                1735689600L, 4889289600L, "ed25519",
            };
        }

        /// <summary>
        /// The eight architecture vectors named only by where they live, for
        /// the tests that read the expectation from the vector itself.
        /// </summary>
        /// <returns>The case name, the fixture name and the position.</returns>
        public static IEnumerable<object[]> ArchitectureCases()
        {
            foreach (var name in new[] { SetV1, SetV2 })
            {
                for (var index = 0; index < 4; index++)
                {
                    yield return new object[]
                    {
                        name + " vector " + index, name, index,
                    };
                }
            }
        }

        /// <summary>
        /// The 'Signature-Agent' header cases the standard publishes.
        /// </summary>
        /// <returns>The case name and the case.</returns>
        public static IEnumerable<object[]> SignatureAgentCases()
        {
            foreach (var vector in Fixtures.SignatureAgents())
            {
                yield return new object[] { vector.Name, vector };
            }
        }

        /// <summary>
        /// Header pairs that no agent could have sent, each with the reason
        /// it cannot be read. A null header is one the request did not carry
        /// at all.
        /// </summary>
        /// <returns>
        /// The case name, the 'Signature' header and the 'Signature-Input'
        /// header.
        /// </returns>
        public static IEnumerable<object[]> MalformedHeaderCases()
        {
            var vector = Fixtures.ArchitectureV2()[2];
            yield return new object[]
            {
                "unbalanced quotes in the signature input",
                vector.Signature,
                "sig1=(\"@authority\");created=1735689600;keyid=\"unclosed",
            };
            yield return new object[]
            {
                "missing closing parenthesis in the inner list",
                vector.Signature,
                "sig1=(\"@authority\"",
            };
            yield return new object[]
            {
                "byte sequence signature written without its colons",
                vector.Signature.Replace(":", string.Empty),
                vector.SignatureInput,
            };
            yield return new object[]
            {
                "label in the signature that the signature input lacks",
                vector.Signature + ", sig9=:AQID:",
                vector.SignatureInput,
            };
            yield return new object[]
            {
                "label in the signature input that the signature lacks",
                vector.Signature,
                vector.SignatureInput + ", sig9=(\"@authority\")",
            };
            yield return new object[]
            {
                "signature header with no signature input header",
                vector.Signature,
                null,
            };
            yield return new object[]
            {
                "signature input header with no signature header",
                null,
                vector.SignatureInput,
            };
        }

        /// <summary>
        /// Field values that are not RFC 8941 dictionaries. Each one has to
        /// be answered with false rather than with an exception, because the
        /// element runs on whatever a caller puts in front of it.
        /// </summary>
        /// <returns>The case name and the field value.</returns>
        public static IEnumerable<object[]> JunkFieldValues()
        {
            yield return new object[] { "a lone comma", "," };
            yield return new object[] { "a leading comma", ",sig1=1" };
            yield return new object[] { "a trailing comma", "sig1=1," };
            yield return new object[]
            {
                "an unterminated string", "sig1=\"unterminated",
            };
            yield return new object[]
            {
                "an unterminated byte sequence", "sig1=:AQID",
            };
            yield return new object[]
            {
                "a parameter value of no known type", "sig1=1;x=@",
            };
            yield return new object[]
            {
                "an inner list that never closes", "sig1=(\"@authority\"",
            };
            yield return new object[] { "a key starting with a digit", "1=2" };
            yield return new object[]
            {
                "a member value that is nothing at all", "sig1=",
            };
            yield return new object[]
            {
                "a very long unterminated string", LongUnterminatedString(),
            };
        }

        /// <summary>
        /// Every 'Signature-Input' value in the eight architecture vectors
        /// parses, and the covered components, their order and the six
        /// signature parameters are what the vector says they are.
        /// </summary>
        /// <param name="caseName">The name of the case.</param>
        /// <param name="setName">The fixture the vector came from.</param>
        /// <param name="index">The position of the vector in that fixture.</param>
        /// <param name="expectedLabel">The label the two headers share.</param>
        /// <param name="expectedComponents">
        /// The covered component names, in the order the signer wrote them.
        /// </param>
        /// <param name="expectedComponentKeys">
        /// The 'key' parameter on each covered component, or null where the
        /// component carries none.
        /// </param>
        /// <param name="expectedCreated">The 'created' parameter.</param>
        /// <param name="expectedExpires">The 'expires' parameter.</param>
        /// <param name="expectedAlgorithm">The 'alg' parameter.</param>
        [DataTestMethod]
        [DynamicData(
            nameof(SignatureInputCases), DynamicDataSourceType.Method)]
        public void SignatureInputParsesToTheStatedComponents(
            string caseName,
            string setName,
            int index,
            string expectedLabel,
            string[] expectedComponents,
            string[] expectedComponentKeys,
            long expectedCreated,
            long expectedExpires,
            string expectedAlgorithm)
        {
            var vector = Vector(setName, index);
            var candidate = Parse(vector, caseName);

            Assert.AreEqual(
                expectedLabel,
                candidate.Label,
                "The label of " + caseName + " should be '" +
                    expectedLabel + "'.");
            Assert.AreEqual(
                expectedComponents.Length,
                candidate.CoveredComponents.Count,
                "The signature of " + caseName + " should cover " +
                    expectedComponents.Length + " components, from '" +
                    vector.SignatureInput + "'.");
            for (var i = 0; i < expectedComponents.Length; i++)
            {
                Assert.AreEqual(
                    expectedComponents[i],
                    candidate.CoveredComponents[i].Value,
                    "Covered component " + i + " of " + caseName +
                        " should be '" + expectedComponents[i] +
                        "', because the order of the components decides " +
                        "the order of the lines of the signature base.");
                Assert.AreEqual(
                    expectedComponentKeys[i],
                    candidate.CoveredComponents[i].GetStringParameter("key"),
                    "The 'key' parameter on covered component " + i +
                        " of " + caseName + " should be " +
                        Show(expectedComponentKeys[i]) + ".");
            }

            Assert.IsTrue(
                candidate.Created.HasValue,
                "The 'created' parameter of " + caseName +
                    " should be read as a whole number.");
            Assert.AreEqual(
                expectedCreated,
                candidate.Created.Value,
                "The 'created' parameter of " + caseName +
                    " should be " + expectedCreated + ".");
            Assert.IsTrue(
                candidate.Expires.HasValue,
                "The 'expires' parameter of " + caseName +
                    " should be read as a whole number.");
            Assert.AreEqual(
                expectedExpires,
                candidate.Expires.Value,
                "The 'expires' parameter of " + caseName +
                    " should be " + expectedExpires + ".");
            Assert.AreEqual(
                vector.KeyId,
                candidate.KeyId,
                "The 'keyid' parameter of " + caseName +
                    " should be the thumbprint of the key the vector " +
                    "signed with, being '" + vector.KeyId + "'.");
            Assert.AreEqual(
                expectedAlgorithm,
                candidate.Algorithm,
                "The 'alg' parameter of " + caseName + " should be '" +
                    expectedAlgorithm + "'.");
            Assert.AreEqual(
                vector.Nonce,
                candidate.Nonce,
                "The 'nonce' parameter of " + caseName +
                    " should be the nonce the vector states.");
            Assert.AreEqual(
                Constants.TAG_WEB_BOT_AUTH,
                candidate.Tag,
                "The 'tag' parameter of " + caseName +
                    " should be '" + Constants.TAG_WEB_BOT_AUTH +
                    "', because every architecture vector is a Web Bot " +
                    "Auth request.");
        }

        /// <summary>
        /// The signature parameters are kept as the exact text that follows
        /// the label in the 'Signature-Input' header. The last line of the
        /// signature base is that text, so a single character of difference
        /// makes every signature fail.
        /// </summary>
        /// <param name="caseName">The name of the case.</param>
        /// <param name="setName">The fixture the vector came from.</param>
        /// <param name="index">The position of the vector in that fixture.</param>
        [DataTestMethod]
        [DynamicData(nameof(ArchitectureCases), DynamicDataSourceType.Method)]
        public void SignatureParamsIsTheTextAfterTheLabel(
            string caseName,
            string setName,
            int index)
        {
            var vector = Vector(setName, index);
            var candidate = Parse(vector, caseName);

            var prefix = candidate.Label + "=";
            StringAssert.StartsWith(
                vector.SignatureInput,
                prefix,
                "The 'Signature-Input' header of " + caseName +
                    " should start with the label and an equals sign.");
            var expected = vector.SignatureInput.Substring(prefix.Length);
            Assert.AreEqual(
                expected,
                candidate.SignatureParams,
                "The signature parameters of " + caseName + " should be " +
                    "the text after '" + prefix + "' character for " +
                    "character, because RFC 9421 section 2.5 puts that " +
                    "text on the '@signature-params' line of the " +
                    "signature base.");
        }

        /// <summary>
        /// Every 'Signature-Agent' case the standard publishes parses to the
        /// label, URI and type it states.
        /// </summary>
        /// <param name="caseName">The name of the case.</param>
        /// <param name="vector">The case.</param>
        [DataTestMethod]
        [DynamicData(
            nameof(SignatureAgentCases), DynamicDataSourceType.Method)]
        public void SignatureAgentHeaderParsesToTheStatedEntries(
            string caseName,
            SignatureAgentVector vector)
        {
            Assert.IsTrue(
                SignatureAgentEntry.TryParse(
                    vector.Header, true, out var entries),
                "The '" + caseName + "' header '" + vector.Header +
                    "' should parse.");
            Assert.AreEqual(
                vector.Entries.Count,
                entries.Count,
                "The '" + caseName + "' header should hold " +
                    vector.Entries.Count + " members.");
            for (var i = 0; i < vector.Entries.Count; i++)
            {
                Assert.AreEqual(
                    vector.Entries[i].Label,
                    entries[i].Label,
                    "Member " + i + " of the '" + caseName +
                        "' header should be labelled '" +
                        vector.Entries[i].Label + "'.");
                Assert.AreEqual(
                    vector.Entries[i].Uri,
                    entries[i].Value,
                    "Member " + i + " of the '" + caseName +
                        "' header should carry the URI '" +
                        vector.Entries[i].Uri + "' exactly as it was sent.");
                Assert.AreEqual(
                    vector.Entries[i].Type,
                    entries[i].Type,
                    "Member " + i + " of the '" + caseName +
                        "' header should be of type '" +
                        vector.Entries[i].Type + "'.");
            }
        }

        /// <summary>
        /// The URL the keys are fetched from follows the member type. A
        /// 'directory' member names an origin and the well known path is
        /// added to it, whilst a 'jwks_uri' or 'cimd' member names the
        /// document itself and is used as it stands.
        /// </summary>
        /// <param name="caseName">The name of the case.</param>
        /// <param name="vector">The case.</param>
        [DataTestMethod]
        [DynamicData(
            nameof(SignatureAgentCases), DynamicDataSourceType.Method)]
        public void SignatureAgentKeyUrlFollowsTheType(
            string caseName,
            SignatureAgentVector vector)
        {
            Assert.IsTrue(
                SignatureAgentEntry.TryParse(
                    vector.Header, true, out var entries),
                "The '" + caseName + "' header '" + vector.Header +
                    "' should parse.");
            for (var i = 0; i < vector.Entries.Count; i++)
            {
                var expected =
                    vector.Entries[i].Type == Constants.AGENT_TYPE_DIRECTORY
                        ? vector.Entries[i].Uri + Constants.DIRECTORY_PATH
                        : vector.Entries[i].Uri;
                Assert.AreEqual(
                    expected,
                    entries[i].KeyUrl,
                    "Member " + i + " of the '" + caseName +
                        "' header is of type '" + vector.Entries[i].Type +
                        "', so its keys should be fetched from '" +
                        expected + "'.");
            }
        }

        /// <summary>
        /// The bare quoted string form that the v1 vectors send carries no
        /// label, so it parses with an empty label when the element is
        /// configured to accept it.
        /// </summary>
        [TestMethod]
        public void BareQuotedSignatureAgentParsesWhenAllowed()
        {
            var header = Fixtures.ArchitectureV1()[1].SignatureAgent;
            Assert.AreEqual(
                "\"" + Fixtures.SignatureAgentOrigin + "\"",
                header,
                "The v1 vectors should send the 'Signature-Agent' header " +
                    "as a bare quoted string.");
            Assert.IsTrue(
                SignatureAgentEntry.TryParse(header, true, out var entries),
                "The bare quoted string header '" + header +
                    "' should parse when the legacy form is allowed.");
            Assert.AreEqual(
                1,
                entries.Count,
                "The bare quoted string header should hold one member.");
            Assert.AreEqual(
                string.Empty,
                entries[0].Label,
                "The bare quoted string form carries no label, so the " +
                    "label should be empty rather than null.");
            Assert.AreEqual(
                Fixtures.SignatureAgentOrigin,
                entries[0].Value,
                "The member should carry the URI '" +
                    Fixtures.SignatureAgentOrigin + "'.");
            Assert.AreEqual(
                Constants.AGENT_TYPE_DIRECTORY,
                entries[0].Type,
                "A member with no 'type' parameter should be of type '" +
                    Constants.AGENT_TYPE_DIRECTORY + "'.");
            Assert.AreEqual(
                Fixtures.SignatureAgentDirectoryUrl,
                entries[0].KeyUrl,
                "The keys of a 'directory' member should be fetched from '" +
                    Fixtures.SignatureAgentDirectoryUrl + "'.");
        }

        /// <summary>
        /// The bare quoted string form is refused when the element is
        /// configured to take only the dictionary form, because a member
        /// with no label cannot be named by a covered component.
        /// </summary>
        [TestMethod]
        public void BareQuotedSignatureAgentIsRefusedWhenNotAllowed()
        {
            var header = Fixtures.ArchitectureV1()[1].SignatureAgent;
            Assert.IsFalse(
                SignatureAgentEntry.TryParse(header, false, out _),
                "The bare quoted string header '" + header +
                    "' should be refused when the legacy form is not " +
                    "allowed.");
        }

        /// <summary>
        /// Headers no agent could have sent read Invalid with the Malformed
        /// reason. The element is run in a pipeline that lets exceptions
        /// through, so a test that passes also proves nothing was thrown.
        /// </summary>
        /// <param name="caseName">The name of the case.</param>
        /// <param name="signature">
        /// The 'Signature' header, or null when the request carried none.
        /// </param>
        /// <param name="signatureInput">
        /// The 'Signature-Input' header, or null when the request carried
        /// none.
        /// </param>
        [DataTestMethod]
        [DynamicData(
            nameof(MalformedHeaderCases), DynamicDataSourceType.Method)]
        public void MalformedHeadersReadMalformed(
            string caseName,
            string signature,
            string signatureInput)
        {
            var evidence = new Dictionary<string, string>
            {
                { Constants.EVIDENCE_HOST_KEY, "example.com" },
                { "header.protocol", "https" },
            };
            if (signature != null)
            {
                evidence[Constants.EVIDENCE_SIGNATURE_KEY] = signature;
            }
            if (signatureInput != null)
            {
                evidence[Constants.EVIDENCE_SIGNATURE_INPUT_KEY] =
                    signatureInput;
            }

            using (var harness = ElementHarness.Create())
            {
                var result = harness.Process(evidence);
                Assert.AreEqual(
                    Constants.STATUS_INVALID,
                    result.AgentSignature.Value,
                    "A request with " + caseName + " should read '" +
                        Constants.STATUS_INVALID + "', but the reason " +
                        "given was '" + result.AgentSignatureReason.Value +
                        "'.");
                Assert.AreEqual(
                    Constants.REASON_MALFORMED,
                    result.AgentSignatureReason.Value,
                    "A request with " + caseName + " should read the '" +
                        Constants.REASON_MALFORMED + "' reason.");
            }
        }

        /// <summary>
        /// The dictionary parser answers false for field values it cannot
        /// read rather than throwing, whatever is put in front of it.
        /// </summary>
        /// <param name="caseName">The name of the case.</param>
        /// <param name="input">The field value.</param>
        [DataTestMethod]
        [DynamicData(nameof(JunkFieldValues), DynamicDataSourceType.Method)]
        public void JunkIsRefusedRatherThanThrown(
            string caseName,
            string input)
        {
            Assert.IsFalse(
                StructuredFieldParser.TryParseDictionary(input, out var result),
                "A field value with " + caseName + ", being '" +
                    Trim(input) + "', should be refused.");
            Assert.IsNull(
                result,
                "A field value with " + caseName +
                    " should hand back no dictionary at all.");
        }

        /// <summary>
        /// A field value of no characters is an empty dictionary, which
        /// RFC 8941 section 4.2.2 requires, so the parser accepts it. The
        /// element still reads such a request as Malformed, because a
        /// 'Signature-Input' header offering no signature at all is not a
        /// request that can be checked.
        /// </summary>
        [TestMethod]
        public void EmptyFieldValueIsAnEmptyDictionary()
        {
            Assert.IsTrue(
                StructuredFieldParser.TryParseDictionary(
                    string.Empty, out var result),
                "RFC 8941 section 4.2.2 says a field value of no " +
                    "characters is an empty dictionary, so the empty " +
                    "string should be accepted.");
            Assert.AreEqual(
                0,
                result.Count,
                "The empty string should give a dictionary of no members.");

            using (var harness = ElementHarness.Create())
            {
                var outcome = harness.Process(new Dictionary<string, string>
                {
                    { Constants.EVIDENCE_SIGNATURE_KEY, string.Empty },
                    { Constants.EVIDENCE_SIGNATURE_INPUT_KEY, string.Empty },
                    { Constants.EVIDENCE_HOST_KEY, "example.com" },
                    { "header.protocol", "https" },
                });
                Assert.AreEqual(
                    Constants.STATUS_INVALID,
                    outcome.AgentSignature.Value,
                    "A request whose signature headers are empty offers " +
                        "no signature to check, so it should read '" +
                        Constants.STATUS_INVALID + "'.");
                Assert.AreEqual(
                    Constants.REASON_MALFORMED,
                    outcome.AgentSignatureReason.Value,
                    "A request whose signature headers are empty should " +
                        "read the '" + Constants.REASON_MALFORMED +
                        "' reason.");
            }
        }

        /// <summary>
        /// A null field value is refused, because evidence can hold a null
        /// as easily as it can hold text.
        /// </summary>
        [TestMethod]
        public void NullFieldValueIsRefused()
        {
            Assert.IsFalse(
                StructuredFieldParser.TryParseDictionary(null, out var result),
                "A null field value should be refused.");
            Assert.IsNull(
                result,
                "A null field value should hand back no dictionary.");
        }

        /// <summary>
        /// Read one architecture vector.
        /// </summary>
        /// <param name="setName">The fixture the vector came from.</param>
        /// <param name="index">The position of the vector in that fixture.</param>
        /// <returns>The vector.</returns>
        private static SignedRequestVector Vector(string setName, int index)
        {
            var vectors = setName == SetV1
                ? Fixtures.ArchitectureV1()
                : Fixtures.ArchitectureV2();
            Assert.AreEqual(
                4,
                vectors.Count,
                "The " + setName + " fixture should hold four vectors.");
            return vectors[index];
        }

        /// <summary>
        /// Parse the two signature headers of a vector into the one
        /// signature it offers.
        /// </summary>
        /// <param name="vector">The vector.</param>
        /// <param name="caseName">The name of the case.</param>
        /// <returns>The signature the vector offers.</returns>
        private static SignatureCandidate Parse(
            SignedRequestVector vector,
            string caseName)
        {
            Assert.IsTrue(
                StructuredFieldParser.TryParseDictionary(
                    vector.SignatureInput, out var input),
                "The 'Signature-Input' header of " + caseName + ", being '" +
                    vector.SignatureInput + "', should parse.");
            Assert.IsTrue(
                StructuredFieldParser.TryParseDictionary(
                    vector.Signature, out var signature),
                "The 'Signature' header of " + caseName + " should parse.");
            Assert.IsTrue(
                SignatureCandidate.TryBuild(
                    input, signature, out var candidates),
                "The two signature headers of " + caseName +
                    " should pair up into a signature to check.");
            Assert.AreEqual(
                1,
                candidates.Count,
                caseName + " offers one signature, so one should be found.");
            return candidates[0];
        }

        /// <summary>
        /// Build a long field value that opens a quoted string and never
        /// closes it. None of the characters after the opening quote is a
        /// quote, so the string cannot end and the value cannot be read
        /// however long it runs.
        /// </summary>
        /// <returns>The field value.</returns>
        private static string LongUnterminatedString()
        {
            const string alphabet = "abc019 ;=,():@\\|";
            var builder = new StringBuilder("sig1=(\"");
            for (var i = 0; i < 4096; i++)
            {
                builder.Append(alphabet[(i * 7 + 3) % alphabet.Length]);
            }
            return builder.ToString();
        }

        /// <summary>
        /// Write a value that may be null into a failure message.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The text to put in the message.</returns>
        private static string Show(string value)
        {
            return value == null ? "absent" : "'" + value + "'";
        }

        /// <summary>
        /// Shorten a field value so that a failure message stays readable.
        /// </summary>
        /// <param name="value">The field value.</param>
        /// <returns>The text to put in the message.</returns>
        private static string Trim(string value)
        {
            return value.Length <= 60
                ? value
                : value.Substring(0, 60) + "... (" + value.Length +
                    " characters)";
        }
    }
}
