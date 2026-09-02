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

using FiftyOne.Pipeline.AgentSignature;
using FiftyOne.Pipeline.AgentSignature.Data;
using FiftyOne.Pipeline.AgentSignature.FlowElement;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using CoreConstants = FiftyOne.Pipeline.Core.Constants;

/// <summary>
/// @example AgentSignature/Program.cs
///
/// Example showing how the agent signature element reads the request
/// signature an automated agent sends under the IETF Web Bot Auth
/// protocol.
///
/// Web Bot Auth is a way for an automated agent, such as a search crawler
/// or an assistant that fetches pages on someone's behalf, to prove which
/// agent it is. The agent signs the request with a private key and sends
/// three headers, being 'Signature', 'Signature-Input' and
/// 'Signature-Agent'. The element reads those three headers, fetches the
/// public key from the address the 'Signature-Agent' header names, checks
/// the signature against it and reports a status and a reason.
///
/// This example is available in full on
/// [GitHub](https://github.com/51Degrees/pipeline-dotnet/blob/master/Examples/AgentSignature/Program.cs).
///
/// A signature says which agent sent a request. It does not say what kind
/// of agent it is. The properties that describe the kind of agent, being
/// IsCrawler, CrawlerName and IsArtificialIntelligence, come from the
/// device detection data file, so a customer who wants them alongside the
/// signature properties adds the FiftyOne.DeviceDetection engine to the
/// same pipeline:
///
/// ```
/// var pipeline = new PipelineBuilder(loggerFactory)
///     .AddFlowElement(new DeviceDetectionHashEngineBuilder(loggerFactory)
///         .Build(dataFilePath, false))
///     .AddFlowElement(new AgentSignatureElementBuilder(loggerFactory)
///         .Build())
///     .Build();
/// ```
///
/// This repository ships no data file, so the example uses the agent
/// signature element on its own and adds no device detection dependency.
/// The engine lives at
/// [device-detection-dotnet](https://github.com/51Degrees/device-detection-dotnet).
///
/// The example shows how to:
///
/// 1. Add the element to a pipeline:
///
/// ```
/// var pipeline = new PipelineBuilder(loggerFactory)
///     .AddFlowElement(new AgentSignatureElementBuilder(loggerFactory)
///         .Build())
///     .Build();
/// ```
///
/// 2. Read the result back from the flow data:
///
/// ```
/// var result = data.Get&lt;IAgentSignatureData&gt;();
/// Console.WriteLine(result.AgentSignature.Value);
/// ```
///
/// 3. See what each of the four outcomes looks like:
///
/// ```
/// ==========================================
/// A request with no signature headers
/// Status: Absent
/// Reason: NoSignature
/// ==========================================
/// A request signed with the test key
/// Status: Verified
/// Reason: Verified
/// ==========================================
/// The same request with one byte of the signature changed
/// Status: Invalid
/// Reason: SignatureMismatch
/// ==========================================
/// The same request naming an agent that publishes no keys
/// Status: Unverified
/// Reason: DirectoryUnavailable
/// ==========================================
/// ```
///
/// 4. Report the agent card properties, being the agent's name, its
/// robots.txt product token and the card address, by configuring a
/// registry of agent cards with `SetRegistry`.
///
/// 5. Check a signature against the live services. The example signs a
/// request with the Ed25519 key printed in RFC 9421 and verifies against
/// the key directory Cloudflare's Web Bot Auth research service publishes,
/// and then shows that the key directory OpenAI publishes for ChatGPT,
/// fetched live, rightly refuses that key. Where a service cannot be
/// reached the example says so and carries on, because a directory that
/// cannot be fetched is never evidence against an agent.
///
/// So that the first five requests are the same every time they run, their
/// keys, card and registry are served by a message handler inside the
/// example rather than by the agent itself. Only the two live checks reach
/// the network.
/// </summary>
namespace Examples.AgentSignature
{
    public class Program
    {
        /// <summary>
        /// The separator the examples in this repository print between
        /// sections.
        /// </summary>
        private const string Separator =
            "==========================================";

