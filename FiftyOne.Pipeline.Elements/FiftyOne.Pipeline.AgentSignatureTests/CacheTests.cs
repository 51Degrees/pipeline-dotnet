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
using FiftyOne.Pipeline.AgentSignature.Parsing;
using FiftyOne.Pipeline.AgentSignature.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;

namespace FiftyOne.Pipeline.AgentSignature.Tests
{
    /// <summary>
    /// The behaviour of the key directory cache, being the wait budget, the
    /// single start, the negative caching, the refresh that keeps serving
    /// the copy already held, the bound on the number of directories held,
    /// and what an abandoned request does. These are the parts of the
    /// element that decide how a slow or broken agent affects the requests
    /// that arrive whilst its keys are being fetched.
    /// </summary>
    [TestClass]
    public class CacheTests
    {
        /// <summary>
        /// The three origins the least recently used test works with.
        /// </summary>
        private const string OriginAlpha = "https://alpha.example";
        private const string OriginBravo = "https://bravo.example";
        private const string OriginCharlie = "https://charlie.example";

        /// <summary>
        /// A request that has to wait for a fetch reports Timeout with the
        /// DirectoryPending reason, comes back inside the wait budget rather
        /// than waiting for the agent, and leaves the fetch running so that
        /// the next request from the same agent is answered from it. One
        /// request is made of the agent, not two.
        /// </summary>
        [TestMethod]
        public void HeldFetchReadsTimeoutInsideBudgetAndIsFinishedLater()
        {
            Warmup();
            var budget = TimeSpan.FromMilliseconds(200);
            using (var handler = new FakeHttpHandler())
            {
                handler.AddDirectory(
                    Fixtures.SignatureAgentDirectoryUrl,
                    RequestSigner.PublicPart(Fixtures.Ed25519Key()));
                using (var harness = ElementHarness.Create(
                    b => b.SetWaitBudget(budget), handler))
                {
                    var signed = RequestSigner.Sign(new SigningOptions());
                    handler.Hold();

                    var watch = Stopwatch.StartNew();
                    var first = harness.ProcessSigned(signed);
                    watch.Stop();

                    Assert.AreEqual(
                        Constants.STATUS_TIMEOUT,
                        first.AgentSignature.Value,
                        "A request that arrives whilst the key directory " +
                        "is still being fetched was expected to read '" +
                        Constants.STATUS_TIMEOUT + "', but " +
                        Describe(first) + ".");
                    Assert.AreEqual(
                        Constants.REASON_DIRECTORY_PENDING,
                        first.AgentSignatureReason.Value,
                        "The reason was expected to be '" +
                        Constants.REASON_DIRECTORY_PENDING + "', but " +
                        Describe(first) + ".");
                    Assert.IsTrue(
                        watch.ElapsedMilliseconds <
                            budget.TotalMilliseconds + 100,
                        "The request was expected to come back inside the " +
                        "wait budget of " +
                        Text(budget.TotalMilliseconds) +
                        "ms plus 100ms of leeway, but it took " +
                        Text(watch.ElapsedMilliseconds) + "ms and " +
                        Describe(first) + ".");

                    handler.Release();
                    var second = ProcessUntilStatus(
                        harness,
                        signed,
                        Constants.STATUS_VERIFIED,
                        "Once the held fetch was let finish, a later " +
                        "request was expected to read '" +
                        Constants.STATUS_VERIFIED +
                        "' from the result it produced.");
                    Assert.AreEqual(
                        Constants.REASON_VERIFIED,
                        second.AgentSignatureReason.Value,
                        "The reason was expected to be '" +
                        Constants.REASON_VERIFIED + "', but " +
                        Describe(second) + ".");
                    Assert.AreEqual(
                        1,
                        handler.CallCount,
                        "The agent was expected to be asked for its key " +
                        "directory exactly once, because the fetch the " +
                        "first request gave up waiting for was left " +
                        "running rather than abandoned, but it was asked " +
                        Text(handler.CallCount) + " times.");
                }
            }
        }

