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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;

namespace FiftyOne.Pipeline.AgentSignature.Tests
{
    /// <summary>
    /// Checks what a signature has to do before the element will say it
    /// proves anything, being that the key set comes from an address the
    /// agent controls rather than from the request itself, that the
    /// signature is tied to the request it arrived on, and that the agent
    /// it names is one the signature covers. The signature times that no
    /// point in time can be written as are checked here as well, because
    /// they arrive through the same header.
    /// </summary>
    [TestClass]
    public class SignatureTrustTests
    {
        /// <summary>
        /// The label the 'Signature-Agent' member is written with.
        /// </summary>
        private const string AgentLabel = "agent1";

        /// <summary>
        /// A second agent, used to show that a covered component naming the
        /// whole header cannot choose between two members.
        /// </summary>
        private const string OtherAgentOrigin = "https://other.example.com";

        /// <summary>
        /// The largest whole number a structured field may carry, which is
        /// a 'created' more than thirty million years from now.
        /// </summary>
        private const string FarFutureSeconds = "999999999999999";

        /// <summary>
        /// The smallest whole number a structured field may carry, which is
        /// a 'created' the same distance in the past.
        /// </summary>
        private const string FarPastSeconds = "-999999999999999";

        #region Key sets carried in the header

        /// <summary>
        /// A key set carried in the header is chosen by whoever sent the
        /// request, so a signature that checks out against it shows only
        /// that the sender holds the matching private key. The element
        /// refuses it by default with Unverified and the InlineDirectory
        /// reason, and fetches nothing, because there is nowhere to fetch
        /// from.
        /// </summary>
        [TestMethod]
        public void InlineKeySetIsRefusedByDefault()
        {
            using (var harness = ElementHarness.Create())
            {
                var result = ProcessInlineRequest(harness);

                Assert.AreEqual(
                    Constants.STATUS_UNVERIFIED,
                    result.AgentSignature.Value,
                    "Expected Unverified, because the agent carried its key " +
                    "set in the header and the element is not set to accept " +
                    "one. " + Describe(result));
                Assert.AreEqual(
                    Constants.REASON_INLINE_DIRECTORY,
                    result.AgentSignatureReason.Value,
                    "Expected the InlineDirectory reason. " +
                    Describe(result));
                Assert.AreEqual(
                    0,
                    harness.Handler.CallCount,
                    "Expected no request at all, because the key set was in " +
                    "the header, and there were " +
                    harness.Handler.CallCount + ".");
            }
        }

        /// <summary>
        /// Told to accept a key set carried in the header, the element reads
        /// the key set out of the 'data:' URI and checks the signature
        /// against it, so a correctly signed request reads Verified. The
        /// signature here is a real one over the request, so this reaches
        /// Verified through the same checking as any other request rather
        /// than through a shortcut.
        /// </summary>
        [TestMethod]
        public void InlineKeySetVerifiesWhenTheElementIsToldToAcceptOne()
        {
            using (var harness = ElementHarness.Create(
                builder => builder.SetAllowInlineDirectory(true)))
            {
                var result = ProcessInlineRequest(harness);

                Assert.AreEqual(
                    Constants.STATUS_VERIFIED,
                    result.AgentSignature.Value,
                    "Expected Verified, because the request was signed with " +
                    "the key the header carries and the element was told to " +
                    "accept a key set in the header. " + Describe(result));
                Assert.AreEqual(
                    Constants.REASON_VERIFIED,
                    result.AgentSignatureReason.Value,
                    "Expected the Verified reason. " + Describe(result));
                Assert.AreEqual(
                    0,
                    harness.Handler.CallCount,
                    "Expected no request at all, because the key set was in " +
                    "the header, and there were " +
                    harness.Handler.CallCount + ".");
            }
        }

        /// <summary>
        /// A key set carried in the header is refused by default, so nobody
        /// has to know the setting exists to be safe from it.
        /// </summary>
        [TestMethod]
        public void InlineKeySetIsOffByDefault()
        {
            Assert.IsFalse(
                Constants.DEFAULT_ALLOW_INLINE_DIRECTORY,
                "A key set carried in the header should be refused unless " +
                "the caller asks for it.");
        }

        #endregion

        #region Signatures tied to nothing

        /// <summary>
        /// A signature that covers neither '@authority' nor '@target-uri'
        /// says nothing about the request it arrived on, so the same
        /// signature would check out against a request sent to any other
        /// site and one captured elsewhere could be replayed here. The
        /// element reads Invalid with the UnboundSignature reason and never
        /// fetches a key, because there is nothing worth checking.
        /// </summary>
        [TestMethod]
        public void SignatureCoveringOnlyTheAgentIsUnbound()
        {
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var signed = RequestSigner.Sign(new SigningOptions
                {
                    CoverAuthority = false,
                });
                var result = harness.ProcessSigned(signed);

                Assert.AreEqual(
                    Constants.STATUS_INVALID,
                    result.AgentSignature.Value,
                    "Expected Invalid, because the signature covers only " +
                    "the 'Signature-Agent' header and so is tied to nothing " +
                    "about the request it arrived on. " + Describe(result));
                Assert.AreEqual(
                    Constants.REASON_UNBOUND_SIGNATURE,
                    result.AgentSignatureReason.Value,
                    "Expected the UnboundSignature reason. " +
                    Describe(result));
                Assert.AreEqual(
                    0,
                    harness.Handler.CallCount,
                    "Expected no key to have been fetched for a signature " +
                    "tied to nothing, and there were " +
                    harness.Handler.CallCount + " requests.");
            }
        }

