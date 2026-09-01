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
using FiftyOne.Pipeline.AgentSignature.Keys;
using FiftyOne.Pipeline.AgentSignature.Tests.Helpers;
using FiftyOne.Pipeline.AgentSignature.Verification;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace FiftyOne.Pipeline.AgentSignature.Tests
{
    /// <summary>
    /// Checks what the element makes of a request signature, covering the
    /// signed request vectors that the Web Bot Auth drafts publish and the
    /// failures that a test has to build for itself, being a changed
    /// signature, a changed host, a wrong tag, a missing parameter, times
    /// that have not arrived, a key the agent does not publish, a key that
    /// had already stopped being valid, an algorithm this element does not
    /// verify, a covered component the pipeline cannot rebuild and a
    /// request that carries no signature at all. The signed key directory
    /// response and the key times a real directory served in milliseconds
    /// are checked here as well, because both decide whether a signature
    /// can be verified.
    /// </summary>
    [TestClass]
    public class VerificationTests
    {
        /// <summary>
        /// The time the four architecture v1 signatures were made, which is
        /// 1 January 2025. Their signatures last one hour from then, so the
        /// harness clock has to be moved back to this point for them to be
        /// anything other than expired.
        /// </summary>
        private static readonly DateTimeOffset SigningTime =
            DateTimeOffset.FromUnixTimeSeconds(1735689700);

        /// <summary>
        /// The wording each detail property carries when the request sent no
        /// signature headers at all. Taken from Messages.resx in the element
        /// project.
        /// </summary>
        private const string AbsentDetailMessage =
            "No signature headers were present in the request";

        /// <summary>
        /// The wording the purpose property carries when the key directory
        /// was never read. Taken from Messages.resx.
        /// </summary>
        private const string PurposeNotReadMessage =
            "The signature was not verified so the directory was not read";

        /// <summary>
        /// The wording the three agent card properties carry when no card
        /// was found. Taken from Messages.resx.
        /// </summary>
        private const string NoCardMessage = "No agent card available";

        #region Data sources

        /// <summary>
        /// The architecture v2 vectors that carry a 'Signature-Agent'
        /// header, being one Ed25519 vector and one RSA-PSS vector.
        /// </summary>
        /// <returns>The vectors.</returns>
        public static IEnumerable<object[]> V2VectorsWithAnAgent()
        {
            return Fixtures.ArchitectureV2()
                .Where(v => v.SignatureAgent != null)
                .Select(v => new object[] { v });
        }

        /// <summary>
        /// The architecture v2 vectors that carry no 'Signature-Agent'
        /// header, being one Ed25519 vector and one RSA-PSS vector.
        /// </summary>
        /// <returns>The vectors.</returns>
        public static IEnumerable<object[]> V2VectorsWithoutAnAgent()
        {
            return Fixtures.ArchitectureV2()
                .Where(v => v.SignatureAgent == null)
                .Select(v => new object[] { v });
        }

        /// <summary>
        /// All four architecture v1 vectors.
        /// </summary>
        /// <returns>The vectors.</returns>
        public static IEnumerable<object[]> V1Vectors()
        {
            return Fixtures.ArchitectureV1()
                .Select(v => new object[] { v });
        }

        #endregion

        #region The published vectors

        /// <summary>
        /// Every architecture v2 vector that carries a 'Signature-Agent'
        /// header verifies against a directory serving the vector's own
        /// public key, and each detail property carries the value the
        /// vector itself holds. Both the Ed25519 and the RSA-PSS vector run
        /// through here, so both algorithms are checked.
        /// </summary>
        /// <param name="vector">The vector to run.</param>
        [DataTestMethod]
        [DynamicData(
            nameof(V2VectorsWithAnAgent), DynamicDataSourceType.Method)]
        public void V2VectorWithAnAgentIsVerified(SignedRequestVector vector)
        {
            using (var harness = ElementHarness.Create())
            {
                harness.Handler.AddDirectory(
                    Fixtures.SignatureAgentDirectoryUrl,
                    RequestSigner.PublicPart(vector.KeyJson));
                var result = harness.ProcessVector(vector);

                AssertOutcome(
                    result,
                    Constants.STATUS_VERIFIED,
                    Constants.REASON_VERIFIED,
                    Describe(vector));
                AssertText(
                    result.AgentSignatureAgent,
                    Fixtures.SignatureAgentOrigin,
                    "agent",
                    vector,
                    result);
                AssertText(
                    result.AgentSignatureKeyId,
                    vector.KeyId,
                    "key id",
                    vector,
                    result);
                AssertText(
                    result.AgentSignatureAlgorithm,
                    vector.Algorithm,
                    "algorithm",
                    vector,
                    result);
                AssertText(
                    result.AgentSignatureNonce,
                    vector.Nonce,
                    "nonce",
                    vector,
                    result);
                AssertTime(
                    result.AgentSignatureCreated,
                    FromMilliseconds(vector.CreatedMs),
                    "created time",
                    vector,
                    result);
                AssertTime(
                    result.AgentSignatureExpires,
                    FromMilliseconds(vector.ExpiresMs),
                    "expiry time",
                    vector,
                    result);
            }
        }

        /// <summary>
        /// The two architecture v2 vectors that cover '@authority' only send
        /// no 'Signature-Agent' header at all. A signature that names no
        /// agent gives this element nowhere to fetch a key from, so there is
        /// nothing to check the signature against and the only honest answer
        /// is Unverified with the NoAgent reason. Unverified is not evidence
        /// against the agent, which matters because a request that names no
        /// agent may still be from a well behaved one.
        /// </summary>
        /// <param name="vector">The vector to run.</param>
        [DataTestMethod]
        [DynamicData(
            nameof(V2VectorsWithoutAnAgent), DynamicDataSourceType.Method)]
        public void V2VectorWithoutAnAgentIsUnverified(
            SignedRequestVector vector)
        {
            Assert.IsNull(
                vector.SignatureAgent,
                Describe(vector) + " was expected to send no " +
                "'Signature-Agent' header, so the fixture has changed.");
            using (var harness = ElementHarness.Create())
            {
                harness.Handler.AddDirectory(
                    Fixtures.SignatureAgentDirectoryUrl,
                    RequestSigner.PublicPart(vector.KeyJson));
                var result = harness.ProcessVector(vector);

                AssertOutcome(
                    result,
                    Constants.STATUS_UNVERIFIED,
                    Constants.REASON_NO_AGENT,
                    Describe(vector));
                Assert.AreEqual(
                    0,
                    harness.Handler.CallCount,
                    Describe(vector) + " names no agent, so no key " +
                    "directory should have been fetched, yet " +
                    harness.Handler.CallCount + " request(s) were made.");
            }
        }

        /// <summary>
        /// The architecture v1 signatures were made on 1 January 2025 and
        /// last one hour, so at the real current time every one of them
        /// reads Invalid with the Expired reason whether or not it names an
        /// agent.
        /// </summary>
        /// <param name="vector">The vector to run.</param>
        [DataTestMethod]
        [DynamicData(nameof(V1Vectors), DynamicDataSourceType.Method)]
        public void V1VectorHasExpiredAtTheCurrentTime(
            SignedRequestVector vector)
        {
            using (var harness = ElementHarness.Create())
            {
                harness.Now = DateTimeOffset.UtcNow;
                harness.Handler.AddDirectory(
                    Fixtures.SignatureAgentDirectoryUrl,
                    RequestSigner.PublicPart(vector.KeyJson));
                var result = harness.ProcessVector(vector);

                AssertOutcome(
                    result,
                    Constants.STATUS_INVALID,
                    Constants.REASON_EXPIRED,
                    Describe(vector) + " at the current time");
            }
        }

        /// <summary>
        /// With the clock moved back to the hour the architecture v1
        /// signatures were made, the two that carry a 'Signature-Agent'
        /// header verify and the two that carry none read Unverified with
        /// the NoAgent reason. The two that verify send the header in the
        /// bare quoted string form of the earlier drafts and cover the plain
        /// 'signature-agent' component rather than one member of it, so this
        /// checks the legacy shape as well as the times.
        /// </summary>
        /// <param name="vector">The vector to run.</param>
        [DataTestMethod]
        [DynamicData(nameof(V1Vectors), DynamicDataSourceType.Method)]
        public void V1VectorReadsItsOutcomeAtTheTimeItWasSigned(
            SignedRequestVector vector)
        {
            var carriesAnAgent = vector.SignatureAgent != null;
            if (carriesAnAgent)
            {
                Assert.IsFalse(
                    vector.SignatureAgent.Contains("="),
                    Describe(vector) + " was expected to send the bare " +
                    "quoted string form of the 'Signature-Agent' header, " +
                    "yet it sent '" + vector.SignatureAgent + "'.");
            }
            using (var harness = ElementHarness.Create())
            {
                harness.Now = SigningTime;
                harness.Handler.AddDirectory(
                    Fixtures.SignatureAgentDirectoryUrl,
                    RequestSigner.PublicPart(vector.KeyJson));
                var result = harness.ProcessVector(vector);

                AssertOutcome(
                    result,
                    carriesAnAgent
                        ? Constants.STATUS_VERIFIED
                        : Constants.STATUS_UNVERIFIED,
                    carriesAnAgent
                        ? Constants.REASON_VERIFIED
                        : Constants.REASON_NO_AGENT,
                    Describe(vector) + " with the clock at the time it " +
                    "was signed");
                if (carriesAnAgent)
                {
                    AssertText(
                        result.AgentSignatureAgent,
                        Fixtures.SignatureAgentOrigin,
                        "agent",
                        vector,
                        result);
                }
            }
        }

        #endregion

        #region Signatures that do not check out

        /// <summary>
        /// Changing one byte of the signature itself means the signature no
        /// longer checks out against the key the agent publishes, which
        /// reads Invalid with the SignatureMismatch reason.
        /// </summary>
        [TestMethod]
        public void ChangedSignatureByteIsSignatureMismatch()
        {
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var signed = RequestSigner.Sign(new SigningOptions());
                var tampered = WithChangedSignatureByte(signed);
                var result = harness.ProcessSigned(tampered);

                AssertOutcome(
                    result,
                    Constants.STATUS_INVALID,
                    Constants.REASON_SIGNATURE_MISMATCH,
                    "A request whose signature had one byte changed");
            }
        }

        /// <summary>
        /// Changing one character of the host means the '@authority' line of
        /// the signature base no longer matches the one the agent signed, so
        /// the signature reads Invalid with the SignatureMismatch reason.
        /// This is the check that stops a signature being replayed against a
        /// different site.
        /// </summary>
        [TestMethod]
        public void ChangedHostIsSignatureMismatch()
        {
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var signed = RequestSigner.Sign(new SigningOptions
                {
                    Host = "example.com",
                });
                var result = harness.ProcessSigned(signed, "examp1e.com");

                AssertOutcome(
                    result,
                    Constants.STATUS_INVALID,
                    Constants.REASON_SIGNATURE_MISMATCH,
                    "A request signed for 'example.com' and sent to " +
                    "'examp1e.com'");
            }
        }

        /// <summary>
        /// A signature that carries a tag other than 'web-bot-auth' was not
        /// made to say that an automated agent sent the request, so the
        /// element reads Invalid with the TagMismatch reason rather than
        /// checking it.
        /// </summary>
        [TestMethod]
        public void OtherTagIsTagMismatch()
        {
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var signed = RequestSigner.Sign(new SigningOptions
                {
                    Tag = "some-other-purpose",
                });
                var result = harness.ProcessSigned(signed);

                AssertOutcome(
                    result,
                    Constants.STATUS_INVALID,
                    Constants.REASON_TAG_MISMATCH,
                    "A signature tagged 'some-other-purpose'");
            }
        }

        /// <summary>
        /// A signature with no 'keyid' parameter does not say which key it
        /// was made with, so there is nothing to look up and the element
        /// reads Invalid with the MissingParameter reason.
        /// </summary>
        [TestMethod]
        public void MissingKeyIdIsMissingParameter()
        {
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var signed = RequestSigner.Sign(new SigningOptions
                {
                    OmitKeyId = true,
                });
                var result = harness.ProcessSigned(signed);

                AssertOutcome(
                    result,
                    Constants.STATUS_INVALID,
                    Constants.REASON_MISSING_PARAMETER,
                    "A signature sent with no 'keyid' parameter");
            }
        }

        /// <summary>
        /// A signature created further into the future than the clock skew
        /// allows cannot yet be in use, so it reads Invalid with the
        /// NotYetValid reason. The default skew is sixty seconds and this
        /// signature is created ten minutes ahead of the element's clock.
        /// </summary>
        [TestMethod]
        public void CreatedBeyondTheSkewIsNotYetValid()
        {
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var created = harness.Now.AddMinutes(10);
                var signed = RequestSigner.Sign(new SigningOptions
                {
                    Created = created,
                    Expires = created.AddHours(1),
                });
                var result = harness.ProcessSigned(signed);

                AssertOutcome(
                    result,
                    Constants.STATUS_INVALID,
                    Constants.REASON_NOT_YET_VALID,
                    "A signature created ten minutes ahead of the clock");
            }
        }

        #endregion

        #region Keys the directory does not support

        /// <summary>
        /// A directory that does not publish the key the signature names is
        /// evidence that the key was withdrawn, so the element reads Invalid
        /// with the UnknownKey reason. Here the request is signed with the
        /// Ed25519 test key whilst the directory publishes the RSA one.
        /// </summary>
        [TestMethod]
        public void DirectoryWithoutTheNamedKeyIsUnknownKey()
        {
            using (var harness = ElementHarness.Create())
            {
                harness.Handler.AddDirectory(
                    Fixtures.SignatureAgentDirectoryUrl,
                    RequestSigner.PublicPart(Fixtures.RsaKey()));
                var signed = RequestSigner.Sign(new SigningOptions());
                var result = harness.ProcessSigned(signed);

                AssertOutcome(
                    result,
                    Constants.STATUS_INVALID,
                    Constants.REASON_UNKNOWN_KEY,
                    "A signature naming a key the directory does not hold");
            }
        }

        /// <summary>
        /// A key whose 'exp' had already passed when the signature was made
        /// was not a key the agent was entitled to sign with at that moment,
        /// so the element reads Invalid with the KeyExpired reason.
        /// </summary>
        [TestMethod]
        public void KeyThatExpiredBeforeTheSignatureIsKeyExpired()
        {
            var created = DateTimeOffset.FromUnixTimeSeconds(1735689600);
            var expiredKey = WithMember(
                RequestSigner.PublicPart(Fixtures.Ed25519Key()),
                "exp",
                (created.ToUnixTimeSeconds() - 3600).ToString(
                    CultureInfo.InvariantCulture));
            using (var harness = ElementHarness.Create())
            {
                harness.Handler.AddDirectory(
                    Fixtures.SignatureAgentDirectoryUrl, expiredKey);
                var signed = RequestSigner.Sign(new SigningOptions
                {
                    Created = created,
                });
                var result = harness.ProcessSigned(signed);

                AssertOutcome(
                    result,
                    Constants.STATUS_INVALID,
                    Constants.REASON_KEY_EXPIRED,
                    "A signature made an hour after its key expired");
            }
        }

        /// <summary>
        /// A key of type 'oct' is a shared secret, which the Web Bot Auth
        /// protocol forbids for signing requests, so the element checks
        /// nothing and reads Unverified with the UnsupportedAlgorithm
        /// reason.
        /// </summary>
        [TestMethod]
        public void SharedSecretKeyTypeIsUnsupportedAlgorithm()
        {
            var sharedSecretKey =
                "{\"kty\":\"oct\",\"kid\":\"" +
                Fixtures.Ed25519Thumbprint +
                "\",\"k\":\"c2hhcmVkLXNlY3JldA\"}";
            using (var harness = ElementHarness.Create())
            {
                harness.Handler.AddDirectory(
                    Fixtures.SignatureAgentDirectoryUrl, sharedSecretKey);
                var signed = RequestSigner.Sign(new SigningOptions
                {
                    Algorithm = null,
                });
                var result = harness.ProcessSigned(signed);

                AssertOutcome(
                    result,
                    Constants.STATUS_UNVERIFIED,
                    Constants.REASON_UNSUPPORTED_ALGORITHM,
                    "A directory publishing a key of type 'oct'");
            }
        }

        /// <summary>
        /// A key that names the 'HS256' algorithm is a shared secret as
        /// well, whatever its type says, so it also reads Unverified with
        /// the UnsupportedAlgorithm reason.
        /// </summary>
        [TestMethod]
        public void SharedSecretKeyAlgorithmIsUnsupportedAlgorithm()
        {
            var sharedSecretKey = WithMember(
                RequestSigner.PublicPart(Fixtures.Ed25519Key()),
                "alg",
                "\"HS256\"");
            using (var harness = ElementHarness.Create())
            {
                harness.Handler.AddDirectory(
                    Fixtures.SignatureAgentDirectoryUrl, sharedSecretKey);
                var signed = RequestSigner.Sign(new SigningOptions
                {
                    Algorithm = null,
                });
                var result = harness.ProcessSigned(signed);

                AssertOutcome(
                    result,
                    Constants.STATUS_UNVERIFIED,
                    Constants.REASON_UNSUPPORTED_ALGORITHM,
                    "A directory publishing a key whose 'alg' is 'HS256'");
            }
        }

        #endregion

        #region Components and absent signatures

        /// <summary>
        /// A signature that covers '@target-uri' cannot be rebuilt, because
        /// the web integration puts the request headers into evidence but
        /// not the request line, so there is no way to know the URI the
        /// agent asked for. The element says so with Unverified and the
        /// ComponentUnavailable reason rather than reporting a mismatch,
        /// because the signature may well be sound.
        /// </summary>
        [TestMethod]
        public void ExtraCoveredComponentIsComponentUnavailable()
        {
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var options = new SigningOptions();
                options.ExtraComponents.Add(
                    new KeyValuePair<string, string>(
                        "\"@target-uri\"",
                        "https://example.com/path/to/resource"));
                var signed = RequestSigner.Sign(options);
                var result = harness.ProcessSigned(signed);

                AssertOutcome(
                    result,
                    Constants.STATUS_UNVERIFIED,
                    Constants.REASON_COMPONENT_UNAVAILABLE,
                    "A signature covering '@target-uri'");
            }
        }

        /// <summary>
        /// A request with no signature headers reads Absent with the
        /// NoSignature reason, which is the normal case, and every one of
        /// the ten detail properties has no value and says why. The whole
        /// property set is read back as a dictionary so that a property
        /// added later cannot slip through without a message.
        /// </summary>
        [TestMethod]
        public void RequestWithNoSignatureSaysWhyEachDetailHasNoValue()
        {
            using (var harness = ElementHarness.Create())
            {
                var values = harness.ProcessAsDictionary(
                    new Dictionary<string, string>
                    {
                        { "header.host", "example.com" },
                        { "header.protocol", "https" },
                    });

                Assert.AreEqual(
                    12,
                    values.Count,
                    "A request with no signature should report all twelve " +
                    "properties, yet it reported " + values.Count + ".");
                AssertTextValue(
                    values,
                    Constants.PROPERTY_STATUS,
                    Constants.STATUS_ABSENT);
                AssertTextValue(
                    values,
                    Constants.PROPERTY_REASON,
                    Constants.REASON_NO_SIGNATURE);

                AssertNoTextValue(
                    values, Constants.PROPERTY_AGENT, AbsentDetailMessage);
                AssertNoTextValue(
                    values, Constants.PROPERTY_KEY_ID, AbsentDetailMessage);
                AssertNoTextValue(
                    values,
                    Constants.PROPERTY_ALGORITHM,
                    AbsentDetailMessage);
                AssertNoTextValue(
                    values, Constants.PROPERTY_NONCE, AbsentDetailMessage);
                AssertNoTimeValue(
                    values, Constants.PROPERTY_CREATED, AbsentDetailMessage);
                AssertNoTimeValue(
                    values, Constants.PROPERTY_EXPIRES, AbsentDetailMessage);
                AssertNoTextValue(
                    values,
                    Constants.PROPERTY_PURPOSE,
                    PurposeNotReadMessage);
                AssertNoTextValue(
                    values, Constants.PROPERTY_NAME, NoCardMessage);
                AssertNoTextValue(
                    values,
                    Constants.PROPERTY_PRODUCT_TOKEN,
                    NoCardMessage);
                AssertNoTextValue(
                    values, Constants.PROPERTY_CARD_URL, NoCardMessage);
            }
        }

        #endregion

        #region The signed key directory response

        /// <summary>
        /// The key directory response the standard publishes is signed over
        /// its own content digest. Served exactly as the vector records it,
        /// the element accepts the directory and a request signed with the
        /// key it holds reads Verified.
        /// </summary>
        [TestMethod]
        public void SignedDirectoryResponseIsAcceptedAndVerifies()
        {
            var vector = Fixtures.DirectoryResponse();
            using (var harness = ElementHarness.Create())
            {
                harness.Handler.Add(
                    vector.TargetUrl, BuildDirectoryResponse(vector.Body,
                        vector));
                var signed = RequestSigner.Sign(new SigningOptions());
                var result = harness.ProcessSigned(signed);

                AssertOutcome(
                    result,
                    Constants.STATUS_VERIFIED,
                    Constants.REASON_VERIFIED,
                    "A request checked against the signed directory " +
                    "response vector '" + vector.Name + "'");
            }
        }

        /// <summary>
        /// Changing the body of that directory response, whilst leaving its
        /// content digest and signature alone, means the response no longer
        /// stands up. The element throws the directory away and reads
        /// Unverified with the DirectoryUnavailable reason. The change here
        /// is one space inside the JSON, so the keys the directory holds are
        /// the same and the digest is the only thing that can have failed.
        /// </summary>
        [TestMethod]
        public void TamperedDirectoryBodyIsDirectoryUnavailable()
        {
            var vector = Fixtures.DirectoryResponse();
            var tamperedBody = vector.Body.Replace(
                "{\"keys\"", "{ \"keys\"");
            Assert.AreNotEqual(
                vector.Body,
                tamperedBody,
                "The directory response vector '" + vector.Name +
                "' no longer holds the text this test changes, so the " +
                "fixture has changed.");
            using (var harness = ElementHarness.Create())
            {
                harness.Handler.Add(
                    vector.TargetUrl,
                    BuildDirectoryResponse(tamperedBody, vector));
                var signed = RequestSigner.Sign(new SigningOptions());
                var result = harness.ProcessSigned(signed);

                AssertOutcome(
                    result,
                    Constants.STATUS_UNVERIFIED,
                    Constants.REASON_DIRECTORY_UNAVAILABLE,
                    "A request checked against a directory response whose " +
                    "body no longer matches its content digest");
            }
        }

        /// <summary>
        /// The fetcher rebuilds the same signature base over the directory
        /// response that the vector records in its 'signature_base' field.
        /// The fetcher does not hand the text it built back to a caller, so
        /// this is shown in two steps. The fetcher accepts the response,
        /// which it only does when the Ed25519 signature checks out against
        /// the text it built, and the same signature also checks out against
        /// the text the vector records. One Ed25519 signature cannot check
        /// out against two different texts, so the two texts are the same.
        /// </summary>
        [TestMethod]
        public void DirectoryResponseSignatureBaseMatchesTheVector()
        {
            var vector = Fixtures.DirectoryResponse();
            var body = Encoding.UTF8.GetBytes(vector.Body);
            Assert.IsTrue(
                KeyDirectory.TryParse(vector.Body, out var directory),
                "The body of the directory response vector '" +
                vector.Name + "' could not be read as a key directory.");

            using (var client = new HttpClient(new FakeHttpHandler(), true))
            using (var response = BuildResponseMessage(vector, body))
            {
                // A response the fetcher sees no signature on is accepted
                // without any checking, so the headers have to be there for
                // this test to prove anything.
                AssertHeaderPresent(response, "signature");
                AssertHeaderPresent(response, "signature-input");
                AssertHeaderPresent(response, "content-digest");
                var fetcher = new DirectoryFetcher(
                    client, NullLogger.Instance, () => SigningTime);
                var accepted = fetcher.TryVerifyResponseSignature(
                    vector.TargetUrl,
                    response,
                    body,
                    directory,
                    out var failure);
                Assert.IsTrue(
                    accepted,
                    "The signature over the directory response vector '" +
                    vector.Name + "' should have been accepted, yet it " +
                    "was refused because " + (failure ?? "no reason given") +
                    ".");
            }

            var key = directory.FindKey(Fixtures.Ed25519Thumbprint);
            Assert.IsNotNull(
                key,
                "The directory response vector '" + vector.Name +
                "' should publish the Ed25519 test key with thumbprint '" +
                Fixtures.Ed25519Thumbprint + "', and it does not.");
            Assert.IsTrue(
                SignatureVerifier.Verify(
                    Constants.ALGORITHM_ED25519,
                    key,
                    Encoding.ASCII.GetBytes(vector.SignatureBase),
                    ReadSignatureBytes(vector.Headers["Signature"])),
                "The signature the directory response vector '" +
                vector.Name + "' records should check out against the " +
                "signature base the same vector records, and it does not.");
        }

        #endregion

        #region Key times in milliseconds

        /// <summary>
        /// The Cloudflare research directory served its key times in
        /// milliseconds where the drafts use seconds. A key whose 'nbf' is
        /// 1743465600000 is read as 1 April 2025 rather than being thrown
        /// away, so a request signed after that date verifies. The parsed
        /// directory is checked as well, because a reader that simply gave
        /// up on the large number would also let the request through.
        /// </summary>
        [TestMethod]
        public void KeyTimesInMillisecondsAreReadAsSeconds()
        {
            const long notBeforeMs = 1743465600000L;
            var keyJson = WithMember(
                RequestSigner.PublicPart(Fixtures.Ed25519Key()),
                "nbf",
                notBeforeMs.ToString(CultureInfo.InvariantCulture));
            var now = DateTimeOffset.FromUnixTimeSeconds(1767225600);

            Assert.IsTrue(
                KeyDirectory.TryParse(
                    "{\"keys\":[" + keyJson + "]}", out var directory),
                "The key with 'nbf' " + notBeforeMs +
                " could not be read as a key directory.");
            Assert.IsTrue(
                directory.TimesWereInMilliseconds,
                "The key with 'nbf' " + notBeforeMs + " should have been " +
                "read as milliseconds, and it was not.");
            Assert.AreEqual(
                DateTimeOffset.FromUnixTimeSeconds(notBeforeMs / 1000),
                directory.Keys[0].NotBefore,
                "The key with 'nbf' " + notBeforeMs + " should be valid " +
                "from 1 April 2025, yet it reads " +
                directory.Keys[0].NotBefore + ".");

            using (var harness = ElementHarness.Create())
            {
                harness.Now = now;
                harness.Handler.AddDirectory(
                    Fixtures.SignatureAgentDirectoryUrl, keyJson);
                var signed = RequestSigner.Sign(new SigningOptions
                {
                    Created = now.AddSeconds(-10),
                });
                var result = harness.ProcessSigned(signed);

                AssertOutcome(
                    result,
                    Constants.STATUS_VERIFIED,
                    Constants.REASON_VERIFIED,
                    "A request checked against a key whose 'nbf' is the " +
                    "millisecond value " + notBeforeMs);
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Name a vector in a way a reader can find it in the fixture file.
        /// </summary>
        private static string Describe(SignedRequestVector vector)
        {
            return "The vector labelled '" + vector.Label + "' signed with " +
                vector.Algorithm +
                (vector.SignatureAgent == null
                    ? " sending no 'Signature-Agent' header"
                    : " naming the agent " + vector.SignatureAgent);
        }

        private static DateTimeOffset FromMilliseconds(long milliseconds)
        {
            return DateTimeOffset.FromUnixTimeSeconds(milliseconds / 1000);
        }

        private static void AssertOutcome(
            IAgentSignatureData result,
            string status,
            string reason,
            string what)
        {
            Assert.AreEqual(
                status,
                result.AgentSignature.Value,
                what + " should read the status " + status + ", yet it " +
                "read " + result.AgentSignature.Value + " with the reason " +
                result.AgentSignatureReason.Value + ".");
            Assert.AreEqual(
                reason,
                result.AgentSignatureReason.Value,
                what + " should give the reason " + reason + ", yet it " +
                "gave " + result.AgentSignatureReason.Value + " with the " +
                "status " + result.AgentSignature.Value + ".");
        }

        private static void AssertText(
            IAspectPropertyValue<string> actual,
            string expected,
            string name,
            SignedRequestVector vector,
            IAgentSignatureData result)
        {
            Assert.IsTrue(
                actual.HasValue,
                Describe(vector) + " should report the " + name + " '" +
                expected + "', yet the property has no value because '" +
                actual.NoValueMessage + "'. The reason was " +
                result.AgentSignatureReason.Value + ".");
            Assert.AreEqual(
                expected,
                actual.Value,
                Describe(vector) + " should report the " + name + " '" +
                expected + "', yet it reported '" + actual.Value +
                "'. The reason was " + result.AgentSignatureReason.Value +
                ".");
        }

        private static void AssertTime(
            IAspectPropertyValue<DateTimeOffset> actual,
            DateTimeOffset expected,
            string name,
            SignedRequestVector vector,
            IAgentSignatureData result)
        {
            Assert.IsTrue(
                actual.HasValue,
                Describe(vector) + " should report the " + name + " " +
                expected.ToString("u", CultureInfo.InvariantCulture) +
                ", yet the property has no value because '" +
                actual.NoValueMessage + "'. The reason was " +
                result.AgentSignatureReason.Value + ".");
            Assert.AreEqual(
                expected,
                actual.Value,
                Describe(vector) + " should report the " + name + " " +
                expected.ToString("u", CultureInfo.InvariantCulture) +
                ", yet it reported " +
                actual.Value.ToString("u", CultureInfo.InvariantCulture) +
                ". The reason was " + result.AgentSignatureReason.Value +
                ".");
        }

        private static void AssertTextValue(
            IReadOnlyDictionary<string, object> values,
            string property,
            string expected)
        {
            var value = ReadText(values, property);
            Assert.IsTrue(
                value.HasValue,
                "The property '" + property + "' should hold '" + expected +
                "', yet it has no value because '" + value.NoValueMessage +
                "'.");
            Assert.AreEqual(
                expected,
                value.Value,
                "The property '" + property + "' should hold '" + expected +
                "', yet it holds '" + value.Value + "'.");
        }

        private static void AssertNoTextValue(
            IReadOnlyDictionary<string, object> values,
            string property,
            string message)
        {
            var value = ReadText(values, property);
            Assert.IsFalse(
                value.HasValue,
                "The property '" + property + "' should have no value on a " +
                "request that carried no signature, yet it holds '" +
                (value.HasValue ? value.Value : null) + "'.");
            Assert.AreEqual(
                message,
                value.NoValueMessage,
                "The property '" + property + "' should say '" + message +
                "' when the request carried no signature, yet it says '" +
                value.NoValueMessage + "'.");
        }

        private static void AssertNoTimeValue(
            IReadOnlyDictionary<string, object> values,
            string property,
            string message)
        {
            Assert.IsTrue(
                values.ContainsKey(property),
                "The property '" + property + "' was not reported at all.");
            var value =
                values[property] as IAspectPropertyValue<DateTimeOffset>;
            Assert.IsNotNull(
                value,
                "The property '" + property + "' should be a time with a " +
                "reason when it has no value, yet it is '" +
                values[property] + "'.");
            Assert.IsFalse(
                value.HasValue,
                "The property '" + property + "' should have no value on a " +
                "request that carried no signature.");
            Assert.AreEqual(
                message,
                value.NoValueMessage,
                "The property '" + property + "' should say '" + message +
                "' when the request carried no signature, yet it says '" +
                value.NoValueMessage + "'.");
        }

        private static IAspectPropertyValue<string> ReadText(
            IReadOnlyDictionary<string, object> values,
            string property)
        {
            Assert.IsTrue(
                values.ContainsKey(property),
                "The property '" + property + "' was not reported at all.");
            var value = values[property] as IAspectPropertyValue<string>;
            Assert.IsNotNull(
                value,
                "The property '" + property + "' should be text with a " +
                "reason when it has no value, yet it is '" +
                values[property] + "'.");
            return value;
        }

        /// <summary>
        /// Copy a signed request with one byte of the decoded signature
        /// changed.
        /// </summary>
        private static SignedRequest WithChangedSignatureByte(
            SignedRequest signed)
        {
            var first = signed.Signature.IndexOf(':');
            var last = signed.Signature.LastIndexOf(':');
            var bytes = Convert.FromBase64String(
                signed.Signature.Substring(first + 1, last - first - 1));
            bytes[0] ^= 0x01;
            return new SignedRequest
            {
                Signature = signed.Signature.Substring(0, first + 1) +
                    Convert.ToBase64String(bytes) + ":",
                SignatureInput = signed.SignatureInput,
                SignatureAgent = signed.SignatureAgent,
                SignatureBase = signed.SignatureBase,
            };
        }

        /// <summary>
        /// Add one member to a JSON Web Key, so that a test can serve a key
        /// carrying a time or an algorithm the fixture does not hold.
        /// </summary>
        private static string WithMember(
            string keyJson,
            string name,
            string value)
        {
            var end = keyJson.LastIndexOf('}');
            return keyJson.Substring(0, end) +
                ",\"" + name + "\":" + value + "}";
        }

        /// <summary>
        /// Build the fake response that serves a key directory body with the
        /// signature headers the vector records. The content type is left to
        /// the media type of the response, because it is not a header a
        /// caller can add to a response by hand.
        /// </summary>
        private static FakeResponse BuildDirectoryResponse(
            string body,
            DirectoryResponseVector vector)
        {
            var response = new FakeResponse
            {
                Body = body,
                MediaType = Constants.DIRECTORY_MEDIA_TYPE,
            };
            foreach (var header in vector.Headers)
            {
                if (string.Equals(
                    header.Key,
                    "content-type",
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                response.Headers[header.Key] = header.Value;
            }
            return response;
        }

        /// <summary>
        /// Build the response message the fetcher is handed directly, being
        /// the same body and headers the fake handler would serve.
        /// </summary>
        private static HttpResponseMessage BuildResponseMessage(
            DirectoryResponseVector vector,
            byte[] body)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(body),
            };
            response.Content.Headers.ContentType =
                new MediaTypeHeaderValue(Constants.DIRECTORY_MEDIA_TYPE);
            foreach (var header in vector.Headers)
            {
                if (string.Equals(
                    header.Key,
                    "content-type",
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                response.Headers.TryAddWithoutValidation(
                    header.Key, header.Value);
            }
            return response;
        }

        /// <summary>
        /// Check that a header the fetcher will look for is on the response
        /// the test built, because a header that was quietly refused would
        /// leave the fetcher with nothing to check.
        /// </summary>
        private static void AssertHeaderPresent(
            HttpResponseMessage response,
            string name)
        {
            Assert.IsNotNull(
                DirectoryFetcher.GetHeader(response, name),
                "The response this test built should carry the '" + name +
                "' header, and it does not, so the fetcher would have had " +
                "nothing to check.");
        }

        /// <summary>
        /// Read the bytes out of a 'Signature' header member, which is
        /// written as a label, an equals sign and the base64 of the
        /// signature between colons.
        /// </summary>
        private static byte[] ReadSignatureBytes(string header)
        {
            var first = header.IndexOf(':');
            var last = header.LastIndexOf(':');
            return Convert.FromBase64String(
                header.Substring(first + 1, last - first - 1));
        }

        #endregion
    }
}
