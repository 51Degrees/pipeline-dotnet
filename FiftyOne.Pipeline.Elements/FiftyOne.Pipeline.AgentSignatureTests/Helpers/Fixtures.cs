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

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FiftyOne.Pipeline.AgentSignature.Tests.Helpers
{
    /// <summary>
    /// One signed request from the cloudflare/web-bot-auth test vectors.
    /// </summary>
    public class SignedRequestVector
    {
        /// <summary>The key the request was signed with, as JSON.</summary>
        public string KeyJson { get; set; }

        /// <summary>The URL the request was made to.</summary>
        public string TargetUrl { get; set; }

        /// <summary>When the signature was made, in Unix milliseconds.</summary>
        public long CreatedMs { get; set; }

        /// <summary>
        /// When the signature stops being valid, in Unix milliseconds.
        /// </summary>
        public long ExpiresMs { get; set; }

        /// <summary>The nonce the signature carries.</summary>
        public string Nonce { get; set; }

        /// <summary>The label the two signature headers share.</summary>
        public string Label { get; set; }

        /// <summary>The value of the 'Signature' header.</summary>
        public string Signature { get; set; }

        /// <summary>The value of the 'Signature-Input' header.</summary>
        public string SignatureInput { get; set; }

        /// <summary>
        /// The value of the 'Signature-Agent' header, or null when the
        /// vector sends none.
        /// </summary>
        public string SignatureAgent { get; set; }

        /// <summary>
        /// The label of the 'Signature-Agent' member the signature covers,
        /// or null when the vector covers the whole header or sends none.
        /// </summary>
        public string SignatureAgentKey { get; set; }

        /// <summary>The key id the signature names.</summary>
        public string KeyId { get; set; }

        /// <summary>The algorithm the signature names.</summary>
        public string Algorithm { get; set; }

        /// <summary>
        /// The host the request was made to, taken from the target URL.
        /// </summary>
        public string Host => new Uri(TargetUrl).Host;

        /// <inheritdoc/>
        public override string ToString() =>
            Label + " " + (Algorithm ?? "?") +
            (SignatureAgent == null ? " (no agent)" : " (agent)");
    }

    /// <summary>
    /// One 'Signature-Agent' header parsing case.
    /// </summary>
    public class SignatureAgentVector
    {
        /// <summary>The name of the case.</summary>
        public string Name { get; set; }

        /// <summary>The header value.</summary>
        public string Header { get; set; }

        /// <summary>The members the header is expected to parse to.</summary>
        public IList<SignatureAgentVectorEntry> Entries { get; } =
            new List<SignatureAgentVectorEntry>();
    }

    /// <summary>
    /// One expected member of a parsed 'Signature-Agent' header.
    /// </summary>
    public class SignatureAgentVectorEntry
    {
        /// <summary>The expected label.</summary>
        public string Label { get; set; }

        /// <summary>The expected URI.</summary>
        public string Uri { get; set; }

        /// <summary>The expected type.</summary>
        public string Type { get; set; }
    }

    /// <summary>
    /// One agent card case.
    /// </summary>
    public class AgentCardVector
    {
        /// <summary>The name of the case.</summary>
        public string Name { get; set; }

        /// <summary>The URL the card is served from.</summary>
        public string Url { get; set; }

        /// <summary>True when the card is expected to be accepted.</summary>
        public bool Valid { get; set; }

        /// <summary>The card as JSON.</summary>
        public string CardJson { get; set; }
    }

    /// <summary>
    /// One agent card registry case.
    /// </summary>
    public class RegistryVector
    {
        /// <summary>The name of the case.</summary>
        public string Name { get; set; }

        /// <summary>The registry text.</summary>
        public string RegistryText { get; set; }

        /// <summary>The card URLs the text is expected to list.</summary>
        public IList<string> CardUrls { get; } = new List<string>();
    }

    /// <summary>
    /// One RFC 7638 key thumbprint case.
    /// </summary>
    public class ThumbprintVector
    {
        /// <summary>The name of the case.</summary>
        public string Name { get; set; }

        /// <summary>The key as JSON.</summary>
        public string KeyJson { get; set; }

        /// <summary>
        /// The canonical JSON the standard says to hash, which the fixture
        /// carries as hexadecimal.
        /// </summary>
        public string CanonicalJson { get; set; }

        /// <summary>The expected thumbprint.</summary>
        public string Thumbprint { get; set; }
    }

    /// <summary>
    /// The signed key directory response case.
    /// </summary>
    public class DirectoryResponseVector
    {
        /// <summary>The name of the case.</summary>
        public string Name { get; set; }

        /// <summary>The URL the directory is served from.</summary>
        public string TargetUrl { get; set; }

        /// <summary>The response headers.</summary>
        public IDictionary<string, string> Headers { get; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The response body.</summary>
        public string Body { get; set; }

        /// <summary>The signature base the standard says to build.</summary>
        public string SignatureBase { get; set; }
    }

    /// <summary>
    /// Reads the test vectors that were copied verbatim from
    /// cloudflare/web-bot-auth. See Fixtures/SOURCE.txt for where each file
    /// came from.
    /// </summary>
    public static class Fixtures
    {
        /// <summary>
        /// The origin the vectors publish their keys at.
        /// </summary>
        public const string SignatureAgentOrigin =
            "https://signature-agent.test";

        /// <summary>
        /// The URL the vectors' key directory is served from.
        /// </summary>
        public const string SignatureAgentDirectoryUrl =
            SignatureAgentOrigin +
            "/.well-known/http-message-signatures-directory";

        /// <summary>
        /// The thumbprint of the RFC 9421 test Ed25519 key.
        /// </summary>
        public const string Ed25519Thumbprint =
            "poqkLGiymh_W0uP6PZFw-dvez3QJT5SolqXBCW38r0U";

        /// <summary>
        /// The thumbprint of the RFC 9421 test RSA key.
        /// </summary>
        public const string RsaThumbprint =
            "oD0HwocPBSfpNy5W3bpJeyFGY_IQ_YpqxSjQ3Yd-CLA";

        /// <summary>
        /// Read the text of a fixture file.
        /// </summary>
        /// <param name="name">The file name.</param>
        /// <returns>The file content.</returns>
        public static string ReadText(string name)
        {
            return File.ReadAllText(
                Path.Combine(
                    Path.GetDirectoryName(
                        typeof(Fixtures).Assembly.Location),
                    "Fixtures",
                    name));
        }

        /// <summary>
        /// The four signed requests whose signatures have already expired.
        /// </summary>
        /// <returns>The vectors.</returns>
        public static IList<SignedRequestVector> ArchitectureV1() =>
            ReadSignedRequests("web_bot_auth_architecture_v1.json");

        /// <summary>
        /// The four signed requests whose signatures are valid until the
        /// year 2124.
        /// </summary>
        /// <returns>The vectors.</returns>
        public static IList<SignedRequestVector> ArchitectureV2() =>
            ReadSignedRequests("web_bot_auth_architecture_v2.json");

        /// <summary>
        /// The Ed25519 key the RFC 9421 examples use, including its private
        /// part so that tests can sign fresh requests with it.
        /// </summary>
        /// <returns>The key as JSON.</returns>
        public static string Ed25519Key() => ReadText("ed25519.json");

        /// <summary>
        /// The RSA-PSS key the RFC 9421 examples use, including its private
        /// part.
        /// </summary>
        /// <returns>The key as JSON.</returns>
        public static string RsaKey() => ReadText("rsapss.json");

        /// <summary>
        /// The 'Signature-Agent' header parsing cases.
        /// </summary>
        /// <returns>The cases.</returns>
        public static IList<SignatureAgentVector> SignatureAgents()
        {
            var result = new List<SignatureAgentVector>();
            using (var document = JsonDocument.Parse(
                ReadText("web_bot_auth_signature_agent_v1.json")))
            {
                foreach (var item in document.RootElement.EnumerateArray())
                {
                    var vector = new SignatureAgentVector
                    {
                        Name = item.GetProperty("name").GetString(),
                        Header = item.GetProperty("header").GetString(),
                    };
                    foreach (var entry in
                        item.GetProperty("entries").EnumerateArray())
                    {
                        vector.Entries.Add(new SignatureAgentVectorEntry
                        {
                            Label = entry.GetProperty("label").GetString(),
                            Uri = entry.GetProperty("uri").GetString(),
                            Type = entry.GetProperty("type").GetString(),
                        });
                    }
                    result.Add(vector);
                }
            }
            return result;
        }

        /// <summary>
        /// The agent card cases, two valid and two not.
        /// </summary>
        /// <returns>The cases.</returns>
        public static IList<AgentCardVector> AgentCards()
        {
            var result = new List<AgentCardVector>();
            using (var document = JsonDocument.Parse(
                ReadText("web_bot_auth_signature_agent_card_v1.json")))
            {
                foreach (var item in document.RootElement.EnumerateArray())
                {
                    result.Add(new AgentCardVector
                    {
                        Name = item.GetProperty("name").GetString(),
                        Url = item.GetProperty("url").GetString(),
                        Valid = item.GetProperty("valid").GetBoolean(),
                        CardJson = item.GetProperty("card").GetRawText(),
                    });
                }
            }
            return result;
        }

        /// <summary>
        /// The agent card registry cases.
        /// </summary>
        /// <returns>The cases.</returns>
        public static IList<RegistryVector> Registries()
        {
            var result = new List<RegistryVector>();
            using (var document = JsonDocument.Parse(
                ReadText("web_bot_auth_registry_v1.json")))
            {
                foreach (var item in document.RootElement.EnumerateArray())
                {
                    var vector = new RegistryVector
                    {
                        Name = item.GetProperty("name").GetString(),
                        RegistryText =
                            item.GetProperty("registry_txt").GetString(),
                    };
                    foreach (var url in item
                        .GetProperty("signature_agent_cards")
                        .EnumerateArray())
                    {
                        vector.CardUrls.Add(url.GetString());
                    }
                    result.Add(vector);
                }
            }
            return result;
        }

        /// <summary>
        /// The RFC 7638 key thumbprint cases.
        /// </summary>
        /// <returns>The cases.</returns>
        public static IList<ThumbprintVector> Thumbprints()
        {
            var result = new List<ThumbprintVector>();
            using (var document = JsonDocument.Parse(
                ReadText("jwk_thumbprint_vectors.json")))
            {
                foreach (var item in document.RootElement.EnumerateArray())
                {
                    result.Add(new ThumbprintVector
                    {
                        Name = item.GetProperty("name").GetString(),
                        KeyJson = item.GetProperty("jwk").GetString(),
                        CanonicalJson = FromHex(
                            item.GetProperty("precompute").GetString()),
                        Thumbprint =
                            item.GetProperty("thumbprint").GetString(),
                    });
                }
            }
            return result;
        }

        /// <summary>
        /// The signed key directory response case.
        /// </summary>
        /// <returns>The case.</returns>
        public static DirectoryResponseVector DirectoryResponse()
        {
            using (var document = JsonDocument.Parse(
                ReadText("web_bot_auth_directory_response_v1.json")))
            {
                var item = document.RootElement[0];
                var response = item.GetProperty("response");
                var vector = new DirectoryResponseVector
                {
                    Name = item.GetProperty("name").GetString(),
                    TargetUrl = item.GetProperty("request")
                        .GetProperty("target_url").GetString(),
                    Body = response.GetProperty("body").GetString(),
                    SignatureBase =
                        item.GetProperty("signature_base").GetString(),
                };
                foreach (var header in
                    response.GetProperty("headers").EnumerateObject())
                {
                    vector.Headers[header.Name] = header.Value.GetString();
                }
                return vector;
            }
        }

        private static IList<SignedRequestVector> ReadSignedRequests(
            string name)
        {
            var result = new List<SignedRequestVector>();
            using (var document = JsonDocument.Parse(ReadText(name)))
            {
                foreach (var item in document.RootElement.EnumerateArray())
                {
                    var vector = new SignedRequestVector
                    {
                        KeyJson = item.GetProperty("key").GetRawText(),
                        TargetUrl =
                            item.GetProperty("target_url").GetString(),
                        CreatedMs = item.GetProperty("created_ms").GetInt64(),
                        ExpiresMs = item.GetProperty("expires_ms").GetInt64(),
                        Nonce = item.GetProperty("nonce").GetString(),
                        Label = item.GetProperty("label").GetString(),
                        Signature = item.GetProperty("signature").GetString(),
                        SignatureInput =
                            item.GetProperty("signature_input").GetString(),
                    };
                    if (item.TryGetProperty(
                        "signature_agent", out var agent))
                    {
                        vector.SignatureAgent = agent.GetString();
                    }
                    if (item.TryGetProperty(
                        "signature_agent_key", out var agentKey))
                    {
                        vector.SignatureAgentKey = agentKey.GetString();
                    }
                    using (var key = JsonDocument.Parse(vector.KeyJson))
                    {
                        vector.KeyId =
                            key.RootElement.GetProperty("kty").GetString() ==
                                "RSA"
                                ? RsaThumbprint
                                : Ed25519Thumbprint;
                    }
                    vector.Algorithm = ReadParameter(
                        vector.SignatureInput, "alg");
                    result.Add(vector);
                }
            }
            return result;
        }

        private static string ReadParameter(string input, string name)
        {
            var marker = ";" + name + "=\"";
            var start = input.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
            {
                return null;
            }
            start += marker.Length;
            var end = input.IndexOf('"', start);
            return end < 0 ? null : input.Substring(start, end - start);
        }

        private static string FromHex(string hex)
        {
            var bytes = new byte[hex.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(
                    hex.Substring(i * 2, 2), 16);
            }
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
    }
}
