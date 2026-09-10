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

using FiftyOne.Pipeline.AgentSignature.Data;
using FiftyOne.Pipeline.AgentSignature.Tests.Helpers;
using FiftyOne.Pipeline.AgentSignature.Tests.Standard.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Text.Json;

namespace FiftyOne.Pipeline.AgentSignature.Tests.Standard
{
    /// <summary>
    /// Checks the JSON reading paths of the netstandard2.0 build of the
    /// element, which reads with Newtonsoft where the net8.0 build reads
    /// with System.Text.Json. Everything here runs through the element
    /// and its public surface, because the internal reader is only
    /// granted to the main test assembly. The paths that differ between
    /// the two libraries are a fetched directory read end to end, a
    /// document that is not JSON, the three shapes a key's 'nbf' and
    /// 'exp' times arrive in, an agent card whose fields nest, and a
    /// string that looks like a date, which Newtonsoft would turn into a
    /// date value unless told not to.
    /// </summary>
    [TestClass]
    public class NewtonsoftJsonReadingTests
    {
        /// <summary>
        /// When the RFC 9421 test signatures were created, as Unix
        /// seconds. The signing helper writes this fixed point, and the
        /// element checks a key's own 'nbf' and 'exp' limits at it, so
        /// the key time tests do not depend on the present moment.
        /// </summary>
        private const long SigningTime = 1735689600;

        #region A fetched directory read end to end

        /// <summary>
        /// A key directory fetched through the handler parses on the
        /// Newtonsoft reader and the signed request reads Verified, with
        /// the directory's own 'purpose' field carried onto the result.
        /// </summary>
        [TestMethod]
        public void AFetchedDirectoryParsesAndTheSignatureReadsVerified()
        {
            using (var harness = StandardHarness.Create())
            {
                harness.Handler.AddDirectoryWithPurpose(
                    Fixtures.SignatureAgentDirectoryUrl,
                    "ai",
                    RequestSigner.PublicPart(Fixtures.Ed25519Key()));

                var result = harness.ProcessSigned(
                    RequestSigner.Sign(new SigningOptions()));

                AssertVerified(result);
                Assert.IsTrue(
                    result.AgentSignaturePurpose.HasValue,
                    "Expected the purpose read from the directory, and it " +
                    "had no value because '" +
                    result.AgentSignaturePurpose.NoValueMessage + "'.");
                Assert.AreEqual(
                    "ai",
                    result.AgentSignaturePurpose.Value,
                    "Expected the 'purpose' field of the fetched " +
                    "directory on the result. " + Describe(result));
            }
        }

        #endregion

        #region A document that is not JSON

        /// <summary>
        /// A directory that is not JSON reads Unverified with the
        /// DirectoryUnavailable reason rather than throwing, which is the
        /// JsonException catch on the Newtonsoft side of the reader.
        /// </summary>
        [TestMethod]
        public void AMalformedDirectoryReadsUnverifiedRatherThanThrowing()
        {
            using (var harness = StandardHarness.Create())
            {
                harness.Handler.Add(
                    Fixtures.SignatureAgentDirectoryUrl,
                    "{ \"keys\": [ { \"kty\": ",
                    Constants.DIRECTORY_MEDIA_TYPE);

                var result = harness.ProcessSigned(
                    RequestSigner.Sign(new SigningOptions()));

                Assert.AreEqual(
                    Constants.STATUS_UNVERIFIED,
                    result.AgentSignature.Value,
                    "Expected Unverified, because the directory could " +
                    "not be read. " + Describe(result));
                Assert.AreEqual(
                    Constants.REASON_DIRECTORY_UNAVAILABLE,
                    result.AgentSignatureReason.Value,
                    "Expected the DirectoryUnavailable reason, because " +
                    "the directory is not JSON. " + Describe(result));
            }
        }

        #endregion

        #region The three shapes of a key's times

        /// <summary>
        /// A key whose 'nbf' and 'exp' arrive as whole numbers of seconds
        /// around the signing time verifies, so numbers are read on the
        /// Newtonsoft side.
        /// </summary>
        [TestMethod]
        public void KeyTimesAsSecondsAroundTheSigningTimeVerify()
        {
            AssertVerifiedWithKeyTimes(
                "\"nbf\":" + (SigningTime - 600) +
                ",\"exp\":" + (SigningTime + 600));
        }

