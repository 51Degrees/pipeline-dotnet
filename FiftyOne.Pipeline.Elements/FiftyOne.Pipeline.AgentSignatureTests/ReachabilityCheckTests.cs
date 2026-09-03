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

using FiftyOne.Pipeline.AgentSignature.FlowElement;
using FiftyOne.Pipeline.AgentSignature.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;

namespace FiftyOne.Pipeline.AgentSignature.Tests
{
    /// <summary>
    /// A deployment with no outbound access answers every signed request
    /// Unverified, one request at a time, which reads as agents behaving
    /// oddly rather than as a deployment that cannot do the work. Where an
    /// address to check is configured the element fetches it once at start
    /// up and says in the log which it is.
    /// </summary>
    [TestClass]
    public class ReachabilityCheckTests
    {
        /// <summary>
        /// A deployment that cannot reach the keys gets one line saying so
        /// at the level an operator is watching, and the line names the
        /// address and what the request will report.
        /// </summary>
        [TestMethod]
        public void UnreachableDirectoryIsReportedAsAnError()
        {
            var logger = new CapturingLoggerFactory();
            using (var handler = new FakeHttpHandler())
            using (var client = new HttpClient(handler, false))
            {
                // Nothing is registered at the address, so the fetch fails
                // the way it would with no outbound access at all.
                using (new AgentSignatureElementBuilder(logger)
                    .SetHttpClient(client)
                    .SetReachabilityCheckUrl(
                        Fixtures.SignatureAgentDirectoryUrl)
                    .Build())
                {
                    Assert.IsTrue(
                        WaitFor(() => logger.Errors.Count > 0),
                        "Expected an error in the log saying the key " +
                        "directory could not be reached at start up.");
                    Assert.IsTrue(
                        logger.Errors[0].Contains(
                            Fixtures.SignatureAgentDirectoryUrl),
                        "Expected the message to name the address that " +
                        "could not be reached, and it was '" +
                        logger.Errors[0] + "'.");
                }
            }
        }

        /// <summary>
        /// A deployment that can reach the keys says so once, at a level
        /// that does not shout, so the absence of the error means the
        /// check ran and passed rather than that nothing happened.
        /// </summary>
        [TestMethod]
        public void ReachableDirectoryIsReportedAsInformation()
        {
            var logger = new CapturingLoggerFactory();
            using (var handler = new FakeHttpHandler())
            using (var client = new HttpClient(handler, false))
            {
                handler.AddDirectory(
                    Fixtures.SignatureAgentDirectoryUrl,
                    RequestSigner.PublicPart(Fixtures.Ed25519Key()));
                using (new AgentSignatureElementBuilder(logger)
                    .SetHttpClient(client)
                    .SetReachabilityCheckUrl(
                        Fixtures.SignatureAgentDirectoryUrl)
                    .Build())
                {
                    Assert.IsTrue(
                        WaitFor(() => logger.Information.Count > 0),
                        "Expected the log to say the key directory was " +
                        "reached at start up.");
                    Assert.AreEqual(
                        0,
                        logger.Errors.Count,
                        "Expected no error where the directory was " +
                        "reached, and the log held '" +
                        string.Join("', '", logger.Errors) + "'.");
                }
            }
        }

        /// <summary>
        /// Building the element does not wait for the check, and does not
        /// throw when the address cannot be reached at all. An element that
        /// reached the network whilst being built would stop a site
        /// starting when the network was down, which this repository fixed
        /// once already in issues 44 and 312.
        /// </summary>
        [TestMethod]
        public void BuildDoesNotWaitForOrFailOnTheCheck()
        {
            var logger = new CapturingLoggerFactory();
            var watch = Stopwatch.StartNew();
            using (new AgentSignatureElementBuilder(logger)
                // Nothing answers here, and no client is given, so the
                // element makes its own and the fetch runs for real.
                .SetReachabilityCheckUrl(
                    "https://no-such-host.invalid/.well-known/http-message-signatures-directory")
                .SetFetchTimeout(TimeSpan.FromSeconds(5))
                .Build())
            {
                watch.Stop();
                Assert.IsTrue(
                    watch.ElapsedMilliseconds < 1000,
                    "Expected building the element to return without " +
                    "waiting for the check, and it took " +
                    watch.ElapsedMilliseconds + "ms.");
            }
        }

