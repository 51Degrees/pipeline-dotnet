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
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace FiftyOne.Pipeline.AgentSignature.Tests.Standard.Helpers
{
    /// <summary>
    /// Builds a pipeline holding one agent signature element and runs
    /// requests through it, reaching the element through its public
    /// surface alone. The main test project has a richer harness, which
    /// cannot be shared here because it sets the element's clock through
    /// an internal member that only the main test assembly may see. This
    /// harness needs no clock, because the times the signing helper
    /// writes are fixed points that stay valid, and the element checks a
    /// key's own validity at the signature's created time rather than at
    /// the present moment.
    /// </summary>
    public sealed class StandardHarness : IDisposable
    {
        /// <summary>
        /// The handler that answers the element's key directory fetches.
        /// </summary>
        public FakeHttpHandler Handler { get; }

        /// <summary>
        /// The pipeline the element sits in.
        /// </summary>
        public IPipeline Pipeline { get; }

        private readonly HttpClient _httpClient;

        private StandardHarness(
            FakeHttpHandler handler,
            Action<AgentSignatureElementBuilder> configure)
        {
            Handler = handler;
            _httpClient = new HttpClient(handler, false);
            var builder = new AgentSignatureElementBuilder(
                NullLoggerFactory.Instance)
                .SetHttpClient(_httpClient)
                // The fake handler answers at once, but the fetch still
                // crosses threads, so the wait is made long enough that a
                // slow build agent cannot turn a fetch into a Timeout.
                .SetWaitBudget(TimeSpan.FromSeconds(5));
            configure?.Invoke(builder);
            Pipeline = new PipelineBuilder(NullLoggerFactory.Instance)
                .AddFlowElement(builder.Build())
                .Build();
        }

        /// <summary>
        /// Build a harness.
        /// </summary>
        /// <param name="configure">
        /// Anything the test wants to change on the builder.
        /// </param>
        /// <returns>The harness.</returns>
        public static StandardHarness Create(
            Action<AgentSignatureElementBuilder> configure = null)
        {
            return new StandardHarness(new FakeHttpHandler(), configure);
        }

        /// <summary>
        /// Run a signed request.
        /// </summary>
        /// <param name="signed">The signature headers.</param>
        /// <returns>What the element made of it.</returns>
        public IAgentSignatureData ProcessSigned(SignedRequest signed)
        {
            var evidence = new Dictionary<string, string>
            {
                { Constants.EVIDENCE_SIGNATURE_KEY, signed.Signature },
                {
                    Constants.EVIDENCE_SIGNATURE_INPUT_KEY,
                    signed.SignatureInput
                },
                { Constants.EVIDENCE_HOST_KEY, "example.com" },
                { Core.Constants.EVIDENCE_PROTOCOL, "https" },
            };
            if (signed.SignatureAgent != null)
            {
                evidence[Constants.EVIDENCE_SIGNATURE_AGENT_KEY] =
                    signed.SignatureAgent;
            }
            return Process(evidence);
        }

        /// <summary>
        /// Run a request with the evidence given.
        /// </summary>
        /// <param name="evidence">The evidence.</param>
        /// <returns>What the element made of it.</returns>
        public IAgentSignatureData Process(
            IDictionary<string, string> evidence)
        {
            using (var data = Pipeline.CreateFlowData())
            {
                foreach (var entry in evidence)
                {
                    data.AddEvidence(entry.Key, entry.Value);
                }
                data.Process();
                return data.Get<IAgentSignatureData>();
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Pipeline.Dispose();
            _httpClient.Dispose();
            Handler.Dispose();
        }
    }
}
