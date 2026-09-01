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
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;

namespace FiftyOne.Pipeline.AgentSignature.Tests.Helpers
{
    /// <summary>
    /// Builds a pipeline holding one agent signature element and runs
    /// requests through it, so that each test says what it is checking
    /// rather than how to wire a pipeline up.
    /// </summary>
    public sealed class ElementHarness : IDisposable
    {
        /// <summary>
        /// The handler that answers the element's key directory fetches.
        /// </summary>
        public FakeHttpHandler Handler { get; }

        /// <summary>
        /// The element under test.
        /// </summary>
        public AgentSignatureElement Element { get; }

        /// <summary>
        /// The pipeline the element sits in.
        /// </summary>
        public IPipeline Pipeline { get; }

        /// <summary>
        /// The time the element believes it is. A test moves this to check
        /// signatures whose times are fixed, and to age the cache.
        /// </summary>
        public DateTimeOffset Now { get; set; } =
            DateTimeOffset.FromUnixTimeSeconds(1735689700);

        /// <summary>
        /// The number of key directory fetches the element has started.
        /// </summary>
        public int FetchCount => Element.FetchCount;

        private readonly HttpClient _httpClient;
        private readonly bool _ownsHandler;

        private ElementHarness(
            FakeHttpHandler handler,
            bool ownsHandler,
            Action<AgentSignatureElementBuilder> configure)
        {
            Handler = handler;
            _ownsHandler = ownsHandler;
            _httpClient = new HttpClient(handler, false);
            var builder = new AgentSignatureElementBuilder(
                NullLoggerFactory.Instance)
                .SetHttpClient(_httpClient)
                .SetClock(() => Now);
            configure?.Invoke(builder);
            Element = builder.Build();
            Pipeline = new PipelineBuilder(NullLoggerFactory.Instance)
                .AddFlowElement(Element)
                .Build();
        }

        /// <summary>
        /// Build a harness.
        /// </summary>
        /// <param name="configure">
        /// Anything the test wants to change on the builder.
        /// </param>
        /// <param name="handler">
        /// The handler to answer fetches with, or null for a fresh one.
        /// </param>
        /// <returns>The harness.</returns>
        public static ElementHarness Create(
            Action<AgentSignatureElementBuilder> configure = null,
            FakeHttpHandler handler = null)
        {
            return new ElementHarness(
                handler ?? new FakeHttpHandler(),
                handler == null,
                configure);
        }

        /// <summary>
        /// Build a harness whose fake directory serves the public part of
        /// the Ed25519 key the RFC 9421 examples use.
        /// </summary>
        /// <param name="configure">
        /// Anything the test wants to change on the builder.
        /// </param>
        /// <returns>The harness.</returns>
        public static ElementHarness CreateWithTestKey(
            Action<AgentSignatureElementBuilder> configure = null)
        {
            var handler = new FakeHttpHandler();
            handler.AddDirectory(
                Fixtures.SignatureAgentDirectoryUrl,
                RequestSigner.PublicPart(Fixtures.Ed25519Key()));
            var harness = new ElementHarness(handler, true, configure);
            return harness;
        }

        /// <summary>
        /// Run a request that carries no signature at all.
        /// </summary>
        /// <returns>What the element made of it.</returns>
        public IAgentSignatureData ProcessPlainRequest()
        {
            return Process(new Dictionary<string, string>
            {
                { "header.host", "example.com" },
                { "header.protocol", "https" },
            });
        }

        /// <summary>
        /// Run a signed request.
        /// </summary>
        /// <param name="signed">The signature headers.</param>
        /// <param name="host">The host header value.</param>
        /// <param name="scheme">The protocol evidence value.</param>
        /// <param name="stopToken">
        /// A token that says the request has been abandoned.
        /// </param>
        /// <returns>What the element made of it.</returns>
        public IAgentSignatureData ProcessSigned(
            SignedRequest signed,
            string host = "example.com",
            string scheme = "https",
            CancellationToken stopToken = default)
        {
            var evidence = new Dictionary<string, string>
            {
                { "header.signature", signed.Signature },
                { "header.signature-input", signed.SignatureInput },
                { "header.host", host },
                { "header.protocol", scheme },
            };
            if (signed.SignatureAgent != null)
            {
                evidence["header.signature-agent"] = signed.SignatureAgent;
            }
            return Process(evidence, stopToken);
        }

        /// <summary>
        /// Run a request built from one of the standard's test vectors.
        /// </summary>
        /// <param name="vector">The vector.</param>
        /// <param name="stopToken">
        /// A token that says the request has been abandoned.
        /// </param>
        /// <returns>What the element made of it.</returns>
        public IAgentSignatureData ProcessVector(
            SignedRequestVector vector,
            CancellationToken stopToken = default)
        {
            return ProcessSigned(
                new SignedRequest
                {
                    Signature = vector.Signature,
                    SignatureInput = vector.SignatureInput,
                    SignatureAgent = vector.SignatureAgent,
                },
                vector.Host,
                "https",
                stopToken);
        }

        /// <summary>
        /// Run a request with the evidence given.
        /// </summary>
        /// <param name="evidence">The evidence.</param>
        /// <param name="stopToken">
        /// A token that says the request has been abandoned.
        /// </param>
        /// <returns>What the element made of it.</returns>
        public IAgentSignatureData Process(
            IDictionary<string, string> evidence,
            CancellationToken stopToken = default)
        {
            using (var data = Pipeline.CreateFlowData(stopToken))
            {
                foreach (var entry in evidence)
                {
                    data.AddEvidence(entry.Key, entry.Value);
                }
                data.Process();
                return data.Get<IAgentSignatureData>();
            }
        }

        /// <summary>
        /// Run a request and hand back the element data as a dictionary, so
        /// that a test can check what a property with no value says.
        /// </summary>
        /// <param name="evidence">The evidence.</param>
        /// <returns>The element data as a dictionary.</returns>
        public IReadOnlyDictionary<string, object> ProcessAsDictionary(
            IDictionary<string, string> evidence)
        {
            using (var data = Pipeline.CreateFlowData())
            {
                foreach (var entry in evidence)
                {
                    data.AddEvidence(entry.Key, entry.Value);
                }
                data.Process();
                return data.Get<IAgentSignatureData>().AsDictionary();
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Pipeline.Dispose();
            _httpClient.Dispose();
            if (_ownsHandler)
            {
                Handler.Dispose();
            }
        }
    }
}