        /// <summary>
        /// No address configured means no check and no request, which is
        /// the default so that an element built by a customer reaches
        /// nothing it was not asked to.
        /// </summary>
        [TestMethod]
        public void NoCheckIsMadeByDefault()
        {
            var logger = new CapturingLoggerFactory();
            using (var handler = new FakeHttpHandler())
            using (var client = new HttpClient(handler, false))
            {
                using (new AgentSignatureElementBuilder(logger)
                    .SetHttpClient(client)
                    .Build())
                {
                    Thread.Sleep(50);
                    Assert.AreEqual(
                        0,
                        handler.CallCount,
                        "Expected no request at all where no address to " +
                        "check was configured.");
                }
            }
        }

        /// <summary>
        /// An element disposed whilst the check is still running says
        /// nothing about reachability, because that is an ordinary
        /// shutdown rather than a deployment that cannot fetch keys.
        /// </summary>
        /// <remarks>
        /// The failure this pins is quiet. The fetcher treats the
        /// cancellation and the disposed client a shutdown produces as
        /// network failures and hands back a failed entry rather than
        /// throwing, so the check completes normally into its failure
        /// branch instead of reaching its catch. Guarding only the catch
        /// therefore left an ordinary shutdown logging, at Error, that
        /// signature checking was switched off.
        /// </remarks>
        [TestMethod]
        public void DisposingWhilstTheCheckRunsRaisesNoAlarm()
        {
            var logger = new CapturingLoggerFactory();
            using (var handler = new FakeHttpHandler())
            {
                // Nothing is added to the handler, so the fetch is still
                // in flight when the element is disposed underneath it.
                var client = new HttpClient(handler, false);
                var element = new AgentSignatureElementBuilder(logger)
                    .SetHttpClient(client)
                    .SetReachabilityCheckUrl(
                        Fixtures.SignatureAgentDirectoryUrl)
                    .Build();
                element.Dispose();
                client.Dispose();

                // Give the check every chance to log before concluding it
                // stayed quiet, so this fails when the guard is removed
                // rather than passing on timing.
                Thread.Sleep(200);

                Assert.AreEqual(
                    0,
                    logger.Errors.Count,
                    "Expected an ordinary shutdown to say nothing about " +
                    "reachability, and it logged: " +
                    string.Join(" | ", logger.Errors));
            }
        }

        private static bool WaitFor(Func<bool> condition)
        {
            for (var i = 0; i < 100; i++)
            {
                if (condition())
                {
                    return true;
                }
                Thread.Sleep(20);
            }
            return false;
        }

        /// <summary>
        /// Keeps the lines written to the log so a test can read them.
        /// </summary>
        private sealed class CapturingLoggerFactory : ILoggerFactory
        {
            public IList<string> Errors { get; } = new List<string>();

            public IList<string> Information { get; } = new List<string>();

            public void AddProvider(ILoggerProvider provider)
            {
            }

            public ILogger CreateLogger(string categoryName)
            {
                return new CapturingLogger(this);
            }

            public void Dispose()
            {
            }

            private sealed class CapturingLogger : ILogger
            {
                private readonly CapturingLoggerFactory _factory;

                public CapturingLogger(CapturingLoggerFactory factory)
                {
                    _factory = factory;
                }

                public IDisposable BeginScope<TState>(TState state)
                {
                    return null;
                }

                public bool IsEnabled(LogLevel logLevel)
                {
                    return true;
                }

                public void Log<TState>(
                    LogLevel logLevel,
                    EventId eventId,
                    TState state,
                    Exception exception,
                    Func<TState, Exception, string> formatter)
                {
                    var line = formatter(state, exception);
                    lock (_factory)
                    {
                        if (logLevel == LogLevel.Error)
                        {
                            _factory.Errors.Add(line);
                        }
                        else if (logLevel == LogLevel.Information)
                        {
                            _factory.Information.Add(line);
                        }
                    }
                }
            }
        }
    }
}
