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
using FiftyOne.Pipeline.AgentSignature.FlowElement;
using FiftyOne.Pipeline.AgentSignature.Keys;
using FiftyOne.Pipeline.AgentSignature.Tests.Helpers;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading;

namespace FiftyOne.Pipeline.AgentSignature.Tests
{
    /// <summary>
    /// Reaches the key directories that two real agents publish, so that the
    /// element is known to work against what is actually served rather than
    /// only against the standard's test vectors. Nothing else in this project
    /// touches the network.
    /// </summary>
    /// <remarks>
    /// This repository has no way of excluding a test from a run, because
    /// ci/run-unit-tests.ps1 selects test libraries by file name and passes
    /// no test filter, and no test in the repository carries a category or
    /// is ignored. These tests therefore gate themselves on an environment
    /// variable and report Inconclusive when it is not set, which leaves a
    /// normal run green whilst saying plainly that they did not run.
    /// </remarks>
    [TestClass]
    public class NetworkTests
    {
        /// <summary>
        /// The environment variable that has to be set to '1' for these
        /// tests to reach the network.
        /// </summary>
        public const string NetworkTestsVariable =
            "FIFTYONE_AGENT_SIGNATURE_NETWORK_TESTS";

        /// <summary>
        /// The origin of the research service that Cloudflare runs, which
        /// serves the Ed25519 key from the RFC 9421 examples.
        /// </summary>
        private const string ResearchOrigin =
            "https://http-message-signatures-example.research.cloudflare.com";

        /// <summary>
        /// The origin of the agent OpenAI runs.
        /// </summary>
        private const string ChatGptOrigin = "https://chatgpt.com";

        /// <summary>
        /// How long a live fetch is given.
        /// </summary>
        private static readonly TimeSpan LiveTimeout =
            TimeSpan.FromSeconds(30);

        /// <summary>
        /// The key directory of the Cloudflare research service parses and
        /// carries at least one key, and its key ids and thumbprints are
        /// reported so that a change at that service can be seen.
        /// </summary>
        [TestMethod]
        [TestCategory("Network")]
        public void ResearchDirectoryServesKeys()
        {
            RequireNetworkTests();
            ReportDirectory(ResearchOrigin);
        }

        /// <summary>
        /// The key directory of the agent OpenAI runs parses and carries at
        /// least one key, and its key ids and thumbprints are reported so
        /// that a change at that agent can be seen.
        /// </summary>
        [TestMethod]
        [TestCategory("Network")]
        public void ChatGptDirectoryServesKeys()
        {
            RequireNetworkTests();
            ReportDirectory(ChatGptOrigin);
        }

        /// <summary>
        /// A request signed here and now with the Ed25519 key from the
        /// RFC 9421 examples verifies against the live key directory of the
        /// Cloudflare research service, which serves that same key.
        /// </summary>
        [TestMethod]
        [TestCategory("Network")]
        public void SignedRequestVerifiesAgainstTheResearchDirectory()
        {
            RequireNetworkTests();

            var now = DateTimeOffset.UtcNow;
            var signed = RequestSigner.Sign(new SigningOptions
            {
                SignatureAgent = ResearchOrigin,
                Created = now.AddSeconds(-5),
                Expires = now.AddMinutes(5),
            });

            using (var client = new HttpClient())
            {
                client.Timeout = LiveTimeout;
                var element = new AgentSignatureElementBuilder(
                    NullLoggerFactory.Instance)
                    .SetHttpClient(client)
                    .SetWaitBudget(LiveTimeout)
                    .SetFetchTimeout(LiveTimeout)
                    .Build();
                using (var pipeline = new PipelineBuilder(
                    NullLoggerFactory.Instance)
                    .AddFlowElement(element)
                    .Build())
                using (var data = pipeline.CreateFlowData())
                {
                    data.AddEvidence(
                        Constants.EVIDENCE_SIGNATURE_KEY, signed.Signature);
                    data.AddEvidence(
                        Constants.EVIDENCE_SIGNATURE_INPUT_KEY,
                        signed.SignatureInput);
                    data.AddEvidence(
                        Constants.EVIDENCE_SIGNATURE_AGENT_KEY,
                        signed.SignatureAgent);
                    data.AddEvidence(
                        Constants.EVIDENCE_HOST_KEY, "example.com");
                    data.AddEvidence(Core.Constants.EVIDENCE_PROTOCOL, "https");
                    data.Process();

                    var result = data.Get<IAgentSignatureData>();
                    Report(
                        "A request signed with the RFC 9421 Ed25519 test " +
                        "key, checked against " + ResearchOrigin + ", read " +
                        Describe(result));
                    Assert.AreEqual(
                        Constants.STATUS_VERIFIED,
                        result.AgentSignature.Value,
                        "Expected Verified, because the research service " +
                        "publishes the Ed25519 key from the RFC 9421 " +
                        "examples, which is the key the request was signed " +
                        "with. " + Describe(result));
                    Assert.AreEqual(
                        Constants.REASON_VERIFIED,
                        result.AgentSignatureReason.Value,
                        "Expected the Verified reason. " + Describe(result));
                    Assert.AreEqual(
                        Fixtures.Ed25519Thumbprint,
                        result.AgentSignatureKeyId.Value,
                        "Expected the key id to be the thumbprint of the " +
                        "RFC 9421 Ed25519 test key. " + Describe(result));
                }
            }
        }