        /// <summary>
        /// A key whose 'exp' arrives as a whole number of seconds before
        /// the signing time reads KeyExpired, which proves the number was
        /// read rather than dropped, because a dropped limit would leave
        /// the key valid.
        /// </summary>
        [TestMethod]
        public void AKeyExpiryInSecondsBeforeTheSigningTimeIsEnforced()
        {
            AssertKeyExpiredWithKeyTimes(
                "\"exp\":" + (SigningTime - 600));
        }

        /// <summary>
        /// A key whose 'nbf' and 'exp' arrive as milliseconds, the way
        /// the Cloudflare research directory served them, verifies, so
        /// the divide by a thousand is applied to numbers read on the
        /// Newtonsoft side.
        /// </summary>
        [TestMethod]
        public void KeyTimesAsMillisecondsAroundTheSigningTimeVerify()
        {
            AssertVerifiedWithKeyTimes(
                "\"nbf\":" + ((SigningTime - 600) * 1000) +
                ",\"exp\":" + ((SigningTime + 600) * 1000));
        }

        /// <summary>
        /// A key whose 'exp' arrives as milliseconds before the signing
        /// time reads KeyExpired. This proves the divide by a thousand
        /// happened, because a millisecond count read as seconds is a
        /// date the framework cannot hold, and the limit would be
        /// dropped, leaving the key valid.
        /// </summary>
        [TestMethod]
        public void AKeyExpiryInMillisecondsBeforeTheSigningTimeIsEnforced()
        {
            AssertKeyExpiredWithKeyTimes(
                "\"exp\":" + ((SigningTime - 600) * 1000));
        }

        /// <summary>
        /// A key whose 'nbf' and 'exp' arrive as strings of digits
        /// verifies, so the reader's string-to-number branch works on the
        /// values Newtonsoft hands it.
        /// </summary>
        [TestMethod]
        public void KeyTimesAsStringsAroundTheSigningTimeVerify()
        {
            AssertVerifiedWithKeyTimes(
                "\"nbf\":\"" + (SigningTime - 600) +
                "\",\"exp\":\"" + (SigningTime + 600) + "\"");
        }

        /// <summary>
        /// A key whose 'exp' arrives as a string of digits before the
        /// signing time reads KeyExpired, which proves the string was
        /// parsed to a number rather than dropped.
        /// </summary>
        [TestMethod]
        public void AKeyExpiryAsAStringBeforeTheSigningTimeIsEnforced()
        {
            AssertKeyExpiredWithKeyTimes(
                "\"exp\":\"" + (SigningTime - 600) + "\"");
        }

        /// <summary>
        /// A key whose 'exp' arrives with a fractional part before the
        /// signing time reads KeyExpired, so the reader's fractional
        /// number branch works on the values Newtonsoft hands it.
        /// </summary>
        [TestMethod]
        public void AKeyExpiryWithAFractionBeforeTheSigningTimeIsEnforced()
        {
            AssertKeyExpiredWithKeyTimes(
                "\"exp\":" + (SigningTime - 600) + ".5");
        }

        #endregion

        #region An agent card whose fields nest