        /// <summary>
        /// Twenty requests that arrive together for one agent start one
        /// fetch between them, so that a burst of traffic from a single
        /// agent does not turn into a burst of requests to that agent.
        /// </summary>
        [TestMethod]
        public void ManyRequestsAtOnceForOneOriginCauseOneFetch()
        {
            const int requests = 20;
            using (var handler = new FakeHttpHandler())
            {
                handler.AddDirectory(
                    Fixtures.SignatureAgentDirectoryUrl,
                    RequestSigner.PublicPart(Fixtures.Ed25519Key()));
                using (var harness = ElementHarness.Create(
                    b => b.SetWaitBudget(TimeSpan.FromMilliseconds(200)),
                    handler))
                {
                    var signed = RequestSigner.Sign(new SigningOptions());
                    var failures = new ConcurrentBag<string>();
                    var threads = new List<Thread>(requests);

                    // The handler is held so that every one of the twenty
                    // requests is inside the cache at the same time, which
                    // is the moment a second fetch could be started.
                    handler.Hold();
                    for (var i = 0; i < requests; i++)
                    {
                        var thread = new Thread(() =>
                        {
                            try
                            {
                                harness.ProcessSigned(signed);
                            }
#pragma warning disable CA1031 // A test thread must not throw.
                            catch (Exception exception)
#pragma warning restore CA1031
                            {
                                failures.Add(exception.ToString());
                            }
                        });
                        thread.IsBackground = true;
                        threads.Add(thread);
                        thread.Start();
                    }
                    foreach (var thread in threads)
                    {
                        thread.Join();
                    }
                    handler.Release();

                    Assert.AreEqual(
                        0,
                        failures.Count,
                        "None of the twenty requests was expected to " +
                        "throw, but one did with " +
                        string.Join(" ", failures) + ".");
                    Assert.AreEqual(
                        1,
                        harness.FetchCount,
                        "Twenty requests for one agent were expected to " +
                        "start exactly one key directory fetch, but " +
                        Text(harness.FetchCount) + " were started.");
                    Assert.AreEqual(
                        1,
                        handler.CallCount,
                        "The agent was expected to be asked for its key " +
                        "directory exactly once, but it was asked " +
                        Text(handler.CallCount) + " times.");
                }
            }
        }

        /// <summary>
        /// A fetch that fails reads Unverified with the
        /// DirectoryUnavailable reason, is remembered for the negative
        /// cache lifetime so that an outage at one agent does not turn into
        /// a fetch on every request, and is tried again once that lifetime
        /// has passed.
        /// </summary>
        [TestMethod]
        public void FailedFetchIsRememberedForTheNegativeCacheLifetime()
        {
            var negativeLifetime = TimeSpan.FromMinutes(5);
            using (var handler = new FakeHttpHandler())
            {
                handler.AddStatus(
                    Fixtures.SignatureAgentDirectoryUrl,
                    HttpStatusCode.InternalServerError);
                using (var harness = ElementHarness.Create(
                    b => b.SetNegativeCacheLifetime(negativeLifetime),
                    handler))
                {
                    var signed = RequestSigner.Sign(new SigningOptions());

                    var first = harness.ProcessSigned(signed);
                    Assert.AreEqual(
                        Constants.STATUS_UNVERIFIED,
                        first.AgentSignature.Value,
                        "An agent answering 500 was expected to read '" +
                        Constants.STATUS_UNVERIFIED + "', but " +
                        Describe(first) + ".");
                    Assert.AreEqual(
                        Constants.REASON_DIRECTORY_UNAVAILABLE,
                        first.AgentSignatureReason.Value,
                        "The reason was expected to be '" +
                        Constants.REASON_DIRECTORY_UNAVAILABLE + "', but " +
                        Describe(first) + ".");
                    Assert.AreEqual(
                        1,
                        handler.CallCount,
                        "The first request was expected to make one fetch, " +
                        "but the handler was asked " +
                        Text(handler.CallCount) + " times and " +
                        Describe(first) + ".");

                    var second = harness.ProcessSigned(signed);
                    Assert.AreEqual(
                        1,
                        handler.CallCount,
                        "A second request inside the negative cache " +
                        "lifetime was expected to make no new fetch, so " +
                        "the handler should still have been asked once, " +
                        "but it was asked " + Text(handler.CallCount) +
                        " times and " + Describe(second) + ".");

                    // Move the element's clock past the point at which the
                    // failure stops being remembered.
                    harness.Now = harness.Now + negativeLifetime +
                        TimeSpan.FromMinutes(1);

                    var third = harness.ProcessSigned(signed);
                    Assert.AreEqual(
                        2,
                        handler.CallCount,
                        "Once the negative cache lifetime had passed the " +
                        "next request was expected to try the agent " +
                        "again, so the handler should have been asked " +
                        "twice, but it was asked " +
                        Text(handler.CallCount) + " times and " +
                        Describe(third) + ".");
                    Assert.AreEqual(
                        2,
                        harness.FetchCount,
                        "Two key directory fetches were expected, one for " +
                        "each side of the negative cache lifetime, but " +
                        Text(harness.FetchCount) + " were started and " +
                        Describe(third) + ".");
                }
            }
        }