        /// <summary>
        /// Fetch the key directory the given origin publishes, check that it
        /// parses and carries at least one key, and report what it holds.
        /// </summary>
        /// <param name="origin">The origin to fetch from.</param>
        private static void ReportDirectory(string origin)
        {
            var url = origin + Constants.DIRECTORY_PATH;
            using (var client = new HttpClient())
            {
                client.Timeout = LiveTimeout;
                var fetcher = new DirectoryFetcher(
                    client,
                    NullLogger.Instance,
                    () => DateTimeOffset.UtcNow,
                    Constants.DEFAULT_MAX_RESPONSE_BYTES);
                DirectoryEntry entry;
                using (var source = new CancellationTokenSource(LiveTimeout))
                {
                    entry = fetcher.FetchAsync(
                        url,
                        Constants.AGENT_TYPE_DIRECTORY,
                        source.Token)
                        .GetAwaiter()
                        .GetResult();
                }

                Assert.IsTrue(
                    entry.Success,
                    "Expected the key directory at '" + url + "' to be " +
                    "read, and it was not because '" +
                    (entry.FailureReason ?? "no reason was given") + "'.");
                Assert.IsTrue(
                    entry.Directory.Keys.Count > 0,
                    "Expected at least one key in the directory at '" + url +
                    "', and it held " + entry.Directory.Keys.Count + ".");
                Report(Summarise(url, entry));
            }
        }

        /// <summary>
        /// Write one line about a key directory, naming each key id and each
        /// thumbprint, so that a run of these tests says what the two live
        /// services were serving at the time.
        /// </summary>
        /// <param name="url">The URL the directory came from.</param>
        /// <param name="entry">What was fetched.</param>
        /// <returns>The description.</returns>
        private static string Summarise(string url, DirectoryEntry entry)
        {
            var text = new StringBuilder();
            text.Append(url)
                .Append(" served ")
                .Append(entry.Directory.Keys.Count.ToString(
                    CultureInfo.InvariantCulture))
                .Append(" keys, purpose '")
                .Append(entry.Directory.Purpose ?? "not stated")
                .Append("'.");
            foreach (var key in entry.Directory.Keys)
            {
                text.Append(" [kty ")
                    .Append(key.KeyType ?? "not stated")
                    .Append(", crv ")
                    .Append(key.Curve ?? "not stated")
                    .Append(", kid ")
                    .Append(string.IsNullOrEmpty(key.KeyId)
                        ? "not stated"
                        : key.KeyId)
                    .Append(", thumbprint ")
                    .Append(string.IsNullOrEmpty(key.Thumbprint)
                        ? "not computed"
                        : key.Thumbprint)
                    .Append("]");
            }
            if (entry.Directory.TimesWereInMilliseconds)
            {
                text.Append(" At least one key time was in milliseconds " +
                    "rather than the seconds the drafts specify.");
            }
            return text.ToString();
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

        /// <summary>
        /// Write a line of the report these tests produce.
        /// </summary>
        /// <param name="text">The line.</param>
        private static void Report(string text)
        {
            Console.WriteLine(text);
        }

        /// <summary>
        /// End the test as Inconclusive unless the environment variable that
        /// allows these tests to reach the network is set to '1'.
        /// </summary>
        private static void RequireNetworkTests()
        {
            var value = Environment.GetEnvironmentVariable(
                NetworkTestsVariable);
            if (string.Equals(value, "1", StringComparison.Ordinal) == false)
            {
                Assert.Inconclusive(
                    "This test reaches the live key directories, so it runs " +
                    "only when " + NetworkTestsVariable + " is set to '1'. " +
                    "It was " + (value == null
                        ? "not set"
                        : "set to '" + value + "'") + ".");
            }
        }
    }
}