        /// <summary>
        /// The Ed25519 key used by the examples in RFC 9421 Appendix B.1.4,
        /// including its private part so that this example can sign a
        /// request with it. The key is published in the standard itself, so
        /// it is public knowledge and is not a secret. Never put a real
        /// private key in source.
        /// </summary>
        private const string TestKey =
            "{" +
            "\"kty\":\"OKP\"," +
            "\"crv\":\"Ed25519\"," +
            "\"kid\":\"test-key-ed25519\"," +
            "\"d\":\"n4Ni-HpISpVObnQMW0wOhCKROaIKqKtW_2ZYb2p9KcU\"," +
            "\"x\":\"JrQLj5P_89iXES9-vFgrIy29clF9CC_oPPsw3c5D0bs\"" +
            "}";

        /// <summary>
        /// The public part of the same key, which is what an agent puts in
        /// its key directory for anyone to read.
        /// </summary>
        private const string TestKeyPublicPart =
            "{" +
            "\"kty\":\"OKP\"," +
            "\"kid\":\"test-key-ed25519\"," +
            "\"crv\":\"Ed25519\"," +
            "\"x\":\"JrQLj5P_89iXES9-vFgrIy29clF9CC_oPPsw3c5D0bs\"" +
            "}";

        /// <summary>
        /// The RFC 7638 thumbprint of the test key, being the short
        /// fingerprint an agent names in the 'keyid' signature parameter.
        /// </summary>
        private const string TestKeyThumbprint =
            "poqkLGiymh_W0uP6PZFw-dvez3QJT5SolqXBCW38r0U";

        /// <summary>
        /// The origin the agent in this example publishes its keys at. The
        /// '.test' top level domain is reserved for exactly this, so it can
        /// never belong to anyone.
        /// </summary>
        private const string AgentOrigin = "https://signature-agent.test";

        /// <summary>
        /// An origin that publishes no keys, so that the example can show
        /// what a signature this element cannot check reads as.
        /// </summary>
        private const string SilentAgentOrigin = "https://no-keys.test";

        /// <summary>
        /// The host the signed requests in this example are made to.
        /// </summary>
        private const string RequestHost = "example.com";

        /// <summary>
        /// The scheme the signed requests in this example are made with.
        /// </summary>
        private const string RequestScheme = "https";

        /// <summary>
        /// The address of the registry of agent cards this example serves
        /// from memory, being a text document listing one card address per
        /// line.
        /// </summary>
        private const string RegistryUrl = "https://registry.test/agents.txt";

        /// <summary>
        /// The address of the agent card this example serves from memory.
        /// The card's 'client_id' field has to be this same address.
        /// </summary>
        private const string CardUrl =
            "https://signature-agent.test/card.json";

        /// <summary>
        /// The origin of the Web Bot Auth research service Cloudflare runs,
        /// which publishes the Ed25519 key printed in RFC 9421, so a request
        /// signed here with that key verifies against what the service is
        /// serving right now.
        /// </summary>
        private const string ResearchOrigin =
            "https://http-message-signatures-example.research.cloudflare.com";

        /// <summary>
        /// The origin of the agent OpenAI runs, which publishes a key
        /// directory for ChatGPT. OpenAI holds its own private keys, so the
        /// live check against this origin shows a wrong key being refused
        /// rather than a signature verifying.
        /// </summary>
        private const string ChatGptOrigin = "https://chatgpt.com";

        /// <summary>
        /// How long the live checks wait for the two services. Generous, so
        /// that a slow connection still shows the result, whilst an
        /// unreachable service holds the example up no longer than this.
        /// </summary>
        private static readonly TimeSpan LiveTimeout =
            TimeSpan.FromSeconds(15);

        public static void Main(string[] args)
        {
            var instance = new Program();
            instance.RunExample();

            Console.WriteLine(Separator);
            Console.WriteLine("Example complete. Press any key to exit.");
            // Wait for user to press a key, unless the example is being run
            // by something that has no keyboard to press, such as the test
            // that keeps this example working.
            if (Console.IsInputRedirected == false)
            {
                Console.ReadKey();
            }
        }