        /// <summary>
        /// An agent card carrying its keys inline in a 'jwks' object,
        /// a 'contacts' array and the nested 'web_bot_auth' object parses
        /// on the Newtonsoft reader, verifies the signature, and puts the
        /// name, the product token, the purpose and the card URL on the
        /// result. This walks objects inside arrays inside objects, which
        /// the reader converts differently per JSON library.
        /// </summary>
        [TestMethod]
        public void AnAgentCardWithInlineKeysAndNestedFieldsIsRead()
        {
            const string cardUrl = "https://example.com/bot";
            var card =
                "{" +
                "\"client_id\":\"" + cardUrl + "\"," +
                "\"client_name\":\"Standard Bot\"," +
                "\"contacts\":[\"mailto:bot-support@example.com\"]," +
                "\"jwks\":{\"keys\":[" +
                RequestSigner.PublicPart(Fixtures.Ed25519Key()) +
                "]}," +
                "\"web_bot_auth\":{" +
                "\"rfc9309-product-token\":\"StandardBot\"," +
                "\"purpose\":\"tdm\"," +
                "\"trigger\":\"fetcher\"" +
                "}" +
                "}";
            using (var harness = StandardHarness.Create())
            {
                harness.Handler.Add(
                    cardUrl, card, Constants.JSON_MEDIA_TYPE);

                var member = "\"" + cardUrl + "\";type=" +
                    Constants.AGENT_TYPE_CIMD;
                var options = new SigningOptions { SignatureAgent = null };
                options.ExtraComponents.Add(
                    new KeyValuePair<string, string>(
                        "\"signature-agent\";key=\"agent1\"", member));
                var signed = RequestSigner.Sign(options);

                var result = harness.Process(
                    new Dictionary<string, string>
                    {
                        {
                            Constants.EVIDENCE_SIGNATURE_KEY,
                            signed.Signature
                        },
                        {
                            Constants.EVIDENCE_SIGNATURE_INPUT_KEY,
                            signed.SignatureInput
                        },
                        {
                            Constants.EVIDENCE_SIGNATURE_AGENT_KEY,
                            "agent1=" + member
                        },
                        { Constants.EVIDENCE_HOST_KEY, "example.com" },
                        { Core.Constants.EVIDENCE_PROTOCOL, "https" },
                    });

                AssertVerified(result);
                AssertValue(
                    "Standard Bot",
                    result.AgentSignatureName,
                    "name",
                    result);
                AssertValue(
                    "StandardBot",
                    result.AgentSignatureProductToken,
                    "product token",
                    result);
                AssertValue(
                    "tdm",
                    result.AgentSignaturePurpose,
                    "purpose",
                    result);
                AssertValue(
                    cardUrl,
                    result.AgentSignatureCardUrl,
                    "card URL",
                    result);
            }
        }

        #endregion

        #region A string that looks like a date

