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
using FiftyOne.Pipeline.AgentSignature.Tests.Helpers;
using FiftyOne.Pipeline.Core.Configuration;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.AgentSignature.Tests
{
    /// <summary>
    /// Checks that every setting the builder offers reaches the element and
    /// changes what the element does, that an element can be built from a
    /// pipeline configuration file, and that the element disposes the client
    /// it made itself whilst leaving a client it was given alone.
    /// </summary>
    [TestClass]
    public class BuilderTests
    {
        /// <summary>
        /// The first registry URL used when checking that registries add up
        /// rather than replace one another.
        /// </summary>
        private const string FirstRegistry =
            "https://registry.example.com/first.txt";

        /// <summary>
        /// The second registry URL used when checking that registries add up
        /// rather than replace one another.
        /// </summary>
        private const string SecondRegistry =
            "https://registry.example.com/second.txt";

        /// <summary>
        /// The configuration file that builds a pipeline holding the element.
        /// </summary>
        private const string OptionsFileName = "TestPipelineOptions.json";

        /// <summary>
        /// The cache lifetime the configuration file asks for.
        /// </summary>
        private static readonly TimeSpan OptionsFileCacheLifetime =
            TimeSpan.FromHours(1);

        /// <summary>
        /// The cache size the configuration file asks for.
        /// </summary>
        private const int OptionsFileCacheSize = 25;

        /// <summary>
        /// The number of agents used to crowd a cache that has been told to
        /// hold one directory.
        /// </summary>
        private const int CrowdedAgentCount = 4;

        /// <summary>
        /// Every 'Set' method on the builder puts its value into the settings
        /// the element is built with, and calling SetRegistry twice adds two
        /// registries rather than replacing the first.
        /// </summary>
        [TestMethod]
        public void EverySetMethodReachesTheSettings()
        {
            using (var httpClient = new HttpClient())
            {
                var builder = new TestBuilder();
                builder
                    .SetHttpClient(httpClient)
                    .SetCacheSize(17)
                    .SetCacheLifetime(TimeSpan.FromMinutes(11))
                    .SetNegativeCacheLifetime(TimeSpan.FromMinutes(7))
                    .SetWaitBudget(TimeSpan.FromMilliseconds(123))
                    .SetFetchTimeout(TimeSpan.FromSeconds(3))
                    .SetClockSkew(TimeSpan.FromSeconds(90))
                    .SetMaxLifetime(TimeSpan.FromMinutes(5))
                    .SetRegistry(FirstRegistry)
                    .SetRegistry(SecondRegistry)
                    .SetAllowLegacySignatureAgent(false);

                var settings = builder.Settings;
                Assert.AreSame(
                    httpClient,
                    settings.HttpClient,
                    "Expected the client given to the builder.");
                Assert.AreEqual(
                    17,
                    settings.CacheSize,
                    "Expected the cache size given to the builder.");
                Assert.AreEqual(
                    TimeSpan.FromMinutes(11),
                    settings.CacheLifetime,
                    "Expected the cache lifetime given to the builder.");
                Assert.AreEqual(
                    TimeSpan.FromMinutes(7),
                    settings.NegativeCacheLifetime,
                    "Expected the negative cache lifetime given to the " +
                    "builder.");
                Assert.AreEqual(
                    TimeSpan.FromMilliseconds(123),
                    settings.WaitBudget,
                    "Expected the wait budget given to the builder.");
                Assert.AreEqual(
                    TimeSpan.FromSeconds(3),
                    settings.FetchTimeout,
                    "Expected the fetch timeout given to the builder.");
                Assert.AreEqual(
                    TimeSpan.FromSeconds(90),
                    settings.ClockSkew,
                    "Expected the clock skew given to the builder.");
                Assert.AreEqual(
                    TimeSpan.FromMinutes(5),
                    settings.MaxLifetime,
                    "Expected the maximum lifetime given to the builder.");
                Assert.IsFalse(
                    settings.AllowLegacySignatureAgent,
                    "Expected the legacy header form to be refused, " +
                    "because the builder was told to refuse it.");
                Assert.AreEqual(
                    2,
                    settings.Registries.Count,
                    "Expected two registries, because SetRegistry was " +
                    "called twice, and there were " +
                    settings.Registries.Count + ".");
                Assert.AreEqual(
                    FirstRegistry,
                    settings.Registries[0],
                    "Expected the first registry given to the builder.");
                Assert.AreEqual(
                    SecondRegistry,
                    settings.Registries[1],
                    "Expected the second registry given to the builder.");
            }
        }

        /// <summary>
        /// The client given to the builder is the one the element fetches
        /// keys with, which is what lets every other test in this project
        /// answer fetches without a network.
        /// </summary>
        [TestMethod]
        public void ClientGivenToTheBuilderIsTheOneUsedForFetches()
        {
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var result = harness.ProcessSigned(
                    RequestSigner.Sign(new SigningOptions()));

                Assert.AreEqual(
                    Constants.STATUS_VERIFIED,
                    result.AgentSignature.Value,
                    "Expected Verified, because the key came from the " +
                    "client the builder was given. " + Describe(result));
                Assert.AreEqual(
                    1,
                    harness.Handler.CallCount,
                    "Expected one fetch through the client the builder was " +
                    "given, and there were " + harness.Handler.CallCount +
                    ". " + Describe(result));
                Assert.AreEqual(
                    Fixtures.SignatureAgentDirectoryUrl,
                    harness.Handler.RequestedUrls.Single(),
                    "Expected the key directory of the agent the signature " +
                    "names to have been fetched. " + Describe(result));
            }
        }

        /// <summary>
        /// A cache told to hold one directory drops the directory it holds as
        /// soon as another agent arrives, so a second run of the same agents
        /// fetches every one of them again, whilst the default size holds
        /// them all and fetches nothing twice.
        /// </summary>
        [TestMethod]
        public void CacheSizeBoundsWhatTheElementRemembers()
        {
            var bounded = FetchesForTwoPassesOverManyAgents(1);
            Assert.AreEqual(
                CrowdedAgentCount * 2,
                bounded,
                "Expected " + (CrowdedAgentCount * 2) + " fetches for two " +
                "passes over " + CrowdedAgentCount + " agents with a cache " +
                "told to hold one directory, because each agent in turn " +
                "drops the one before it, and there were " + bounded + ".");

            var roomy = FetchesForTwoPassesOverManyAgents(
                Constants.DEFAULT_CACHE_SIZE);
            Assert.AreEqual(
                CrowdedAgentCount,
                roomy,
                "Expected one fetch for each of the " + CrowdedAgentCount +
                " agents with the default cache size, because every " +
                "directory is still held on the second pass, and there " +
                "were " + roomy + ".");
        }

        /// <summary>
        /// Once a directory is older than the cache lifetime the next request
        /// that needs it starts a fresh fetch, and a directory younger than
        /// the lifetime is reused.
        /// </summary>
        [TestMethod]
        public void CacheLifetimeDecidesWhenADirectoryIsFetchedAgain()
        {
            using (var harness = ElementHarness.CreateWithTestKey(
                builder => builder.SetCacheLifetime(TimeSpan.FromMinutes(10))))
            {
                var signed = RequestSigner.Sign(new SigningOptions());
                harness.ProcessSigned(signed);
                Assert.AreEqual(
                    1,
                    harness.FetchCount,
                    "Expected one fetch for the first request.");

                harness.Now = harness.Now.AddMinutes(5);
                harness.ProcessSigned(signed);
                Assert.AreEqual(
                    1,
                    harness.FetchCount,
                    "Expected the directory to be reused after five " +
                    "minutes, because the lifetime is ten minutes, and " +
                    "there were " + harness.FetchCount + " fetches.");

                harness.Now = harness.Now.AddMinutes(10);
                var result = harness.ProcessSigned(signed);
                Assert.AreEqual(
                    2,
                    harness.FetchCount,
                    "Expected a second fetch after fifteen minutes, " +
                    "because the lifetime is ten minutes, and there were " +
                    harness.FetchCount + " fetches. " + Describe(result));
            }
        }

        /// <summary>
        /// A failed fetch is remembered for the negative cache lifetime, so
        /// an agent that is having an outage is not fetched again on every
        /// request, and is fetched again once that period has passed.
        /// </summary>
        [TestMethod]
        public void NegativeCacheLifetimeHoldsBackAFreshFetch()
        {
            using (var harness = ElementHarness.Create(
                builder => builder.SetNegativeCacheLifetime(
                    TimeSpan.FromMinutes(10))))
            {
                harness.Handler.AddStatus(
                    Fixtures.SignatureAgentDirectoryUrl,
                    HttpStatusCode.InternalServerError);
                var signed = RequestSigner.Sign(new SigningOptions());

                var failed = harness.ProcessSigned(signed);
                Assert.AreEqual(
                    Constants.STATUS_UNVERIFIED,
                    failed.AgentSignature.Value,
                    "Expected Unverified, because the key directory " +
                    "answered with a server error. " + Describe(failed));

                harness.Handler.AddDirectory(
                    Fixtures.SignatureAgentDirectoryUrl,
                    RequestSigner.PublicPart(Fixtures.Ed25519Key()));
                harness.Now = harness.Now.AddMinutes(5);
                var remembered = harness.ProcessSigned(signed);
                Assert.AreEqual(
                    1,
                    harness.FetchCount,
                    "Expected the failure to be remembered after five " +
                    "minutes, because the negative lifetime is ten " +
                    "minutes, and there were " + harness.FetchCount +
                    " fetches. " + Describe(remembered));

                harness.Now = harness.Now.AddMinutes(10);
                var fresh = harness.ProcessSigned(signed);
                Assert.AreEqual(
                    Constants.STATUS_VERIFIED,
                    fresh.AgentSignature.Value,
                    "Expected Verified after fifteen minutes, because the " +
                    "failure is forgotten after ten and the agent is " +
                    "answering again. " + Describe(fresh));
            }
        }

        /// <summary>
        /// A request waits for a key directory fetch only for the wait
        /// budget, after which it reports Timeout whilst the fetch keeps
        /// running.
        /// </summary>
        [TestMethod]
        public void WaitBudgetEndsTheWaitWithTimeout()
        {
            using (var harness = ElementHarness.CreateWithTestKey(
                builder => builder.SetWaitBudget(
                    TimeSpan.FromMilliseconds(50))))
            {
                harness.Handler.Hold();
                try
                {
                    var result = harness.ProcessSigned(
                        RequestSigner.Sign(new SigningOptions()));

                    Assert.AreEqual(
                        Constants.STATUS_TIMEOUT,
                        result.AgentSignature.Value,
                        "Expected Timeout, because the fetch was held open " +
                        "for longer than the wait budget. " +
                        Describe(result));
                    Assert.AreEqual(
                        Constants.REASON_DIRECTORY_PENDING,
                        result.AgentSignatureReason.Value,
                        "Expected the DirectoryPending reason. " +
                        Describe(result));
                }
                finally
                {
                    harness.Handler.Release();
                }
            }
        }

        /// <summary>
        /// A fetch that takes longer than the fetch timeout is given up on,
        /// so the request reads Unverified rather than waiting out its whole
        /// wait budget.
        /// </summary>
        [TestMethod]
        public void FetchTimeoutEndsAFetchThatTakesTooLong()
        {
            using (var harness = ElementHarness.CreateWithTestKey(
                builder => builder
                    .SetFetchTimeout(TimeSpan.FromMilliseconds(100))
                    .SetWaitBudget(TimeSpan.FromSeconds(10))))
            {
                harness.Handler.Hold();
                try
                {
                    var result = harness.ProcessSigned(
                        RequestSigner.Sign(new SigningOptions()));

                    Assert.AreEqual(
                        Constants.STATUS_UNVERIFIED,
                        result.AgentSignature.Value,
                        "Expected Unverified, because the fetch was given " +
                        "up on before the wait budget ran out. " +
                        Describe(result));
                    Assert.AreEqual(
                        Constants.REASON_DIRECTORY_UNAVAILABLE,
                        result.AgentSignatureReason.Value,
                        "Expected the DirectoryUnavailable reason. " +
                        Describe(result));
                }
                finally
                {
                    harness.Handler.Release();
                }
            }
        }

        /// <summary>
        /// A signature made a little ahead of this machine's clock is
        /// accepted within the clock skew and refused once the skew is
        /// removed.
        /// </summary>
        [TestMethod]
        public void ClockSkewDecidesWhetherAFutureSignatureIsAccepted()
        {
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var signed = RequestSigner.Sign(new SigningOptions
                {
                    Created = harness.Now.AddSeconds(30),
                });
                var result = harness.ProcessSigned(signed);
                Assert.AreEqual(
                    Constants.STATUS_VERIFIED,
                    result.AgentSignature.Value,
                    "Expected Verified, because the signature was made 30 " +
                    "seconds ahead and the default skew is 60 seconds. " +
                    Describe(result));
            }

            using (var harness = ElementHarness.CreateWithTestKey(
                builder => builder.SetClockSkew(TimeSpan.Zero)))
            {
                var signed = RequestSigner.Sign(new SigningOptions
                {
                    Created = harness.Now.AddSeconds(30),
                });
                var result = harness.ProcessSigned(signed);
                Assert.AreEqual(
                    Constants.STATUS_INVALID,
                    result.AgentSignature.Value,
                    "Expected Invalid, because the signature was made 30 " +
                    "seconds ahead and no skew is allowed. " +
                    Describe(result));
                Assert.AreEqual(
                    Constants.REASON_NOT_YET_VALID,
                    result.AgentSignatureReason.Value,
                    "Expected the NotYetValid reason. " + Describe(result));
            }
        }

        /// <summary>
        /// A signature valid for longer than the maximum lifetime reads
        /// Invalid with the Expired reason, and the default of zero places no
        /// limit at all so the same signature verifies.
        /// </summary>
        [TestMethod]
        public void MaxLifetimeShorterThanTheSignatureReadsExpired()
        {
            var signed = RequestSigner.Sign(new SigningOptions());

            using (var harness = ElementHarness.CreateWithTestKey(
                builder => builder.SetMaxLifetime(TimeSpan.FromHours(1))))
            {
                var result = harness.ProcessSigned(signed);
                Assert.AreEqual(
                    Constants.STATUS_INVALID,
                    result.AgentSignature.Value,
                    "Expected Invalid, because the signature is valid for " +
                    "far longer than the hour the element allows. " +
                    Describe(result));
                Assert.AreEqual(
                    Constants.REASON_EXPIRED,
                    result.AgentSignatureReason.Value,
                    "Expected the Expired reason. " + Describe(result));
            }

            using (var harness = ElementHarness.CreateWithTestKey())
            {
                Assert.AreEqual(
                    TimeSpan.Zero,
                    Constants.DEFAULT_MAX_LIFETIME,
                    "Expected the default maximum lifetime to be zero, " +
                    "which places no limit.");
                var result = harness.ProcessSigned(signed);
                Assert.AreEqual(
                    Constants.STATUS_VERIFIED,
                    result.AgentSignature.Value,
                    "Expected Verified, because the default places no " +
                    "limit on how long a signature may be valid for. " +
                    Describe(result));
            }
        }

        /// <summary>
        /// The bare quoted string form of the 'Signature-Agent' header, which
        /// the earlier drafts used, is accepted by default and reads
        /// Malformed once the builder is told to refuse it.
        /// </summary>
        [TestMethod]
        public void AllowLegacySignatureAgentDecidesWhetherTheOldFormIsRead()
        {
            var signed = RequestSigner.Sign(new SigningOptions
            {
                SignatureAgentLabel = null,
            });

            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var result = harness.ProcessSigned(signed);
                Assert.AreEqual(
                    Constants.STATUS_VERIFIED,
                    result.AgentSignature.Value,
                    "Expected Verified, because the bare quoted string form " +
                    "is accepted by default. " + Describe(result));
            }

            using (var harness = ElementHarness.CreateWithTestKey(
                builder => builder.SetAllowLegacySignatureAgent(false)))
            {
                var result = harness.ProcessSigned(signed);
                Assert.AreEqual(
                    Constants.STATUS_INVALID,
                    result.AgentSignature.Value,
                    "Expected Invalid, because the bare quoted string form " +
                    "was refused. " + Describe(result));
                Assert.AreEqual(
                    Constants.REASON_MALFORMED,
                    result.AgentSignatureReason.Value,
                    "Expected the Malformed reason. " + Describe(result));
            }
        }

        /// <summary>
        /// A pipeline built from a configuration file, which names the
        /// builder and gives it a period and a whole number as text, holds
        /// the element, gives it the settings the file asks for and processes
        /// a request.
        /// </summary>
        [TestMethod]
        public void PipelineIsBuiltFromAConfigurationFile()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(
                    System.IO.Path.GetDirectoryName(
                        typeof(BuilderTests).Assembly.Location))
                .AddJsonFile(OptionsFileName)
                .Build();
            var options = configuration
                .GetSection("PipelineOptions")
                .Get<PipelineOptions>();
            Assert.IsNotNull(
                options,
                "Expected the configuration file '" + OptionsFileName +
                "' to hold a 'PipelineOptions' section.");

            using (var pipeline = new PipelineBuilder(
                NullLoggerFactory.Instance).BuildFromConfiguration(options))
            {
                var element = pipeline.FlowElements
                    .OfType<AgentSignatureElement>()
                    .SingleOrDefault();
                Assert.IsNotNull(
                    element,
                    "Expected the pipeline to hold one agent signature " +
                    "element, and it held " + string.Join(
                        ", ",
                        pipeline.FlowElements.Select(
                            e => e.GetType().Name)) + ".");
                Assert.AreEqual(
                    Constants.DEFAULT_ELEMENT_DATA_KEY,
                    element.ElementDataKey,
                    "Expected the element data key of the agent signature " +
                    "element.");

                var settings = Settings(element);
                Assert.AreEqual(
                    OptionsFileCacheLifetime,
                    settings.CacheLifetime,
                    "Expected the cache lifetime the configuration file " +
                    "asks for, written as text and read as a period.");
                Assert.AreEqual(
                    OptionsFileCacheSize,
                    settings.CacheSize,
                    "Expected the cache size the configuration file asks " +
                    "for, written as text and read as a whole number.");

                using (var data = pipeline.CreateFlowData())
                {
                    data.AddEvidence(
                        Constants.EVIDENCE_HOST_KEY, "example.com");
                    data.AddEvidence(
                        Core.Constants.EVIDENCE_PROTOCOL, "https");
                    data.Process();
                    var result = data.Get<IAgentSignatureData>();
                    Assert.AreEqual(
                        Constants.STATUS_ABSENT,
                        result.AgentSignature.Value,
                        "Expected Absent from the element built by the " +
                        "configuration file, because the request carried " +
                        "no signature. " + Describe(result));
                    Assert.AreEqual(
                        Constants.REASON_NO_SIGNATURE,
                        result.AgentSignatureReason.Value,
                        "Expected the NoSignature reason. " +
                        Describe(result));
                }
            }
        }

        /// <summary>
        /// Disposing the pipeline disposes the client the element made for
        /// itself, and leaves a client the element was given open, because
        /// whoever supplied that client owns it.
        /// </summary>
        [TestMethod]
        public void DisposingThePipelineDisposesOnlyTheClientTheElementOwns()
        {
            using (var handler = new RecordingHandler())
            {
                // The client is told to dispose the handler with itself, so
                // a handler that records being disposed shows whether the
                // element disposed the client it was given.
                var injected = new HttpClient(handler, true);
                var element = new AgentSignatureElementBuilder(
                    NullLoggerFactory.Instance)
                    .SetHttpClient(injected)
                    .Build();
                using (var pipeline = new PipelineBuilder(
                    NullLoggerFactory.Instance)
                    .AddFlowElement(element)
                    .Build())
                {
                    Assert.AreEqual(
                        0,
                        handler.DisposeCount,
                        "Expected the handler to be open whilst the " +
                        "pipeline is.");
                }

                Assert.AreEqual(
                    0,
                    handler.DisposeCount,
                    "Expected the given client to be left open, because " +
                    "whoever supplied it owns it, and its handler was " +
                    "disposed " + handler.DisposeCount + " times.");
                var failure = RequestFailure(injected);
                Assert.IsNull(
                    failure,
                    "Expected a request through the given client to work " +
                    "after the pipeline was disposed, and it failed with " +
                    (failure == null ? "nothing" : failure.GetType().Name +
                        " saying '" + failure.Message + "'."));
                injected.Dispose();
                Assert.AreEqual(
                    1,
                    handler.DisposeCount,
                    "Expected the handler to be disposed once the client " +
                    "that owns it is.");
            }

            var owningElement = new AgentSignatureElementBuilder(
                NullLoggerFactory.Instance).Build();
            // The element makes this client for itself and never hands it
            // out, so the only way to make a request through it after the
            // pipeline is disposed is to read the field it is held in.
            var owned = OwnedClient(owningElement);
            Assert.IsNotNull(
                owned,
                "Expected the element to have made a client of its own.");
            using (var pipeline = new PipelineBuilder(
                NullLoggerFactory.Instance)
                .AddFlowElement(owningElement)
                .Build())
            {
            }

            var ownedFailure = RequestFailure(owned);
            Assert.IsInstanceOfType(
                ownedFailure,
                typeof(ObjectDisposedException),
                "Expected a request through the client the element made " +
                "for itself to fail because that client was disposed with " +
                "the pipeline, and it failed with " +
                (ownedFailure == null
                    ? "nothing"
                    : ownedFailure.GetType().Name + " saying '" +
                        ownedFailure.Message + "'."));
        }

        /// <summary>
        /// Run a crowd of agents through one element twice, in the same
        /// order each time, and report how many key directory fetches that
        /// took.
        /// </summary>
        /// <param name="cacheSize">The cache size to build with.</param>
        /// <returns>The number of fetches.</returns>
        private static int FetchesForTwoPassesOverManyAgents(int cacheSize)
        {
            var origins = new List<string>();
            for (var i = 0; i < CrowdedAgentCount; i++)
            {
                origins.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "https://agent{0}.example.com",
                    i));
            }

            using (var harness = ElementHarness.Create(
                builder => builder.SetCacheSize(cacheSize)))
            {
                foreach (var origin in origins)
                {
                    harness.Handler.AddDirectory(
                        origin + Constants.DIRECTORY_PATH,
                        RequestSigner.PublicPart(Fixtures.Ed25519Key()));
                }
                for (var pass = 0; pass < 2; pass++)
                {
                    foreach (var origin in origins)
                    {
                        AssertVerifiedForAgent(harness, origin);
                    }
                }
                return harness.FetchCount;
            }
        }

        /// <summary>
        /// Sign a request naming the given agent, run it and check that it
        /// verified, so that a fetch count is never read from a run that went
        /// wrong for some other reason.
        /// </summary>
        /// <param name="harness">The harness to run it through.</param>
        /// <param name="origin">The origin the agent publishes keys at.</param>
        private static void AssertVerifiedForAgent(
            ElementHarness harness,
            string origin)
        {
            var result = harness.ProcessSigned(
                RequestSigner.Sign(new SigningOptions
                {
                    SignatureAgent = origin,
                }));
            Assert.AreEqual(
                Constants.STATUS_VERIFIED,
                result.AgentSignature.Value,
                "Expected Verified for the agent at '" + origin +
                "', because its directory serves the key the request was " +
                "signed with. " + Describe(result));
        }

        /// <summary>
        /// Make a request through the given client and hand back whatever it
        /// failed with, or null when it worked.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <returns>The failure, or null.</returns>
        private static Exception RequestFailure(HttpClient client)
        {
            try
            {
                using (var response = client
                    .GetAsync(new Uri("https://example.com/"))
                    .GetAwaiter()
                    .GetResult())
                {
                    return null;
                }
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        /// <summary>
        /// Read the client the element made for itself.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <returns>The client.</returns>
        private static HttpClient OwnedClient(AgentSignatureElement element)
        {
            var field = typeof(AgentSignatureElement).GetField(
                "_httpClient",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(
                field,
                "Expected the element to hold its client in a field named " +
                "'_httpClient'.");
            return field.GetValue(element) as HttpClient;
        }

        /// <summary>
        /// Read the settings an element was built with.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <returns>The settings.</returns>
        private static AgentSignatureConfiguration Settings(
            AgentSignatureElement element)
        {
            var field = typeof(AgentSignatureElement).GetField(
                "_configuration",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(
                field,
                "Expected the element to hold its settings in a field " +
                "named '_configuration'.");
            return field.GetValue(element) as AgentSignatureConfiguration;
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
        /// A builder that hands back the settings it is building up, which
        /// are otherwise only visible to the builder and the element.
        /// </summary>
        private sealed class TestBuilder : AgentSignatureElementBuilder
        {
            /// <summary>
            /// Construct a builder that logs nowhere.
            /// </summary>
            public TestBuilder()
                : base(NullLoggerFactory.Instance)
            {
            }

            /// <summary>
            /// The settings built up so far.
            /// </summary>
            public AgentSignatureConfiguration Settings => Configuration;
        }

        /// <summary>
        /// A handler that answers every request with an empty document and
        /// counts how many times it has been disposed.
        /// </summary>
        private sealed class RecordingHandler : HttpMessageHandler
        {
            /// <summary>
            /// The number of times this handler has been disposed.
            /// </summary>
            public int DisposeCount => _disposeCount;

            private int _disposeCount;

            /// <inheritdoc/>
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{}"),
                        RequestMessage = request,
                    });
            }

            /// <inheritdoc/>
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    Interlocked.Increment(ref _disposeCount);
                }
                base.Dispose(disposing);
            }
        }
    }
}