        /// <summary>
        /// A signature that covers '@authority', which is what a well
        /// behaved agent sends, verifies as it did before the check above
        /// was added, so the check does not fire on good traffic.
        /// </summary>
        [TestMethod]
        public void SignatureCoveringTheAuthorityStillVerifies()
        {
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var signed = RequestSigner.Sign(new SigningOptions());
                Assert.IsTrue(
                    signed.SignatureInput.Contains("\"@authority\""),
                    "This test needs a signature covering '@authority', " +
                    "and the one it built was '" + signed.SignatureInput +
                    "'.");
                var result = harness.ProcessSigned(signed);

                Assert.AreEqual(
                    Constants.STATUS_VERIFIED,
                    result.AgentSignature.Value,
                    "Expected Verified for a signature covering " +
                    "'@authority'. " + Describe(result));
                Assert.AreEqual(
                    Constants.REASON_VERIFIED,
                    result.AgentSignatureReason.Value,
                    "Expected the Verified reason. " + Describe(result));
            }
        }

        #endregion

        #region Times no point in time can be written as

        /// <summary>
        /// A 'created' far outside the range of times the framework can hold
        /// is answered rather than thrown out of the pipeline. Such a value
        /// names no time at all, so the signature parameters cannot be read
        /// and the request reads Invalid with the Malformed reason. Reading
        /// it as the earliest time instead would let a signature claiming a
        /// created far in the future pass as one made long ago.
        /// </summary>
        /// <param name="created">The 'created' parameter to send.</param>
        [DataTestMethod]
        [DataRow(FarFutureSeconds)]
        [DataRow(FarPastSeconds)]
        public void CreatedTimeOutsideTheRangeOfTimesIsAnswered(string created)
        {
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var signed = RequestSigner.Sign(new SigningOptions
                {
                    CreatedText = created,
                });
                Assert.IsTrue(
                    signed.SignatureInput.Contains(";created=" + created),
                    "This test needs a signature whose 'created' is " +
                    created + ", and the one it built was '" +
                    signed.SignatureInput + "'.");

                var result = harness.ProcessSigned(signed);

                Assert.AreEqual(
                    Constants.STATUS_INVALID,
                    result.AgentSignature.Value,
                    "Expected Invalid for a signature whose 'created' is " +
                    created + ", because that names no time the element can " +
                    "act on. " + Describe(result));
                Assert.AreEqual(
                    Constants.REASON_MALFORMED,
                    result.AgentSignatureReason.Value,
                    "Expected the Malformed reason. " + Describe(result));
            }
        }

        /// <summary>
        /// The same holds for 'expires'. A value far outside the range of
        /// times the framework can hold names no time, so the signature
        /// reads Invalid with the Malformed reason rather than throwing.
        /// </summary>
        /// <param name="expires">The 'expires' parameter to send.</param>
        [DataTestMethod]
        [DataRow(FarFutureSeconds)]
        [DataRow(FarPastSeconds)]
        public void ExpiryTimeOutsideTheRangeOfTimesIsAnswered(string expires)
        {
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var signed = RequestSigner.Sign(new SigningOptions
                {
                    ExpiresText = expires,
                });
                var result = harness.ProcessSigned(signed);

                Assert.AreEqual(
                    Constants.STATUS_INVALID,
                    result.AgentSignature.Value,
                    "Expected Invalid for a signature whose 'expires' is " +
                    expires + ", because that names no time the element can " +
                    "act on. " + Describe(result));
                Assert.AreEqual(
                    Constants.REASON_MALFORMED,
                    result.AgentSignatureReason.Value,
                    "Expected the Malformed reason. " + Describe(result));
            }
        }

        #endregion

        #region Agents the signature does not cover

        /// <summary>
        /// Anyone can add a header to a request, so a 'Signature-Agent'
        /// header the signature does not cover says nothing about which
        /// agent sent the request. A request whose signature covers
        /// '@authority' only, with a header bolted on naming an agent whose
        /// directory would verify it, reads Unverified with the NoAgent
        /// reason and fetches nothing.
        /// </summary>
        [TestMethod]
        public void AgentHeaderTheSignatureDoesNotCoverIsNoAgent()
        {
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                // Signed with no 'Signature-Agent' member covered at all,
                // then sent with one added by hand.
                var signed = RequestSigner.Sign(new SigningOptions
                {
                    SignatureAgent = null,
                });
                var result = Process(
                    harness,
                    signed,
                    AgentLabel + "=\"" + Fixtures.SignatureAgentOrigin +
                        "\"");

                AssertNoAgent(
                    result,
                    "a 'Signature-Agent' header the signature does not " +
                    "cover");
                Assert.AreEqual(
                    0,
                    harness.Handler.CallCount,
                    "Expected no key to have been fetched from an agent the " +
                    "signature does not name, and there were " +
                    harness.Handler.CallCount + " requests.");
            }
        }

        /// <summary>
        /// A signature that covers the bare 'signature-agent' component
        /// covers the whole header, so the header has to name one agent for
        /// there to be no doubt which one signed. Sent with two members, the
        /// element picks neither and reads Unverified with the NoAgent
        /// reason, even though the first member names the agent whose keys
        /// this harness serves. The same bare component with one member is
        /// the form the architecture v1 vectors send, and those verify, so
        /// this is about the count rather than the form.
        /// </summary>
        [TestMethod]
        public void BareAgentComponentWithTwoMembersIsNoAgent()
        {
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var signed = RequestSigner.Sign(new SigningOptions
                {
                    SignatureAgentLabel = null,
                });
                var result = Process(
                    harness,
                    signed,
                    AgentLabel + "=\"" + Fixtures.SignatureAgentOrigin +
                        "\", agent2=\"" + OtherAgentOrigin + "\"");

                AssertNoAgent(
                    result,
                    "a covered 'signature-agent' component with two members " +
                    "in the header");
                Assert.AreEqual(
                    0,
                    harness.Handler.CallCount,
                    "Expected neither member to have been fetched, and " +
                    "there were " + harness.Handler.CallCount + " requests.");
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Run a signed request with the 'Signature-Agent' header given,
        /// which is how a test sends a header the signature itself does not
        /// cover.
        /// </summary>
        /// <param name="harness">The harness to run it through.</param>
        /// <param name="signed">The signature headers.</param>
        /// <param name="agentHeader">
        /// The 'Signature-Agent' header to send.
        /// </param>
        /// <returns>What the element made of the request.</returns>
        private static IAgentSignatureData Process(
            ElementHarness harness,
            SignedRequest signed,
            string agentHeader)
        {
            return harness.Process(new Dictionary<string, string>
            {
                { Constants.EVIDENCE_SIGNATURE_KEY, signed.Signature },
                {
                    Constants.EVIDENCE_SIGNATURE_INPUT_KEY,
                    signed.SignatureInput
                },
                { Constants.EVIDENCE_SIGNATURE_AGENT_KEY, agentHeader },
                { Constants.EVIDENCE_HOST_KEY, "example.com" },
                { Core.Constants.EVIDENCE_PROTOCOL, "https" },
            });
        }

        /// <summary>
        /// Sign a request whose 'Signature-Agent' member carries the key
        /// directory itself in a 'data:' URI, and run it through the
        /// element.
        /// </summary>
        /// <param name="harness">The harness to run it through.</param>
        /// <returns>What the element made of the request.</returns>
        private static IAgentSignatureData ProcessInlineRequest(
            ElementHarness harness)
        {
            var member = InlineMemberValue();
            // The signer helper writes a member holding a plain origin, so
            // the member carrying the key set is built here instead and
            // added as a covered component of its own.
            var options = new SigningOptions
            {
                SignatureAgent = null,
            };
            options.ExtraComponents.Add(
                new KeyValuePair<string, string>(
                    "\"signature-agent\";key=\"" + AgentLabel + "\"",
                    member));
            var signed = RequestSigner.Sign(options);

            return Process(harness, signed, AgentLabel + "=" + member);
        }

        /// <summary>
        /// The 'Signature-Agent' member that carries the key directory in a
        /// 'data:' URI, being the directory media type, the word saying the
        /// bytes are base64, and the directory itself.
        /// </summary>
        /// <returns>The member value.</returns>
        private static string InlineMemberValue()
        {
            var directory = "{\"keys\":[" +
                RequestSigner.PublicPart(Fixtures.Ed25519Key()) + "]}";
            return "\"data:" + Constants.DIRECTORY_MEDIA_TYPE + ";base64," +
                Convert.ToBase64String(Encoding.UTF8.GetBytes(directory)) +
                "\"";
        }

        /// <summary>
        /// Check that the element found no agent it could trust the header
        /// about.
        /// </summary>
        /// <param name="result">What the element made of the request.</param>
        /// <param name="what">What was sent, for the message.</param>
        private static void AssertNoAgent(
            IAgentSignatureData result,
            string what)
        {
            Assert.AreEqual(
                Constants.STATUS_UNVERIFIED,
                result.AgentSignature.Value,
                "Expected Unverified for " + what + ", because the " +
                "signature says nothing about which agent sent the " +
                "request. " + Describe(result));
            Assert.AreEqual(
                Constants.REASON_NO_AGENT,
                result.AgentSignatureReason.Value,
                "Expected the NoAgent reason for " + what + ". " +
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
                "' with the reason '" + result.AgentSignatureReason.Value +
                "'.";
        }

        #endregion
    }
}