        /// <summary>
        /// Once the cache lifetime has passed, the next request starts a
        /// fresh fetch and is answered straight away from the copy already
        /// held rather than waiting for the fresh one. The handler is held
        /// open so that an answer which waited for the refresh could not
        /// come back inside the time asserted.
        /// </summary>
        [TestMethod]
        public void StaleDirectoryIsRefreshedAndAnsweredFromTheCopyHeld()
        {
            var lifetime = TimeSpan.FromMinutes(10);
            using (var handler = new FakeHttpHandler())
            {
                handler.AddDirectory(
                    Fixtures.SignatureAgentDirectoryUrl,
                    RequestSigner.PublicPart(Fixtures.Ed25519Key()));
                using (var harness = ElementHarness.Create(
                    b => b.SetCacheLifetime(lifetime)
                        .SetWaitBudget(TimeSpan.FromSeconds(5)),
                    handler))
                {
                    var signed = RequestSigner.Sign(new SigningOptions());
                    var first = harness.ProcessSigned(signed);
                    Assert.AreEqual(
                        Constants.STATUS_VERIFIED,
                        first.AgentSignature.Value,
                        "The first request was expected to read '" +
                        Constants.STATUS_VERIFIED + "', but " +
                        Describe(first) + ".");

                    handler.Hold();
                    harness.Now = harness.Now + lifetime +
                        TimeSpan.FromMinutes(1);

                    var watch = Stopwatch.StartNew();
                    var second = harness.ProcessSigned(signed);
                    watch.Stop();

                    Assert.AreEqual(
                        Constants.STATUS_VERIFIED,
                        second.AgentSignature.Value,
                        "A request made once the cache lifetime had " +
                        "passed was expected to be answered from the copy " +
                        "already held and so to read '" +
                        Constants.STATUS_VERIFIED + "', but " +
                        Describe(second) + ".");
                    Assert.AreEqual(
                        2,
                        harness.FetchCount,
                        "That request was expected to start a fresh " +
                        "fetch, making two in all, but " +
                        Text(harness.FetchCount) + " were started and " +
                        Describe(second) + ".");
                    Assert.IsTrue(
                        watch.ElapsedMilliseconds < 2000,
                        "That request was expected to be answered " +
                        "without waiting for the fresh fetch, which the " +
                        "handler was holding open, but it took " +
                        Text(watch.ElapsedMilliseconds) + "ms and " +
                        Describe(second) + ".");

                    handler.Release();
                }
            }
        }

        /// <summary>
        /// A refresh that fails does not throw away the keys already held,
        /// so later requests keep reading Verified. The protocol draft is
        /// explicit that a directory which fails to resolve must not lose a
        /// key that is already known.
        /// </summary>
        [TestMethod]
        public void RefreshThatFailsKeepsTheKeysAlreadyHeld()
        {
            var lifetime = TimeSpan.FromMinutes(10);
            using (var handler = new FakeHttpHandler())
            {
                handler.AddDirectory(
                    Fixtures.SignatureAgentDirectoryUrl,
                    RequestSigner.PublicPart(Fixtures.Ed25519Key()));
                using (var harness = ElementHarness.Create(
                    b => b.SetCacheLifetime(lifetime), handler))
                {
                    var signed = RequestSigner.Sign(new SigningOptions());
                    var first = harness.ProcessSigned(signed);
                    Assert.AreEqual(
                        Constants.STATUS_VERIFIED,
                        first.AgentSignature.Value,
                        "The first request was expected to read '" +
                        Constants.STATUS_VERIFIED + "', but " +
                        Describe(first) + ".");

                    // The agent goes away, so the refresh cannot succeed.
                    handler.Remove(Fixtures.SignatureAgentDirectoryUrl);
                    harness.Now = harness.Now + lifetime +
                        TimeSpan.FromMinutes(1);

                    var second = harness.ProcessSigned(signed);
                    Assert.AreEqual(
                        Constants.STATUS_VERIFIED,
                        second.AgentSignature.Value,
                        "The request that started the refresh was " +
                        "expected to read '" + Constants.STATUS_VERIFIED +
                        "' from the copy already held, but " +
                        Describe(second) + ".");
                    WaitForCalls(
                        handler,
                        2,
                        "The refresh was expected to reach the agent and " +
                        "fail.");

                    for (var i = 0; i < 3; i++)
                    {
                        var later = harness.ProcessSigned(signed);
                        Assert.AreEqual(
                            Constants.STATUS_VERIFIED,
                            later.AgentSignature.Value,
                            "A request made after the refresh had failed " +
                            "was expected to keep reading '" +
                            Constants.STATUS_VERIFIED + "' from the keys " +
                            "already held, but " + Describe(later) + ".");
                        Assert.AreEqual(
                            Constants.REASON_VERIFIED,
                            later.AgentSignatureReason.Value,
                            "The reason was expected to be '" +
                            Constants.REASON_VERIFIED + "', but " +
                            Describe(later) + ".");
                    }
                }
            }
        }

