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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FiftyOne.Pipeline.AgentSignature.Tests
{
    /// <summary>
    /// Checks the three limits placed on fetching a key directory, being the
    /// addresses the element is willing to fetch, the number of bytes it
    /// will read from a response, and its refusal to accept a document that
    /// arrived from somewhere other than the address that was asked for.
    /// The address fetched is named by a header that whoever sent the
    /// request wrote, so all three decide what one request can make this
    /// element do.
    /// </summary>
    [TestClass]
    public class DirectoryFetchSafetyTests
    {
        /// <summary>
        /// The URL the fake agent card is served from, which the 'client_id'
        /// field of the card has to match.
        /// </summary>
        private const string CardUrl = "https://example.com/bot";

        /// <summary>
        /// The label the 'Signature-Agent' member is written with.
        /// </summary>
        private const string AgentLabel = "agent1";

        /// <summary>
        /// The address a machine asks for its own credentials on inside the
        /// large cloud providers, which is the address an attacker reaches
        /// for when trying to make a server fetch its own internal
        /// services.
        /// </summary>
        private const string MetadataHost = "169.254.169.254";

        /// <summary>
        /// Where a directory that was redirected ends up, standing for the
        /// address a redirect would send the element to.
        /// </summary>
        private const string ElsewhereUrl =
            "https://elsewhere.example.com" + Constants.DIRECTORY_PATH;

        #region Addresses that may not be fetched

        /// <summary>
        /// An address is fetched only when it is HTTPS, names no user before
        /// the host, and is either a host name or a public IP address. Every
        /// other case is refused before any request is made, because the
        /// address comes from a header the sender wrote.
        /// </summary>
        /// <param name="url">The address to test.</param>
        /// <param name="expected">Whether it may be fetched.</param>
        /// <param name="why">Why, for the assertion message.</param>
        [DataTestMethod]
        [DataRow(
            "https://keys.example.com/.well-known/keys",
            true,
            "it is an ordinary host name over HTTPS")]
        [DataRow(
            "http://keys.example.com/.well-known/keys",
            false,
            "plain HTTP proves nothing about who published the keys")]
        [DataRow(
            "ftp://keys.example.com/keys",
            false,
            "only HTTPS is fetched")]
        [DataRow(
            "keys.example.com/keys",
            false,
            "it is not an absolute address")]
        [DataRow(
            "https://someone@keys.example.com/keys",
            false,
            "anything before an '@' names a user rather than a host")]
        [DataRow(
            "https://someone:secret@keys.example.com/keys",
            false,
            "anything before an '@' names a user rather than a host")]
        [DataRow(
            "https://93.184.216.34/keys",
            true,
            "it is a public IP address")]
        [DataRow(
            "https://127.0.0.1/keys",
            false,
            "it is the loopback address")]
        [DataRow(
            "https://10.1.2.3/keys",
            false,
            "10.x only appears inside a network")]
        [DataRow(
            "https://172.16.0.1/keys",
            false,
            "172.16 to 172.31 only appear inside a network")]
        [DataRow(
            "https://172.31.255.254/keys",
            false,
            "172.16 to 172.31 only appear inside a network")]
        [DataRow(
            "https://172.15.0.1/keys",
            true,
            "172.15 sits below the range held back for private use")]
        [DataRow(
            "https://172.32.0.1/keys",
            true,
            "172.32 sits above the range held back for private use")]
        [DataRow(
            "https://192.168.1.1/keys",
            false,
            "192.168.x only appears inside a network")]
        [DataRow(
            "https://169.254.169.254/latest/meta-data",
            false,
            "it is the address cloud providers answer machine " +
            "credentials on")]
        [DataRow(
            "https://100.64.0.1/keys",
            false,
            "100.64 to 100.127 is the range carriers share between " +
            "customers")]
        [DataRow(
            "https://0.0.0.0/keys",
            false,
            "it is the unspecified address")]
        [DataRow(
            "https://[::1]/keys",
            false,
            "it is the loopback address written for IPv6")]
        [DataRow(
            "https://[fc00::1]/keys",
            false,
            "fc00::/7 is held back for use inside one network")]
        [DataRow(
            "https://[fd12:3456:789a::1]/keys",
            false,
            "fc00::/7 is held back for use inside one network")]
        [DataRow(
            "https://[fe80::1]/keys",
            false,
            "it is a link local address")]
        [DataRow(
            "https://[::ffff:169.254.169.254]/latest/meta-data",
            false,
            "an IPv4 address mapped into IPv6 is read as the IPv4 " +
            "address it carries")]
        [DataRow(
            "https://[::ffff:93.184.216.34]/keys",
            true,
            "the IPv4 address it carries is a public one")]
        [DataRow(
            "https://[2606:2800:220:1:248:1893:25c8:1946]/keys",
            true,
            "it is a public IPv6 address")]
        public void AddressIsFetchedOnlyWhenItIsPublicAndOverHttps(
            string url,
            bool expected,
            string why)
        {
            Assert.AreEqual(
                expected,
                DirectoryFetcher.IsSafeUrl(url),
                "The address '" + url + "' should " +
                (expected ? "be fetched, because " : "not be fetched, " +
                    "because ") + why + ".");
        }

        /// <summary>
        /// An agent card is a document the element fetched because of a
        /// header the sender wrote, so an address the card names is checked
        /// in the same way as the header's own. A card naming the address a
        /// machine answers its own credentials on causes no request at all
        /// to that address, and the request that named the card reads
        /// Unverified with the DirectoryUnavailable reason.
        /// </summary>
        /// <param name="jwksUri">The address the card names.</param>
        [DataTestMethod]
        [DataRow("http://" + MetadataHost + "/latest/meta-data/iam")]
        [DataRow("https://" + MetadataHost + "/latest/meta-data/iam")]
        public void AgentCardNamingAnAddressThatMayNotBeFetchedIsNotFollowed(
            string jwksUri)
        {
            using (var harness = ElementHarness.Create())
            {
                harness.Handler.Add(
                    CardUrl,
                    "{\"client_id\":\"" + CardUrl + "\"," +
                    "\"client_name\":\"Example Bot\"," +
                    "\"jwks_uri\":\"" + jwksUri + "\"}",
                    Constants.JSON_MEDIA_TYPE);
                var result = ProcessCimdRequest(harness);

                Assert.AreEqual(
                    Constants.STATUS_UNVERIFIED,
                    result.AgentSignature.Value,
                    "Expected Unverified, because the only address the card " +
                    "names for its keys is one that may not be fetched. " +
                    Describe(result));
                Assert.AreEqual(
                    Constants.REASON_DIRECTORY_UNAVAILABLE,
                    result.AgentSignatureReason.Value,
                    "Expected the DirectoryUnavailable reason. " +
                    Describe(result));
                var reached = harness.Handler.RequestedUrls
                    .Where(u => u.Contains(MetadataHost))
                    .ToList();
                Assert.AreEqual(
                    0,
                    reached.Count,
                    "Expected no request at all to '" + MetadataHost +
                    "', and the element asked for " +
                    string.Join(", ", reached) + ".");
                Assert.AreEqual(
                    CardUrl,
                    harness.Handler.RequestedUrls.Single(),
                    "Expected the card itself to be the only thing " +
                    "fetched, and the element asked for " +
                    string.Join(", ", harness.Handler.RequestedUrls) + ".");
            }
        }

        #endregion

        #region Responses longer than the limit

        /// <summary>
        /// A response that states a length over the limit is refused before
        /// any of it is read, so the request reads Unverified with the
        /// DirectoryUnavailable reason.
        /// </summary>
        [TestMethod]
        public void DirectoryStatingALengthOverTheLimitIsNotRead()
        {
            var body = DirectoryBody();
            const int limit = 64;
            Assert.IsTrue(
                Encoding.UTF8.GetByteCount(body) > limit,
                "This test needs a directory longer than the " + limit +
                " byte limit it sets, and the one it serves is " +
                Encoding.UTF8.GetByteCount(body) + " bytes.");

            using (var harness = ElementHarness.Create(
                builder => builder.SetMaxResponseBytes(limit)))
            {
                harness.Handler.Add(
                    Fixtures.SignatureAgentDirectoryUrl,
                    body,
                    Constants.DIRECTORY_MEDIA_TYPE);
                var result = harness.ProcessSigned(
                    RequestSigner.Sign(new SigningOptions()));

                AssertDirectoryUnavailable(
                    result,
                    "a directory stating a length over the limit");
            }
        }

        /// <summary>
        /// A response that states no length at all is measured as its bytes
        /// arrive, so one longer than the limit is given up on part way
        /// through and the request reads Unverified with the
        /// DirectoryUnavailable reason.
        /// </summary>
        [TestMethod]
        public void DirectoryStatingNoLengthOverTheLimitIsNotRead()
        {
            var body = DirectoryBody();
            const int limit = 64;
            using (var content = new UnmeasuredContent(body))
            {
                // The whole point of this test is the response that states
                // no length, so a body that quietly states one would leave
                // it checking the same thing as the test above.
                Assert.IsNull(
                    content.Headers.ContentLength,
                    "The body this test serves should state no length, and " +
                    "it states " + content.Headers.ContentLength + ".");
            }

            using (var harness = ElementHarness.Create(
                builder => builder.SetMaxResponseBytes(limit)))
            {
                harness.Handler.Add(
                    Fixtures.SignatureAgentDirectoryUrl,
                    new FakeResponse
                    {
                        Body = body,
                        MediaType = Constants.DIRECTORY_MEDIA_TYPE,
                        DeclareLength = false,
                    });
                var result = harness.ProcessSigned(
                    RequestSigner.Sign(new SigningOptions()));

                AssertDirectoryUnavailable(
                    result,
                    "a directory stating no length whose body is over the " +
                    "limit");
            }
        }

        /// <summary>
        /// A directory exactly as long as the limit allows is read as
        /// normal, whether or not the response states its length, so the
        /// two tests above fail on the length rather than on the limit
        /// refusing everything.
        /// </summary>
        /// <param name="declareLength">
        /// Whether the response states how long its body is.
        /// </param>
        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void DirectoryAsLongAsTheLimitAllowsIsRead(bool declareLength)
        {
            var body = DirectoryBody();
            var limit = Encoding.UTF8.GetByteCount(body);

            using (var harness = ElementHarness.Create(
                builder => builder.SetMaxResponseBytes(limit)))
            {
                harness.Handler.Add(
                    Fixtures.SignatureAgentDirectoryUrl,
                    new FakeResponse
                    {
                        Body = body,
                        MediaType = Constants.DIRECTORY_MEDIA_TYPE,
                        DeclareLength = declareLength,
                    });
                var result = harness.ProcessSigned(
                    RequestSigner.Sign(new SigningOptions()));

                Assert.AreEqual(
                    Constants.STATUS_VERIFIED,
                    result.AgentSignature.Value,
                    "Expected Verified for a directory of " + limit +
                    " bytes with a limit of " + limit + " bytes, which the " +
                    "response " + (declareLength ? "states" : "does not " +
                        "state") + ". " + Describe(result));
            }
        }

        #endregion

        #region Documents that arrived from somewhere else

        /// <summary>
        /// The well known path is what ties a key set to a domain, so a
        /// document that arrived from another address is not the document
        /// that was asked for. The element throws it away and reads
        /// Unverified with the DirectoryUnavailable reason, and the same
        /// document served from the address that was asked for verifies.
        /// </summary>
        [TestMethod]
        public void RedirectedDirectoryIsThrownAway()
        {
            var body = DirectoryBody();
            var signed = RequestSigner.Sign(new SigningOptions());

            using (var harness = ElementHarness.Create())
            {
                harness.Handler.Add(
                    Fixtures.SignatureAgentDirectoryUrl,
                    new FakeResponse
                    {
                        Body = body,
                        MediaType = Constants.DIRECTORY_MEDIA_TYPE,
                        FinalUrl = ElsewhereUrl,
                    });
                var result = harness.ProcessSigned(signed);

                AssertDirectoryUnavailable(
                    result, "a directory that arrived from '" +
                    ElsewhereUrl + "'");
                Assert.AreEqual(
                    1,
                    harness.Handler.CallCount,
                    "Expected the directory to have been asked for once and " +
                    "its answer thrown away, and there were " +
                    harness.Handler.CallCount + " requests.");
            }

            using (var harness = ElementHarness.Create())
            {
                harness.Handler.Add(
                    Fixtures.SignatureAgentDirectoryUrl,
                    body,
                    Constants.DIRECTORY_MEDIA_TYPE);
                var result = harness.ProcessSigned(signed);

                Assert.AreEqual(
                    Constants.STATUS_VERIFIED,
                    result.AgentSignature.Value,
                    "Expected Verified for the same directory served from " +
                    "the address that was asked for, so the test above " +
                    "fails on the address it arrived from rather than on " +
                    "the document itself. " + Describe(result));
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// The key directory that serves the public part of the Ed25519 test
        /// key, built here rather than by the fake handler so that a test
        /// knows how many bytes it is.
        /// </summary>
        /// <returns>The directory document.</returns>
        private static string DirectoryBody()
        {
            return "{\"keys\":[" +
                RequestSigner.PublicPart(Fixtures.Ed25519Key()) + "]}";
        }

        /// <summary>
        /// Sign a request that names the fake agent card with a 'cimd'
        /// member and run it through the element.
        /// </summary>
        /// <param name="harness">The harness to run it through.</param>
        /// <returns>What the element made of the request.</returns>
        private static IAgentSignatureData ProcessCimdRequest(
            ElementHarness harness)
        {
            var member = "\"" + CardUrl + "\";type=" +
                Constants.AGENT_TYPE_CIMD;
            // The signer helper writes the plain form of the header, so the
            // member with a type is built here instead and added as a
            // covered component of its own.
            var options = new SigningOptions
            {
                SignatureAgent = null,
            };
            options.ExtraComponents.Add(
                new KeyValuePair<string, string>(
                    "\"signature-agent\";key=\"" + AgentLabel + "\"",
                    member));
            var signed = RequestSigner.Sign(options);

            return harness.Process(new Dictionary<string, string>
            {
                { Constants.EVIDENCE_SIGNATURE_KEY, signed.Signature },
                {
                    Constants.EVIDENCE_SIGNATURE_INPUT_KEY,
                    signed.SignatureInput
                },
                {
                    Constants.EVIDENCE_SIGNATURE_AGENT_KEY,
                    AgentLabel + "=" + member
                },
                { Constants.EVIDENCE_HOST_KEY, "example.com" },
                { Core.Constants.EVIDENCE_PROTOCOL, "https" },
            });
        }

        /// <summary>
        /// Check that the element could not obtain the keys.
        /// </summary>
        /// <param name="result">What the element made of the request.</param>
        /// <param name="what">What was served, for the message.</param>
        private static void AssertDirectoryUnavailable(
            IAgentSignatureData result,
            string what)
        {
            Assert.AreEqual(
                Constants.STATUS_UNVERIFIED,
                result.AgentSignature.Value,
                "Expected Unverified for " + what + ", because no key was " +
                "obtained. " + Describe(result));
            Assert.AreEqual(
                Constants.REASON_DIRECTORY_UNAVAILABLE,
                result.AgentSignatureReason.Value,
                "Expected the DirectoryUnavailable reason for " + what +
                ". " + Describe(result));
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