        /// <summary>
        /// Run the example
        /// </summary>
        public void RunExample()
        {
            WriteIntroduction();

            var loggerFactory = new LoggerFactory();
            // The handler answers the element's fetches from a table held
            // in memory, so the first five requests never reach the network
            // and print the same thing every time they run.
            using (var handler = new ExampleKeyDirectoryHandler())
            using (var httpClient = new HttpClient(handler, false))
            {
                handler.AddDirectory(
                    AgentOrigin + Constants.DIRECTORY_PATH,
                    "Fetching pages to answer a person's question",
                    TestKeyPublicPart);
                handler.AddResponse(
                    RegistryUrl, CardUrl + "\n", "text/plain");
                handler.AddResponse(CardUrl, CardJson(), "application/json");

                // Create a pipeline holding one agent signature element.
                using (var pipeline = new PipelineBuilder(loggerFactory)
                    .AddFlowElement(
                        new AgentSignatureElementBuilder(loggerFactory)
                            // The element fetches key directories with this
                            // client. A real deployment leaves this alone so
                            // that the element makes a client of its own.
                            .SetHttpClient(httpClient)
                            // A key directory served from memory answers at
                            // once, so a generous budget here only makes the
                            // example the same every time it runs.
                            .SetWaitBudget(TimeSpan.FromSeconds(5))
                            .Build())
                    .Build())
                {
                    RunPlainRequest(pipeline);
                    RunSignedRequest(pipeline);
                    RunTamperedRequest(pipeline);
                    RunSilentAgentRequest(pipeline);
                }

                // A second pipeline with a registry of agent cards
                // configured, so the card properties have values.
                using (var pipeline = new PipelineBuilder(loggerFactory)
                    .AddFlowElement(
                        new AgentSignatureElementBuilder(loggerFactory)
                            .SetHttpClient(httpClient)
                            .SetWaitBudget(TimeSpan.FromSeconds(5))
                            .SetRegistry(RegistryUrl)
                            .Build())
                    .Build())
                {
                    RunCardRequest(pipeline);
                }
            }

            RunLiveChecks(loggerFactory);

            Console.WriteLine(Separator);
            Console.WriteLine(
                "A signature says which agent sent the request. It does " +
                "not say what kind of agent it is. To report IsCrawler, " +
                "CrawlerName and IsArtificialIntelligence alongside these " +
                "properties, add the FiftyOne.DeviceDetection engine to " +
                "the same pipeline, because those properties come from the " +
                "device detection data file. This repository ships no data " +
                "file, so this example uses the agent signature element on " +
                "its own.");
            WriteFindOutMore();
        }

        /// <summary>
        /// Print where to find out more, so that anyone this example is
        /// shown to can follow up from the output itself.
        /// </summary>
        private static void WriteFindOutMore()
        {
            Console.WriteLine(Separator);
            Console.WriteLine("Find out more");
            Console.WriteLine(
                "  The device detection engine, which adds IsCrawler, " +
                "CrawlerName and IsArtificialIntelligence:");
            Console.WriteLine(
                "    https://github.com/51Degrees/device-detection-dotnet");
            Console.WriteLine(
                "  This repository, where the agent signature element " +
                "lives:");
            Console.WriteLine(
                "    https://github.com/51Degrees/pipeline-dotnet");
            Console.WriteLine("  The 51Degrees documentation:");
            Console.WriteLine(
                "    https://51degrees.com/documentation" +
                "?utm_source=code&utm_medium=example" +
                "&utm_campaign=pipeline-dotnet" +
                "&utm_content=examples-agentsignature-program.cs" +
                "&utm_term=find-out-more");
            Console.WriteLine(
                "  More about 51Degrees and how to get in touch:");
            Console.WriteLine(
                "    https://51degrees.com" +
                "?utm_source=code&utm_medium=example" +
                "&utm_campaign=pipeline-dotnet" +
                "&utm_content=examples-agentsignature-program.cs" +
                "&utm_term=about-51degrees");
        }