        /// <summary>
        /// A refresh that succeeds and no longer holds the key the
        /// signature names reads Invalid with the UnknownKey reason,
        /// because a key the agent has withdrawn is evidence against the
        /// signature rather than something to keep trusting.
        /// </summary>
        [TestMethod]
        public void RefreshThatDropsTheKeyReadsUnknownKey()
        {
            var lifetime = TimeSpan.FromMinutes(10);
            using (var handler = new FakeHttpHandler())
            {
                handler.AddDirectory(
                    Fixtures.SignatureAgentDirectoryUrl,
                    RequestSigner.PublicPart(Fixtures.Ed25519Key()));
                using (var harness = ElementHarness.Create(
                    b => b.SetCacheLifetime(lifetime), handler))
                {
                    var signed = RequestSigner.Sign(new SigningOptions());
                    var first = harness.ProcessSigned(signed);
                    Assert.AreEqual(
                        Constants.STATUS_VERIFIED,
                        first.AgentSignature.Value,
                        "The first request was expected to read '" +
                        Constants.STATUS_VERIFIED + "', but " +
                        Describe(first) + ".");

                    // The agent withdraws the key it signed with and
                    // publishes a different one in its place.
                    handler.AddDirectory(
                        Fixtures.SignatureAgentDirectoryUrl,
                        RequestSigner.PublicPart(Fixtures.RsaKey()));
                    harness.Now = harness.Now + lifetime +
                        TimeSpan.FromMinutes(1);

                    var stale = harness.ProcessSigned(signed);
                    Assert.AreEqual(
                        Constants.STATUS_VERIFIED,
                        stale.AgentSignature.Value,
                        "The request that started the refresh was " +
                        "expected to read '" + Constants.STATUS_VERIFIED +
                        "' from the copy already held, but " +
                        Describe(stale) + ".");

                    var later = ProcessUntilStatus(
                        harness,
                        signed,
                        Constants.STATUS_INVALID,
                        "Once the refresh had brought back a directory " +
                        "without the key, a request was expected to read '" +
                        Constants.STATUS_INVALID + "'.");
                    Assert.AreEqual(
                        Constants.REASON_UNKNOWN_KEY,
                        later.AgentSignatureReason.Value,
                        "The reason was expected to be '" +
                        Constants.REASON_UNKNOWN_KEY + "', but " +
                        Describe(later) + ".");
                }
            }
        }

