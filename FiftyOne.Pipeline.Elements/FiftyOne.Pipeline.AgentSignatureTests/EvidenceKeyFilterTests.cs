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

using FiftyOne.Pipeline.AgentSignature.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.AgentSignature.Tests
{
    /// <summary>
    /// Checks the evidence the element asks for. A signature may cover any
    /// request header, so the element cannot write down the list in advance
    /// and asks for every header instead. The web integration only puts
    /// evidence into a request that some element has asked for, so a header
    /// the filter leaves out is a header no signature covering it can be
    /// checked against.
    /// </summary>
    [TestClass]
    public class EvidenceKeyFilterTests
    {
        /// <summary>
        /// The header a signature covers in the end to end test below, being
        /// an ordinary request header rather than one of the three the
        /// protocol names.
        /// </summary>
        private const string OrdinaryHeaderKey = "header.content-type";

        /// <summary>
        /// The value of that header.
        /// </summary>
        private const string OrdinaryHeaderValue = "application/json";

        /// <summary>
        /// Every request header is asked for, together with the protocol
        /// that '@authority' and '@scheme' are built from, and nothing that
        /// is not a header the element could be asked to rebuild.
        /// </summary>
        /// <param name="key">The evidence key.</param>
        /// <param name="expected">Whether the element asks for it.</param>
        /// <param name="why">Why, for the assertion message.</param>
        [DataTestMethod]
        [DataRow(
            "header.signature",
            true,
            "it carries the signature itself")]
        [DataRow(
            "header.signature-input",
            true,
            "it says what the signature covers")]
        [DataRow(
            "header.signature-agent",
            true,
            "it says where the agent publishes its keys")]
        [DataRow(
            "header.host",
            true,
            "'@authority' is built from it")]
        [DataRow(
            "header.protocol",
            true,
            "'@authority' and '@scheme' are built from it")]
        [DataRow(
            "header.content-type",
            true,
            "a signature may cover any request header")]
        [DataRow(
            "header.x-something-nobody-has-heard-of",
            true,
            "a signature may cover any request header")]
        [DataRow(
            "HEADER.Content-Type",
            true,
            "an evidence key is matched whatever its case")]
        [DataRow(
            "query.signature",
            true,
            "a caller's own Pipeline forwards its evidence with the " +
            "prefix taken off, so a signature header reaches the cloud " +
            "service under the query prefix")]
        [DataRow(
            "query.foo",
            true,
            "a signature may cover any request header, and a forwarded " +
            "header cannot be told apart from any other query value once " +
            "the prefix has been taken off")]
        [DataRow(
            "cookie.bar",
            false,
            "a cookie is not a request header")]
        [DataRow(
            "server.client-ip",
            false,
            "the address the request came from says nothing about what " +
            "the signature covers")]
        [DataRow(
            "headerless",
            false,
            "it only looks like a header key")]
        public void EvidenceKeyIsAskedForOnlyWhenItIsAHeader(
            string key,
            bool expected,
            string why)
        {
            using (var harness = ElementHarness.Create())
            {
                Assert.AreEqual(
                    expected,
                    harness.Element.EvidenceKeyFilter.Include(key),
                    "The element should " + (expected ? "ask" : "not ask") +
                    " for the evidence key '" + key + "', because " + why +
                    ".");
                Assert.AreEqual(
                    expected,
                    harness.Element.EvidenceKeyFilter.Order(key).HasValue,
                    "The evidence key '" + key + "' should " +
                    (expected ? "be given an order, because the element " +
                        "asks for it" : "be given no order, because the " +
                        "element does not ask for it") + ".");
            }
        }

        /// <summary>
        /// A signature covering an ordinary request header verifies, which
        /// it could not do whilst the element asked for a fixed list of five
        /// keys, because the web integration would then never have put that
        /// header into evidence. The pipeline filter is checked as well as
        /// the outcome, because the filter is what the web integration asks
        /// before it adds a header.
        /// </summary>
        [TestMethod]
        public void SignatureCoveringAnOrdinaryHeaderVerifies()
        {
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                Assert.IsTrue(
                    harness.Pipeline.EvidenceKeyFilter.Include(
                        OrdinaryHeaderKey),
                    "The pipeline should ask for '" + OrdinaryHeaderKey +
                    "', because that is what the web integration reads " +
                    "before it puts a header into evidence.");

                var options = new SigningOptions();
                options.ExtraComponents.Add(
                    new KeyValuePair<string, string>(
                        "\"content-type\"", OrdinaryHeaderValue));
                var signed = RequestSigner.Sign(options);

                var result = harness.Process(new Dictionary<string, string>
                {
                    { Constants.EVIDENCE_SIGNATURE_KEY, signed.Signature },
                    {
                        Constants.EVIDENCE_SIGNATURE_INPUT_KEY,
                        signed.SignatureInput
                    },
                    {
                        Constants.EVIDENCE_SIGNATURE_AGENT_KEY,
                        signed.SignatureAgent
                    },
                    { Constants.EVIDENCE_HOST_KEY, "example.com" },
                    { Core.Constants.EVIDENCE_PROTOCOL, "https" },
                    { OrdinaryHeaderKey, OrdinaryHeaderValue },
                });

                Assert.AreEqual(
                    Constants.STATUS_VERIFIED,
                    result.AgentSignature.Value,
                    "Expected Verified for a signature covering the " +
                    "'content-type' header. The status was '" +
                    result.AgentSignature.Value + "' with the reason '" +
                    result.AgentSignatureReason.Value + "'.");
            }
        }

        /// <summary>
        /// A signature covering a header that did not arrive cannot be
        /// rebuilt, so the element says so rather than reporting a mismatch.
        /// This is the other half of the test above, showing that the header
        /// really is what the signature was checked against.
        /// </summary>
        [TestMethod]
        public void SignatureCoveringAHeaderThatDidNotArriveIsUnverified()
        {
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var options = new SigningOptions();
                options.ExtraComponents.Add(
                    new KeyValuePair<string, string>(
                        "\"content-type\"", OrdinaryHeaderValue));
                var signed = RequestSigner.Sign(options);
                var result = harness.ProcessSigned(signed);

                Assert.AreEqual(
                    Constants.STATUS_UNVERIFIED,
                    result.AgentSignature.Value,
                    "Expected Unverified for a signature covering a header " +
                    "the request did not carry. The status was '" +
                    result.AgentSignature.Value + "' with the reason '" +
                    result.AgentSignatureReason.Value + "'.");
                Assert.AreEqual(
                    Constants.REASON_COMPONENT_UNAVAILABLE,
                    result.AgentSignatureReason.Value,
                    "Expected the ComponentUnavailable reason, and the " +
                    "reason was '" + result.AgentSignatureReason.Value +
                    "'.");
            }
        }
    }
}