        /// <summary>
        /// Say in plain words what the element does and which headers it
        /// reads.
        /// </summary>
        private void WriteIntroduction()
        {
            Console.WriteLine(Separator);
            Console.WriteLine("Verifying an agent's request signature");
            Console.WriteLine(
                "Web Bot Auth is a way for an automated agent, such as a " +
                "search crawler or an assistant that fetches pages on " +
                "someone's behalf, to prove which agent it is. The agent " +
                "signs the request with a private key and sends three " +
                "headers, being 'Signature', 'Signature-Input' and " +
                "'Signature-Agent'. This element reads those three " +
                "headers, fetches the public key from the address the " +
                "'Signature-Agent' header names and reports whether the " +
                "signature checks out.");
            Console.WriteLine(
                "The five requests below show each of the outcomes and the " +
                "agent card properties. Their keys, card and registry are " +
                "served from inside this example, so they print the same " +
                "thing every time. Two live checks follow, which reach the " +
                "key directories Cloudflare and OpenAI publish, and which " +
                "say so plainly where a service cannot be reached.");
        }

        /// <summary>
        /// A request with no signature headers at all, which is what nearly
        /// every request looks like, because only a handful of agents sign
        /// today.
        /// </summary>
        /// <param name="pipeline">The pipeline to process with.</param>
        private void RunPlainRequest(IPipeline pipeline)
        {
            Console.WriteLine(Separator);
            Console.WriteLine("1. A request with no signature headers");
            var evidence = new Dictionary<string, string>()
            {
                { Constants.EVIDENCE_HOST_KEY, RequestHost },
                { CoreConstants.EVIDENCE_PROTOCOL, RequestScheme },
            };
            var result = Process(pipeline, evidence);
            WriteStatus(result);
            Console.WriteLine(
                "An absent signature is never evidence against a request.");
        }

        /// <summary>
        /// A request signed with the test key, whose public part the
        /// handler serves from the agent's key directory.
        /// </summary>
        /// <param name="pipeline">The pipeline to process with.</param>
        private void RunSignedRequest(IPipeline pipeline)
        {
            Console.WriteLine(Separator);
            Console.WriteLine("2. A request signed with the test key");
            var signed = SignRequest(AgentOrigin);
            var result = Process(pipeline, ToEvidence(signed));
            WriteStatus(result);
            WriteDetails(result);
        }

        /// <summary>
        /// The same request with one byte of the signature changed, which
        /// is what an agent that is being copied by someone else looks
        /// like.
        /// </summary>
        /// <param name="pipeline">The pipeline to process with.</param>
        private void RunTamperedRequest(IPipeline pipeline)
        {
            Console.WriteLine(Separator);
            Console.WriteLine(
                "3. The same request with one byte of the signature " +
                "changed");
            var signed = SignRequest(AgentOrigin);
            signed.Signature =
                AgentRequestSigner.ChangeOneByte(signed.Signature);
            var result = Process(pipeline, ToEvidence(signed));
            WriteStatus(result);
            Console.WriteLine(
                "The key was read and the signature did not check out " +
                "against it, so the request is not from the agent it names.");
        }

        /// <summary>
        /// The same request naming an agent whose key directory cannot be
        /// read, which says nothing about the agent either way.
        /// </summary>
        /// <param name="pipeline">The pipeline to process with.</param>
        private void RunSilentAgentRequest(IPipeline pipeline)
        {
            Console.WriteLine(Separator);
            Console.WriteLine(
                "4. The same request naming an agent that publishes no keys");
            var signed = SignRequest(SilentAgentOrigin);
            var result = Process(pipeline, ToEvidence(signed));
            WriteStatus(result);
            Console.WriteLine(
                "There was no key to check the signature against, so this " +
                "is not evidence against the agent.");
        }

        /// <summary>
        /// The same signed request again with a registry of agent cards
        /// configured, so the agent's name, product token and card address
        /// are reported alongside the verified signature.
        /// </summary>
        /// <param name="pipeline">The pipeline to process with.</param>
        private void RunCardRequest(IPipeline pipeline)
        {
            Console.WriteLine(Separator);
            Console.WriteLine(
                "5. The same request with a registry of agent cards " +
                "configured");
            var signed = SignRequest(AgentOrigin);
            var result = Process(pipeline, ToEvidence(signed));
            WriteStatus(result);
            WriteDetails(result);
            Console.WriteLine(
                "An agent card is where an agent says who it is. The name, " +
                "the product token to look for in robots.txt and the card " +
                "address now have values, because the registry configured " +
                "with SetRegistry lists a card whose keys are the ones the " +
                "signature verified against.");
        }