        /// <summary>
        /// The cache is bounded, so an agent cannot fill memory by naming a
        /// different origin on every request. With room for two
        /// directories, the origin used longest ago is dropped and has to
        /// be fetched again, whilst the origin used most recently is still
        /// answered without a fetch.
        /// </summary>
        /// <remarks>
        /// The cache is driven directly, and built with one list, because
        /// the least recently used cache underneath it splits its room
        /// across as many lists as the concurrency it is given and then
        /// drops the entry used longest ago within one list rather than
        /// across the whole cache. The element asks for one list per
        /// processor, so a cache with room for two directories is not
        /// bounded to two at all on a machine with more processors than
        /// that, and the origin used longest ago is not the one dropped.
        /// That is a finding about the element rather than something for
        /// this test to work around, so it is reported alongside these
        /// tests and the contract itself is checked here with one list.
        /// </remarks>
        [TestMethod]
        public void LeastRecentlyUsedOriginIsFetchedAgain()
        {
            var now = DateTimeOffset.FromUnixTimeSeconds(1735689700);
            using (var handler = new FakeHttpHandler())
            using (var httpClient = new HttpClient(handler, false))
            {
                var publicKey = RequestSigner.PublicPart(
                    Fixtures.Ed25519Key());
                foreach (var origin in new[]
                    { OriginAlpha, OriginBravo, OriginCharlie })
                {
                    handler.AddDirectory(
                        origin + Constants.DIRECTORY_PATH, publicKey);
                }
                var fetcher = new DirectoryFetcher(
                    httpClient,
                    NullLogger.Instance,
                    () => now,
                    Constants.DEFAULT_MAX_RESPONSE_BYTES);
                using (var cache = new DirectoryCache(
                    fetcher,
                    () => now,
                    2,
                    TimeSpan.FromMinutes(10),
                    TimeSpan.FromMinutes(5),
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(10),
                    1))
                {
                    var alpha = AgentFor(OriginAlpha);
                    var bravo = AgentFor(OriginBravo);
                    var charlie = AgentFor(OriginCharlie);

                    Resolve(cache, alpha, OriginAlpha);
                    Resolve(cache, bravo, OriginBravo);
                    Resolve(cache, charlie, OriginCharlie);
                    Assert.AreEqual(
                        3,
                        cache.FetchCount,
                        "Three origins were expected to cause three " +
                        "fetches, but " + Text(cache.FetchCount) +
                        " were started.");

                    // Alpha was used longest ago, so with room for two it
                    // is the one the cache has dropped.
                    Resolve(cache, alpha, OriginAlpha);
                    Assert.AreEqual(
                        4,
                        cache.FetchCount,
                        "The origin used longest ago was expected to have " +
                        "been dropped from a cache with room for two, and " +
                        "so to need a fourth fetch, but " +
                        Text(cache.FetchCount) + " have been started.");

                    // Charlie is still held, which shows the fourth fetch
                    // came from the bound on the cache rather than from
                    // nothing being kept at all.
                    Resolve(cache, charlie, OriginCharlie);
                    Assert.AreEqual(
                        4,
                        cache.FetchCount,
                        "The origin used most recently was expected to " +
                        "still be held, so no fifth fetch should have " +
                        "been started, but " + Text(cache.FetchCount) +
                        " have been started.");
                }
            }
        }

        /// <summary>
        /// A request that has already been abandoned starts no fetch at
        /// all, because work nobody is waiting for is work an agent can use
        /// to make this element make requests for it. The cache is driven
        /// directly here because a flow data whose stop token is already
        /// cancelled never reaches the element. Both the pipeline
        /// (Pipeline.cs, the 'data.Stop' check before each element) and
        /// FlowElementBase skip every element in that case, so there is no
        /// element to report anything. The cache answers Pending, which is
        /// what the element turns into the Timeout status with the
        /// DirectoryPending reason.
        /// </summary>
        [TestMethod]
        public void RequestAlreadyAbandonedStartsNoFetch()
        {
            var now = DateTimeOffset.FromUnixTimeSeconds(1735689700);
            using (var handler = new FakeHttpHandler())
            using (var httpClient = new HttpClient(handler, false))
            {
                handler.AddDirectory(
                    Fixtures.SignatureAgentDirectoryUrl,
                    RequestSigner.PublicPart(Fixtures.Ed25519Key()));
                var fetcher = new DirectoryFetcher(
                    httpClient,
                    NullLogger.Instance,
                    () => now,
                    Constants.DEFAULT_MAX_RESPONSE_BYTES);
                using (var cache = new DirectoryCache(
                    fetcher,
                    () => now,
                    10,
                    TimeSpan.FromMinutes(10),
                    TimeSpan.FromMinutes(5),
                    TimeSpan.FromMilliseconds(200),
                    TimeSpan.FromSeconds(5),
                    1))
                {
                    Assert.IsTrue(
                        SignatureAgentEntry.TryParse(
                            "agent1=\"" + Fixtures.SignatureAgentOrigin +
                                "\"",
                            true,
                            out var agents),
                        "The test's own 'Signature-Agent' header was " +
                        "expected to parse.");

                    using (var source = new CancellationTokenSource())
                    {
                        source.Cancel();
                        var outcome = cache.Lookup(
                            agents[0], source.Token, out var entry);

                        Assert.AreEqual(
                            DirectoryLookupOutcome.Pending,
                            outcome,
                            "A lookup for a request that had already been " +
                            "abandoned was expected to answer '" +
                            DirectoryLookupOutcome.Pending +
                            "', which the element reads as the '" +
                            Constants.STATUS_TIMEOUT + "' status with " +
                            "the '" + Constants.REASON_DIRECTORY_PENDING +
                            "' reason, but it answered '" + outcome + "'.");
                        Assert.IsNull(
                            entry,
                            "No entry was expected, because nothing was " +
                            "fetched.");
                        Assert.AreEqual(
                            0,
                            cache.FetchCount,
                            "No fetch was expected to be started for an " +
                            "abandoned request, but " +
                            Text(cache.FetchCount) + " were started.");
                        Assert.AreEqual(
                            0,
                            handler.CallCount,
                            "The agent was expected not to be asked for " +
                            "anything, but it was asked " +
                            Text(handler.CallCount) + " times.");
                    }
                }
            }
        }