        /// <summary>
        /// A key id that reads as a date stays the exact string the
        /// directory served, so the signature naming that key id
        /// verifies. The Newtonsoft reader turns date-shaped strings into
        /// date values unless date parsing is switched off, and a key id
        /// turned into a date would no longer match anything, so this
        /// failing means date parsing has been switched back on.
        /// </summary>
        [TestMethod]
        public void AKeyIdThatLooksLikeADateStaysAString()
        {
            const string dateLikeKeyId = "2026-01-01T10:00:00Z";
            using (var harness = StandardHarness.Create())
            {
                harness.Handler.AddDirectory(
                    Fixtures.SignatureAgentDirectoryUrl,
                    Ed25519PublicKeyJson(dateLikeKeyId, null));

                var result = harness.ProcessSigned(
                    RequestSigner.Sign(new SigningOptions
                    {
                        KeyId = dateLikeKeyId,
                    }));

                AssertVerified(result);
                Assert.AreEqual(
                    dateLikeKeyId,
                    result.AgentSignatureKeyId.Value,
                    "Expected the key id exactly as the request sent " +
                    "it. " + Describe(result));
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Serve the Ed25519 test key with the given time fields and
        /// check that the signed request verifies.
        /// </summary>
        /// <param name="timeFields">
        /// The 'nbf' and 'exp' fields as JSON text, without braces.
        /// </param>
        private static void AssertVerifiedWithKeyTimes(string timeFields)
        {
            var result = ProcessWithKeyTimes(timeFields, out var harness);
            using (harness)
            {
                AssertVerified(result);
            }
        }

        /// <summary>
        /// Serve the Ed25519 test key with the given time fields and
        /// check that the signature reads Invalid with the KeyExpired
        /// reason, because the key was not valid when the signature was
        /// created.
        /// </summary>
        /// <param name="timeFields">
        /// The 'nbf' and 'exp' fields as JSON text, without braces.
        /// </param>
        private static void AssertKeyExpiredWithKeyTimes(string timeFields)
        {
            var result = ProcessWithKeyTimes(timeFields, out var harness);
            using (harness)
            {
                Assert.AreEqual(
                    Constants.STATUS_INVALID,
                    result.AgentSignature.Value,
                    "Expected Invalid for the key limits '" + timeFields +
                    "', because the key had stopped being valid before " +
                    "the signature was created. " + Describe(result));
                Assert.AreEqual(
                    Constants.REASON_KEY_EXPIRED,
                    result.AgentSignatureReason.Value,
                    "Expected the KeyExpired reason for the key limits '" +
                    timeFields + "'. A missing reason here means the " +
                    "limit was dropped while being read rather than " +
                    "enforced. " + Describe(result));
            }
        }

        /// <summary>
        /// Serve the Ed25519 test key with the given time fields and run
        /// a request signed with it.
        /// </summary>
        /// <param name="timeFields">
        /// The 'nbf' and 'exp' fields as JSON text, without braces.
        /// </param>
        /// <param name="harness">
        /// The harness used, which the caller disposes.
        /// </param>
        /// <returns>What the element made of the request.</returns>
        private static IAgentSignatureData ProcessWithKeyTimes(
            string timeFields,
            out StandardHarness harness)
        {
            harness = StandardHarness.Create();
            harness.Handler.AddDirectory(
                Fixtures.SignatureAgentDirectoryUrl,
                Ed25519PublicKeyJson("test-key-ed25519", timeFields));
            return harness.ProcessSigned(
                RequestSigner.Sign(new SigningOptions()));
        }

        /// <summary>
        /// Build the public part of the RFC 9421 Ed25519 test key as
        /// JSON, with the key id given and any further fields appended.
        /// </summary>
        /// <param name="keyId">The 'kid' to state.</param>
        /// <param name="extraFields">
        /// Further fields as JSON text without braces, or null for none.
        /// </param>
        /// <returns>The key as JSON.</returns>
        private static string Ed25519PublicKeyJson(
            string keyId,
            string extraFields)
        {
            using (var document = JsonDocument.Parse(
                Fixtures.Ed25519Key()))
            {
                var x = document.RootElement.GetProperty("x").GetString();
                return "{\"kty\":\"OKP\",\"crv\":\"Ed25519\"," +
                    "\"kid\":\"" + keyId + "\",\"x\":\"" + x + "\"" +
                    (extraFields == null ? "" : "," + extraFields) +
                    "}";
            }
        }

        /// <summary>
        /// Check that the signature verified.
        /// </summary>
        /// <param name="result">What the element made of the request.</param>
        private static void AssertVerified(IAgentSignatureData result)
        {
            Assert.AreEqual(
                Constants.STATUS_VERIFIED,
                result.AgentSignature.Value,
                "Expected Verified, because the request was signed with " +
                "the key the agent publishes. " + Describe(result));
            Assert.AreEqual(
                Constants.REASON_VERIFIED,
                result.AgentSignatureReason.Value,
                "Expected the Verified reason. " + Describe(result));
        }

        /// <summary>
        /// Check that a property carries the value expected.
        /// </summary>
        /// <param name="expected">The value expected.</param>
        /// <param name="actual">The property.</param>
        /// <param name="description">What the property holds.</param>
        /// <param name="result">What the element made of the request.</param>
        private static void AssertValue(
            string expected,
            Engines.Data.IAspectPropertyValue<string> actual,
            string description,
            IAgentSignatureData result)
        {
            Assert.IsTrue(
                actual.HasValue,
                "Expected the " + description + " to be '" + expected +
                "', and it had no value because '" +
                actual.NoValueMessage + "'. " + Describe(result));
            Assert.AreEqual(
                expected,
                actual.Value,
                "Expected the " + description + " to be '" + expected +
                "', and it was '" + actual.Value + "'. " +
                Describe(result));
        }

        /// <summary>
        /// Describe what the element made of a request, so that every
        /// assertion message carries the status and the reason.
        /// </summary>
        /// <param name="result">What the element made of the request.</param>
        /// <returns>The description.</returns>
        private static string Describe(IAgentSignatureData result)
        {
            return "The status was '" + result.AgentSignature.Value +
                "' with the reason '" +
                result.AgentSignatureReason.Value + "'.";
        }

        #endregion
    }
}