        /// <summary>
        /// Check a signature against the two live services, saying plainly
        /// where one cannot be reached. Nothing an unreachable service
        /// causes is evidence against an agent, so the example carries on
        /// either way.
        /// </summary>
        /// <param name="loggerFactory">The logger factory.</param>
        private void RunLiveChecks(ILoggerFactory loggerFactory)
        {
            // No handler is set here, so the element fetches with a client
            // of its own over the real network, exactly as a deployment
            // does.
            using (var pipeline = new PipelineBuilder(loggerFactory)
                .AddFlowElement(
                    new AgentSignatureElementBuilder(loggerFactory)
                        // The live checks wait for the fetch so the result
                        // shows in one request. A web server keeps the
                        // default budget instead, reporting Timeout whilst
                        // the fetch finishes in the background.
                        .SetWaitBudget(LiveTimeout)
                        .SetFetchTimeout(LiveTimeout)
                        .Build())
                .Build())
            {
                RunResearchServiceRequest(pipeline);
                RunChatGptRequest(pipeline);
            }
        }

        /// <summary>
        /// Sign a request with the RFC 9421 test key and verify against the
        /// key directory Cloudflare's Web Bot Auth research service is
        /// publishing right now, which serves that same key.
        /// </summary>
        /// <param name="pipeline">The pipeline to process with.</param>
        private void RunResearchServiceRequest(IPipeline pipeline)
        {
            Console.WriteLine(Separator);
            Console.WriteLine(
                "6. Live: a request checked against Cloudflare's Web Bot " +
                "Auth research service");
            var signed = SignRequest(ResearchOrigin);
            var result = Process(pipeline, ToEvidence(signed));
            WriteStatus(result);
            if (string.Equals(
                result.AgentSignature.Value,
                Constants.STATUS_VERIFIED,
                StringComparison.Ordinal))
            {
                WriteDetails(result);
                Console.WriteLine(
                    "The signature was checked against the key directory " +
                    "the research service at " + ResearchOrigin +
                    " is publishing right now, fetched over the real " +
                    "network.");
            }
            else
            {
                Console.WriteLine(
                    "The research service could not be reached, so this " +
                    "check was skipped. An unreachable directory is never " +
                    "evidence against an agent.");
            }
        }

        /// <summary>
        /// The same signed request naming the agent OpenAI runs. OpenAI
        /// holds its own private keys, so the live key directory ChatGPT
        /// publishes rightly refuses the test key.
        /// </summary>
        /// <param name="pipeline">The pipeline to process with.</param>
        private void RunChatGptRequest(IPipeline pipeline)
        {
            Console.WriteLine(Separator);
            Console.WriteLine(
                "7. Live: the same request pretending to be ChatGPT");
            var signed = SignRequest(ChatGptOrigin);
            var result = Process(pipeline, ToEvidence(signed));
            WriteStatus(result);
            if (string.Equals(
                result.AgentSignature.Value,
                Constants.STATUS_INVALID,
                StringComparison.Ordinal))
            {
                Console.WriteLine(
                    "The key directory OpenAI publishes at " +
                    ChatGptOrigin + " was fetched over the real network " +
                    "and read, and it holds no key matching this " +
                    "signature, so a request claiming to be ChatGPT " +
                    "without OpenAI's private key is refused. This is the " +
                    "protection working.");
            }
            else
            {
                Console.WriteLine(
                    "The ChatGPT key directory could not be reached, so " +
                    "this check was skipped. An unreachable directory is " +
                    "never evidence against an agent.");
            }
        }