        /// <summary>
        /// A request abandoned whilst it is waiting for a fetch comes back
        /// straight away with Timeout rather than sitting out the rest of
        /// the wait budget, and the fetch it started is still there for the
        /// next request to use.
        /// </summary>
        [TestMethod]
        public void RequestAbandonedWhilstWaitingComesBackPromptly()
        {
            Warmup();
            var budget = TimeSpan.FromSeconds(10);
            using (var handler = new FakeHttpHandler())
            {
                handler.AddDirectory(
                    Fixtures.SignatureAgentDirectoryUrl,
                    RequestSigner.PublicPart(Fixtures.Ed25519Key()));
                using (var harness = ElementHarness.Create(
                    b => b.SetWaitBudget(budget), handler))
                using (var source = new CancellationTokenSource())
                {
                    var signed = RequestSigner.Sign(new SigningOptions());
                    handler.Hold();

                    // The request is abandoned only once the fetch has
                    // actually reached the agent, which is proof that the
                    // element was already running rather than skipped.
                    var canceller = new Thread(() =>
                    {
                        WaitFor(() => handler.CallCount > 0);
                        source.Cancel();
                    });
                    canceller.IsBackground = true;
                    canceller.Start();

                    var watch = Stopwatch.StartNew();
                    var result = harness.ProcessSigned(
                        signed, stopToken: source.Token);
                    watch.Stop();
                    canceller.Join();

                    Assert.AreEqual(
                        Constants.STATUS_TIMEOUT,
                        result.AgentSignature.Value,
                        "A request abandoned whilst it waited was " +
                        "expected to read '" + Constants.STATUS_TIMEOUT +
                        "', but " + Describe(result) + ".");
                    Assert.AreEqual(
                        Constants.REASON_DIRECTORY_PENDING,
                        result.AgentSignatureReason.Value,
                        "The reason was expected to be '" +
                        Constants.REASON_DIRECTORY_PENDING + "', but " +
                        Describe(result) + ".");
                    Assert.IsTrue(
                        watch.ElapsedMilliseconds < 3000,
                        "The request was expected to come back well " +
                        "inside the wait budget of " +
                        Text(budget.TotalMilliseconds) + "ms once it was " +
                        "abandoned, but it took " +
                        Text(watch.ElapsedMilliseconds) + "ms and " +
                        Describe(result) + ".");

                    handler.Release();
                    var later = ProcessUntilStatus(
                        harness,
                        RequestSigner.Sign(new SigningOptions()),
                        Constants.STATUS_VERIFIED,
                        "The fetch the abandoned request started was " +
                        "expected to be left running, so a later request " +
                        "should read '" + Constants.STATUS_VERIFIED + "'.");
                    Assert.AreEqual(
                        1,
                        handler.CallCount,
                        "The agent was expected to be asked once in all, " +
                        "but it was asked " + Text(handler.CallCount) +
                        " times and " + Describe(later) + ".");
                }
            }
        }

        /// <summary>
        /// A key directory served as plain JSON is not accepted, because
        /// the directory draft gives it a media type of its own and
        /// anything else may be a document that was never meant to be read
        /// as a set of keys.
        /// </summary>
        [TestMethod]
        public void DirectoryServedAsPlainJsonIsNotAccepted()
        {
            using (var handler = new FakeHttpHandler())
            {
                handler.Add(
                    Fixtures.SignatureAgentDirectoryUrl,
                    "{\"keys\":[" +
                        RequestSigner.PublicPart(Fixtures.Ed25519Key()) +
                        "]}",
                    Constants.JSON_MEDIA_TYPE);
                using (var harness = ElementHarness.Create(null, handler))
                {
                    var signed = RequestSigner.Sign(new SigningOptions());
                    var result = harness.ProcessSigned(signed);

                    Assert.AreEqual(
                        Constants.STATUS_UNVERIFIED,
                        result.AgentSignature.Value,
                        "A directory served as '" +
                        Constants.JSON_MEDIA_TYPE +
                        "' rather than '" + Constants.DIRECTORY_MEDIA_TYPE +
                        "' was expected to read '" +
                        Constants.STATUS_UNVERIFIED + "', but " +
                        Describe(result) + ".");
                    Assert.AreEqual(
                        Constants.REASON_DIRECTORY_UNAVAILABLE,
                        result.AgentSignatureReason.Value,
                        "The reason was expected to be '" +
                        Constants.REASON_DIRECTORY_UNAVAILABLE + "', but " +
                        Describe(result) + ".");
                }
            }
        }

