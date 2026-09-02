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
using FiftyOne.Pipeline.Core.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.AgentSignature.Tests
{
    /// <summary>
    /// The strongest coverage an agent can choose takes in the request
    /// line, being the method, the path and the query string, because a
    /// signature covering only the authority checks out against any address
    /// on the site and one captured elsewhere on the site could be replayed.
    /// These tests cover the four derived components built from the request
    /// line evidence, and what happens where an integration supplies none of
    /// it.
    /// </summary>
    [TestClass]
    public class RequestLineComponentTests
    {
        private const string Host = "example.com";

        private const string Path = "/products/compare";

        private const string Query = "id=7&sort=name";

        /// <summary>
        /// A signature covering the method, the path and the query verifies
        /// where the request line is in the evidence. Each component is
        /// written here as RFC 9421 sections 2.2.1, 2.2.6 and 2.2.7 define
        /// it, so a component this element builds differently would fail to
        /// verify rather than quietly passing.
        /// </summary>
        [TestMethod]
        public void SignatureCoveringTheRequestLineVerifies()
        {
            var signed = RequestSigner.Sign(Options(
                new KeyValuePair<string, string>("\"@method\"", "GET"),
                new KeyValuePair<string, string>("\"@path\"", Path),
                new KeyValuePair<string, string>("\"@query\"", "?" + Query)));

            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var result = harness.ProcessSigned(
                    signed, RequestLine("GET", Path, Query));

                Assert.AreEqual(
                    Constants.STATUS_VERIFIED,
                    result.AgentSignature.Value,
                    "Expected a signature covering the method, the path " +
                    "and the query to verify, and the status was '" +
                    result.AgentSignature.Value + "' with reason '" +
                    result.AgentSignatureReason.Value + "'.");
            }
        }

        /// <summary>
        /// A signature covering the whole target address verifies. This is
        /// the strongest form the protocol draft offers, because the
        /// address covers the scheme, the host, the path and the query
        /// together.
        /// </summary>
        [TestMethod]
        public void SignatureCoveringTheTargetUriVerifies()
        {
            var signed = RequestSigner.Sign(Options(
                new KeyValuePair<string, string>(
                    "\"@target-uri\"",
                    "https://" + Host + Path + "?" + Query)));

            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var result = harness.ProcessSigned(
                    signed, RequestLine("GET", Path, Query));

                Assert.AreEqual(
                    Constants.STATUS_VERIFIED,
                    result.AgentSignature.Value,
                    "Expected a signature covering the target address to " +
                    "verify, and the status was '" +
                    result.AgentSignature.Value + "' with reason '" +
                    result.AgentSignatureReason.Value + "'.");
            }
        }

        /// <summary>
        /// A request with no query string at all still writes the query
        /// component as a question mark on its own, which RFC 9421 section
        /// 2.2.7 requires, so the signature verifies. Without the rule the
        /// component would be empty and the base would not match.
        /// </summary>
        [TestMethod]
        public void SignatureVerifiesWhereTheRequestCarriesNoQuery()
        {
            var signed = RequestSigner.Sign(Options(
                new KeyValuePair<string, string>("\"@path\"", Path),
                new KeyValuePair<string, string>("\"@query\"", "?")));

            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var result = harness.ProcessSigned(
                    signed, RequestLine("GET", Path, string.Empty));

                Assert.AreEqual(
                    Constants.STATUS_VERIFIED,
                    result.AgentSignature.Value,
                    "Expected a request with no query string to verify " +
                    "with the query component written as '?', and the " +
                    "status was '" + result.AgentSignature.Value +
                    "' with reason '" + result.AgentSignatureReason.Value +
                    "'.");
            }
        }

        /// <summary>
        /// The method goes on the signature base in upper case whatever
        /// case the request used, as RFC 9421 section 2.2.1 requires.
        /// </summary>
        [TestMethod]
        public void MethodIsUpperCasedOnTheSignatureBase()
        {
            var signed = RequestSigner.Sign(Options(
                new KeyValuePair<string, string>("\"@method\"", "POST")));

            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var result = harness.ProcessSigned(
                    signed, RequestLine("post", Path, Query));

                Assert.AreEqual(
                    Constants.STATUS_VERIFIED,
                    result.AgentSignature.Value,
                    "Expected a request whose method arrived in lower " +
                    "case to verify against a signature made over the " +
                    "upper case form, and the status was '" +
                    result.AgentSignature.Value + "' with reason '" +
                    result.AgentSignatureReason.Value + "'.");
            }
        }

        /// <summary>
        /// A signature covering the path does not verify against a request
        /// for a different path. This is the protection the coverage buys,
        /// because a signature captured on one address must not check out
        /// on another.
        /// </summary>
        [TestMethod]
        public void SignatureDoesNotVerifyAgainstADifferentPath()
        {
            var signed = RequestSigner.Sign(Options(
                new KeyValuePair<string, string>("\"@path\"", Path)));

            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var result = harness.ProcessSigned(
                    signed, RequestLine("GET", "/admin/export", Query));

                Assert.AreEqual(
                    Constants.STATUS_INVALID,
                    result.AgentSignature.Value,
                    "Expected a signature made for one path to fail " +
                    "against another, and the status was '" +
                    result.AgentSignature.Value + "'.");
                Assert.AreEqual(
                    Constants.REASON_SIGNATURE_MISMATCH,
                    result.AgentSignatureReason.Value,
                    "Expected the SignatureMismatch reason for a request " +
                    "to a different path, and the reason was '" +
                    result.AgentSignatureReason.Value + "'.");
            }
        }

        /// <summary>
        /// A signature covering the query does not verify against a request
        /// carrying a different query string, so the parameters an agent
        /// signed cannot be changed on the way.
        /// </summary>
        [TestMethod]
        public void SignatureDoesNotVerifyAgainstADifferentQuery()
        {
            var signed = RequestSigner.Sign(Options(
                new KeyValuePair<string, string>(
                    "\"@query\"", "?" + Query)));

            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var result = harness.ProcessSigned(
                    signed, RequestLine("GET", Path, "id=8&sort=name"));

                Assert.AreEqual(
                    Constants.STATUS_INVALID,
                    result.AgentSignature.Value,
                    "Expected a signature made over one query string to " +
                    "fail against another, and the status was '" +
                    result.AgentSignature.Value + "'.");
            }
        }

        /// <summary>
        /// Where the integration supplies no request line, which is what a
        /// pipeline fed by hand or by an older web integration looks like,
        /// a signature covering the path reports that it could not be
        /// checked rather than that it failed. A signature that cannot be
        /// rebuilt is not evidence against the agent.
        /// </summary>
        [TestMethod]
        public void RequestLineComponentsWithoutTheEvidenceReadUnverified()
        {
            var signed = RequestSigner.Sign(Options(
                new KeyValuePair<string, string>("\"@path\"", Path)));

            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var result = harness.ProcessSigned(signed);

                Assert.AreEqual(
                    Constants.STATUS_UNVERIFIED,
                    result.AgentSignature.Value,
                    "Expected Unverified where the request line is not in " +
                    "the evidence, and the status was '" +
                    result.AgentSignature.Value + "'.");
                Assert.AreEqual(
                    Constants.REASON_COMPONENT_UNAVAILABLE,
                    result.AgentSignatureReason.Value,
                    "Expected the ComponentUnavailable reason where the " +
                    "request line is not in the evidence, and the reason " +
                    "was '" + result.AgentSignatureReason.Value + "'.");
            }
        }

        /// <summary>
        /// The element asks for the three request line keys, so that a web
        /// integration adds them. A pipeline holding no element that asks
        /// for them carries none of them, which is what keeps this change
        /// out of every other pipeline.
        /// </summary>
        [TestMethod]
        public void ElementAsksForTheRequestLineEvidence()
        {
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var filter = harness.Element.EvidenceKeyFilter;
                foreach (var key in new[]
                {
                    Core.Constants.EVIDENCE_REQUEST_METHOD_KEY,
                    Core.Constants.EVIDENCE_REQUEST_PATH_KEY,
                    Core.Constants.EVIDENCE_REQUEST_QUERY_KEY,
                })
                {
                    Assert.IsTrue(
                        filter.Include(key),
                        "Expected the element to ask for '" + key +
                        "', because a web integration only adds evidence " +
                        "that an element has asked for.");
                }
                Assert.IsFalse(
                    filter.Include("server.client-ip"),
                    "Expected the element not to ask for evidence it " +
                    "cannot use, because asking for more than is needed " +
                    "makes other elements' work reach this element too.");
            }
        }

        /// <summary>
        /// A request forwarded to the cloud service verifies. A caller's
        /// own Pipeline sends its evidence on with the prefix taken off,
        /// so the signature headers and the request line arrive under the
        /// query prefix, whilst the headers of the call to the cloud
        /// itself arrive under the header prefix and name the cloud as the
        /// host. The forwarded copy has to win, or the check would run
        /// against the call the caller made rather than the request the
        /// agent signed.
        /// </summary>
        [TestMethod]
        public void ForwardedRequestVerifiesAgainstTheCallersOwnHost()
        {
            var signed = RequestSigner.Sign(Options(
                new KeyValuePair<string, string>("\"@method\"", "GET"),
                new KeyValuePair<string, string>("\"@path\"", Path)));

            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var forwarded = new Dictionary<string, string>()
                {
                    // What the caller's Pipeline sent on.
                    { "query.signature", signed.Signature },
                    { "query.signature-input", signed.SignatureInput },
                    { "query.signature-agent", signed.SignatureAgent },
                    { "query.host", Host },
                    { "query.protocol", "https" },
                    { "query.request-method", "GET" },
                    { "query.request-path", Path },
                    { "query.request-query", string.Empty },
                };
                var result = harness.ProcessSigned(
                    // The call to the cloud carries none of the agent's
                    // own signature, and names the cloud as its host.
                    new SignedRequest
                    {
                        Signature = "sig1=:AAAA:",
                        SignatureInput = "sig1=();created=1",
                        SignatureAgent = null,
                    },
                    forwarded,
                    "cloud.51degrees.com");

                Assert.AreEqual(
                    Constants.STATUS_VERIFIED,
                    result.AgentSignature.Value,
                    "Expected a forwarded request to verify against the " +
                    "caller's own host rather than the cloud's, and the " +
                    "status was '" + result.AgentSignature.Value +
                    "' with reason '" + result.AgentSignatureReason.Value +
                    "'.");
                Assert.AreEqual(
                    Fixtures.SignatureAgentOrigin,
                    result.AgentSignatureAgent.Value,
                    "Expected the agent from the forwarded headers.");
            }
        }

        /// <summary>
        /// The element asks for the keys a forwarded request carries by
        /// name, not only through the rule that accepts any header. The
        /// cloud service builds the list of evidence it accepts from the
        /// named keys, and a rule about a prefix cannot be written into
        /// such a list, so without these a forwarded signature would
        /// arrive carrying nothing.
        /// </summary>
        [TestMethod]
        public void NamedKeysCoverWhatAForwardedRequestNeeds()
        {
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var whitelist =
                    ((EvidenceKeyFilterWhitelist)harness.Element
                        .EvidenceKeyFilter).Whitelist;
                foreach (var key in new[]
                {
                    "query.signature",
                    "query.signature-input",
                    "query.signature-agent",
                    "query.host",
                    "query.protocol",
                    "query.request-method",
                    "query.request-path",
                    "query.request-query",
                    "header.signature",
                    "header.signature-input",
                    "header.signature-agent",
                    "header.host",
                    "header.protocol",
                    Core.Constants.EVIDENCE_REQUEST_METHOD_KEY,
                    Core.Constants.EVIDENCE_REQUEST_PATH_KEY,
                    Core.Constants.EVIDENCE_REQUEST_QUERY_KEY,
                })
                {
                    Assert.IsTrue(
                        whitelist.ContainsKey(key),
                        "Expected '" + key + "' to be named in the " +
                        "whitelist, because a service that reads the " +
                        "whitelist to decide what to forward cannot see " +
                        "a rule about a prefix.");
                }
            }
        }

        /// <summary>
        /// Sign a request whose covered components include the given extra
        /// ones beyond the authority and the signature agent.
        /// </summary>
        private static SigningOptions Options(
            params KeyValuePair<string, string>[] extra)
        {
            var options = new SigningOptions
            {
                Host = Host,
            };
            foreach (var component in extra)
            {
                options.ExtraComponents.Add(component);
            }
            return options;
        }

        /// <summary>
        /// The evidence a web integration adds for the request line.
        /// </summary>
        private static IDictionary<string, string> RequestLine(
            string method,
            string path,
            string query)
        {
            return new Dictionary<string, string>()
            {
                { Core.Constants.EVIDENCE_REQUEST_METHOD_KEY, method },
                { Core.Constants.EVIDENCE_REQUEST_PATH_KEY, path },
                { Core.Constants.EVIDENCE_REQUEST_QUERY_KEY, query },
            };
        }
    }
}