        /// <summary>
        /// The agent card this example serves, in which the agent says who
        /// it is. The 'client_id' has to be the address the card is served
        /// from, and the 'jwks_uri' names the same key directory the
        /// signature verifies against, which is how a card is tied to an
        /// agent.
        /// </summary>
        /// <returns>The card as JSON.</returns>
        private static string CardJson()
        {
            return "{" +
                "\"client_id\":\"" + CardUrl + "\"," +
                "\"client_name\":\"Example Signing Agent\"," +
                "\"client_uri\":\"" + AgentOrigin + "\"," +
                "\"jwks_uri\":\"" + AgentOrigin + Constants.DIRECTORY_PATH +
                "\"," +
                "\"web_bot_auth\":{" +
                "\"rfc9309-product-token\":\"ExampleAgent\"" +
                "}}";
        }

        /// <summary>
        /// Sign a request the way an agent would. The element itself never
        /// signs anything, so the signing code lives here in the example.
        /// </summary>
        /// <param name="agentOrigin">
        /// The origin to name in the 'Signature-Agent' header.
        /// </param>
        /// <returns>The three headers a signed request carries.</returns>
        private static SignedRequestHeaders SignRequest(string agentOrigin)
        {
            // The times sit around the real current time, so the example
            // needs no clock of its own and works whatever today's date is.
            var now = DateTimeOffset.UtcNow;
            return AgentRequestSigner.Sign(
                TestKey,
                TestKeyThumbprint,
                RequestHost,
                agentOrigin,
                now.AddSeconds(-30),
                now.AddHours(1));
        }

        /// <summary>
        /// Turn the three headers into the evidence a web server would put
        /// into the pipeline.
        /// </summary>
        /// <param name="signed">The headers.</param>
        /// <returns>The evidence.</returns>
        private static IDictionary<string, string> ToEvidence(
            SignedRequestHeaders signed)
        {
            return new Dictionary<string, string>()
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
                { Constants.EVIDENCE_HOST_KEY, RequestHost },
                { CoreConstants.EVIDENCE_PROTOCOL, RequestScheme },
            };
        }

        /// <summary>
        /// Process the evidence given and hand back what the element made
        /// of it.
        /// </summary>
        /// <param name="pipeline">The pipeline to process with.</param>
        /// <param name="evidence">The evidence.</param>
        /// <returns>The element data.</returns>
        private static IAgentSignatureData Process(
            IPipeline pipeline,
            IDictionary<string, string> evidence)
        {
            using (var data = pipeline.CreateFlowData())
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
        /// Print the status and the reason, which always have a value.
        /// </summary>
        /// <param name="result">The element data.</param>
        private static void WriteStatus(IAgentSignatureData result)
        {
            Console.WriteLine("Status: " + result.AgentSignature.Value);
            Console.WriteLine("Reason: " + result.AgentSignatureReason.Value);
        }

        /// <summary>
        /// Print every detail property. A property with no value says why
        /// rather than returning something that looks like an answer.
        /// </summary>
        /// <param name="result">The element data.</param>
        private static void WriteDetails(IAgentSignatureData result)
        {
            Write("Agent", result.AgentSignatureAgent);
            Write("Key id", result.AgentSignatureKeyId);
            Write("Algorithm", result.AgentSignatureAlgorithm);
            Write("Created", result.AgentSignatureCreated);
            Write("Expires", result.AgentSignatureExpires);
            Write("Nonce", result.AgentSignatureNonce);
            Write("Purpose", result.AgentSignaturePurpose);
            Write("Name", result.AgentSignatureName);
            Write("Product token", result.AgentSignatureProductToken);
            Write("Card URL", result.AgentSignatureCardUrl);
        }

        /// <summary>
        /// Print one property, saying why it has no value when it has none.
        /// </summary>
        /// <typeparam name="T">The type of the property.</typeparam>
        /// <param name="name">The name to print.</param>
        /// <param name="value">The property.</param>
        private static void Write<T>(
            string name,
            IAspectPropertyValue<T> value)
        {
            var text = value.HasValue
                ? string.Format(
                    CultureInfo.InvariantCulture, "{0}", value.Value)
                : "no value, because " + value.NoValueMessage;
            Console.WriteLine(
                "  " + name.PadRight(14) + ": " + text);
        }
    }
}