        /// <summary>
        /// A JWKS the agent names directly with ';type=jwks_uri' is an
        /// ordinary JSON document, so plain JSON is accepted for it and the
        /// signature reads Verified.
        /// </summary>
        [TestMethod]
        public void JwksUriServedAsPlainJsonIsAccepted()
        {
            const string jwksUrl = "https://keys.example/jwks.json";
            using (var handler = new FakeHttpHandler())
            {
                handler.Add(
                    jwksUrl,
                    "{\"keys\":[" +
                        RequestSigner.PublicPart(Fixtures.Ed25519Key()) +
                        "]}",
                    Constants.JSON_MEDIA_TYPE);
                using (var harness = ElementHarness.Create(null, handler))
                {
                    var signed = SignNamingJwksUri(jwksUrl);
                    var result = harness.ProcessSigned(signed);

                    Assert.AreEqual(
                        Constants.STATUS_VERIFIED,
                        result.AgentSignature.Value,
                        "A JWKS named with ';type=" +
                        Constants.AGENT_TYPE_JWKS_URI +
                        "' and served as '" + Constants.JSON_MEDIA_TYPE +
                        "' was expected to read '" +
                        Constants.STATUS_VERIFIED + "', but " +
                        Describe(result) + ".");
                    Assert.AreEqual(
                        Constants.REASON_VERIFIED,
                        result.AgentSignatureReason.Value,
                        "The reason was expected to be '" +
                        Constants.REASON_VERIFIED + "', but " +
                        Describe(result) + ".");
                    Assert.AreEqual(
                        jwksUrl,
                        handler.RequestedUrls[0],
                        "The keys were expected to be fetched from the " +
                        "URL the agent named, without the key directory " +
                        "path added to it.");
                }
            }
        }

        /// <summary>
        /// Sign a request that names its key source with the 'jwks_uri'
        /// type. The shared signing helper adds no type parameter, and the
        /// covered component has to carry the member exactly as the header
        /// writes it, so the header and the text that is signed are built
        /// here.
        /// </summary>
        /// <param name="jwksUrl">The URL the agent publishes keys at.</param>
        /// <returns>The headers to put into evidence.</returns>
        private static SignedRequest SignNamingJwksUri(string jwksUrl)
        {
            const string label = "agent1";
            var member = "\"" + jwksUrl + "\";type=" +
                Constants.AGENT_TYPE_JWKS_URI;
            var components = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(
                    "\"@authority\"", "example.com"),
                new KeyValuePair<string, string>(
                    "\"signature-agent\";key=\"" + label + "\"", member),
            };
            var identifiers = new StringBuilder("(");
            for (var i = 0; i < components.Count; i++)
            {
                if (i > 0)
                {
                    identifiers.Append(" ");
                }
                identifiers.Append(components[i].Key);
            }
            identifiers.Append(")");
            var signatureParams = identifiers.ToString() +
                ";created=1735689600;expires=4889289600" +
                ";keyid=\"" + Fixtures.Ed25519Thumbprint + "\"" +
                ";alg=\"" + Constants.ALGORITHM_ED25519 + "\"" +
                ";tag=\"" + Constants.TAG_WEB_BOT_AUTH + "\"";
            var signatureBase = RequestSigner.BuildBase(
                components, signatureParams);
            var signature = RequestSigner.SignBytes(
                Fixtures.Ed25519Key(),
                Encoding.ASCII.GetBytes(signatureBase));
            return new SignedRequest
            {
                Signature = "sig1=:" +
                    Convert.ToBase64String(signature) + ":",
                SignatureInput = "sig1=" + signatureParams,
                SignatureAgent = label + "=" + member,
                SignatureBase = signatureBase,
            };
        }

        /// <summary>
        /// Build the parsed 'Signature-Agent' member for an origin that
        /// publishes a key directory.
        /// </summary>
        /// <param name="origin">The origin.</param>
        /// <returns>The member.</returns>
        private static SignatureAgentEntry AgentFor(string origin)
        {
            Assert.IsTrue(
                SignatureAgentEntry.TryParse(
                    "agent1=\"" + origin + "\"", true, out var agents),
                "The test's own 'Signature-Agent' header naming '" +
                origin + "' was expected to parse.");
            return agents[0];
        }

        /// <summary>
        /// Look one origin up in the cache and check that its keys came
        /// back, so that the least recently used test says what it is
        /// counting rather than repeating the same lines.
        /// </summary>
        /// <param name="cache">The cache.</param>
        /// <param name="agent">The 'Signature-Agent' member.</param>
        /// <param name="origin">
        /// The origin the member names, for the failure message.
        /// </param>
        private static void Resolve(
            DirectoryCache cache,
            SignatureAgentEntry agent,
            string origin)
        {
            var outcome = cache.Lookup(
                agent, CancellationToken.None, out var entry);
            Assert.AreEqual(
                DirectoryLookupOutcome.Resolved,
                outcome,
                "The keys for '" + origin + "' were expected to be " +
                "available, but the lookup answered '" + outcome + "'.");
            Assert.IsTrue(
                entry.Success,
                "The keys for '" + origin + "' were expected to be read, " +
                "but the fetch failed because " + entry.FailureReason + ".");
        }

        /// <summary>
        /// Run one whole request through an element of its own, so that the
        /// times measured in the tests that watch the clock are the wait
        /// budget rather than the cost of running each piece of code for
        /// the first time.
        /// </summary>
        private static void Warmup()
        {
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                harness.ProcessSigned(RequestSigner.Sign(new SigningOptions()));
            }
        }

        /// <summary>
        /// Run the request over and over until it reads the status given,
        /// which is how a test waits for a fetch that runs in the
        /// background without guessing how long it takes.
        /// </summary>
        /// <param name="harness">The harness.</param>
        /// <param name="signed">The signed request.</param>
        /// <param name="status">The status to wait for.</param>
        /// <param name="expected">
        /// What was expected, for the failure message.
        /// </param>
        /// <returns>The reading that had the status.</returns>
        private static IAgentSignatureData ProcessUntilStatus(
            ElementHarness harness,
            SignedRequest signed,
            string status,
            string expected)
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            IAgentSignatureData result = null;
            while (DateTime.UtcNow < deadline)
            {
                result = harness.ProcessSigned(signed);
                if (string.Equals(
                    result.AgentSignature.Value,
                    status,
                    StringComparison.Ordinal))
                {
                    return result;
                }
                Thread.Sleep(10);
            }
            Assert.Fail(
                expected + " That never happened inside 20 seconds, and " +
                "the last reading was that " + Describe(result) + ".");
            return null;
        }

        /// <summary>
        /// Wait until the handler has been asked for at least the number of
        /// requests given, which is how a test waits for a refresh that
        /// runs in the background.
        /// </summary>
        /// <param name="handler">The handler.</param>
        /// <param name="calls">The number of requests to wait for.</param>
        /// <param name="expected">
        /// What was expected, for the failure message.
        /// </param>
        private static void WaitForCalls(
            FakeHttpHandler handler,
            int calls,
            string expected)
        {
            if (WaitFor(() => handler.CallCount >= calls) == false)
            {
                Assert.Fail(
                    expected + " That never happened inside 20 seconds, " +
                    "and the agent had been asked " +
                    Text(handler.CallCount) + " times rather than " +
                    Text(calls) + ".");
            }
        }

        /// <summary>
        /// Wait for a condition to become true, so that the tests never
        /// sleep for a fixed period and hope.
        /// </summary>
        /// <param name="condition">The condition.</param>
        /// <returns>True when the condition became true in time.</returns>
        private static bool WaitFor(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < deadline)
            {
                if (condition())
                {
                    return true;
                }
                Thread.Sleep(5);
            }
            return false;
        }

        /// <summary>
        /// Describe a reading for a failure message.
        /// </summary>
        /// <param name="result">The reading, which may be null.</param>
        /// <returns>The description.</returns>
        private static string Describe(IAgentSignatureData result)
        {
            if (result == null)
            {
                return "there was no reading at all";
            }
            return "the status was '" + result.AgentSignature.Value +
                "' with the reason '" +
                result.AgentSignatureReason.Value + "'";
        }

        /// <summary>
        /// Write a number for a failure message.
        /// </summary>
        /// <param name="value">The number.</param>
        /// <returns>The text.</returns>
        private static string Text(double value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
