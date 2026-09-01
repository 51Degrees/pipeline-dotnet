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

using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FiftyOne.Pipeline.AgentSignature.Tests.Helpers
{
    /// <summary>
    /// The three headers a signed request carries, together with the text
    /// that was signed.
    /// </summary>
    public class SignedRequest
    {
        /// <summary>The value of the 'Signature' header.</summary>
        public string Signature { get; set; }

        /// <summary>The value of the 'Signature-Input' header.</summary>
        public string SignatureInput { get; set; }

        /// <summary>
        /// The value of the 'Signature-Agent' header, or null when the
        /// request carries none.
        /// </summary>
        public string SignatureAgent { get; set; }

        /// <summary>The text that was signed.</summary>
        public string SignatureBase { get; set; }
    }

    /// <summary>
    /// How a test wants a request signed.
    /// </summary>
    public class SigningOptions
    {
        /// <summary>
        /// The key to sign with, as a JSON Web Key including its private
        /// part. Use <see cref="Fixtures.Ed25519Key"/> or
        /// <see cref="Fixtures.RsaKey"/>.
        /// </summary>
        public string KeyJson { get; set; } = Fixtures.Ed25519Key();

        /// <summary>The label the signature headers share.</summary>
        public string Label { get; set; } = "sig1";

        /// <summary>The host the request is made to.</summary>
        public string Host { get; set; } = "example.com";

        /// <summary>The scheme of the request.</summary>
        public string Scheme { get; set; } = "https";

        /// <summary>
        /// The origin the agent publishes its keys at, or null for a
        /// request with no 'Signature-Agent' header.
        /// </summary>
        public string SignatureAgent { get; set; } =
            Fixtures.SignatureAgentOrigin;

        /// <summary>
        /// The label of the 'Signature-Agent' member, or null to send the
        /// header in the bare quoted string form that the earlier drafts
        /// used.
        /// </summary>
        public string SignatureAgentLabel { get; set; } = "agent1";

        /// <summary>When the signature is made.</summary>
        public DateTimeOffset Created { get; set; } =
            DateTimeOffset.FromUnixTimeSeconds(1735689600);

        /// <summary>When the signature stops being valid.</summary>
        public DateTimeOffset Expires { get; set; } =
            DateTimeOffset.FromUnixTimeSeconds(4889289600);

        /// <summary>
        /// The text to write as the 'created' parameter, or null to write
        /// <see cref="Created"/> as Unix seconds. A test uses this to send a
        /// number no point in time can be written as, such as one far
        /// outside the range the framework holds.
        /// </summary>
        public string CreatedText { get; set; }

        /// <summary>
        /// The text to write as the 'expires' parameter, or null to write
        /// <see cref="Expires"/> as Unix seconds.
        /// </summary>
        public string ExpiresText { get; set; }

        /// <summary>
        /// True to cover '@authority', which every well behaved agent does.
        /// A test sets this to false to send a signature that is tied to
        /// nothing about the request it arrived on.
        /// </summary>
        public bool CoverAuthority { get; set; } = true;

        /// <summary>
        /// The key id to name, which defaults to the thumbprint of the
        /// Ed25519 test key.
        /// </summary>
        public string KeyId { get; set; } = Fixtures.Ed25519Thumbprint;

        /// <summary>
        /// The algorithm to name, or null to leave the 'alg' parameter out.
        /// </summary>
        public string Algorithm { get; set; } = "ed25519";

        /// <summary>
        /// The tag to name, or null to leave the 'tag' parameter out.
        /// </summary>
        public string Tag { get; set; } = "web-bot-auth";

        /// <summary>
        /// The nonce to send, or null to leave the 'nonce' parameter out.
        /// </summary>
        public string Nonce { get; set; }

        /// <summary>
        /// True to leave the 'keyid' parameter out, so that a test can see
        /// what a signature missing a required parameter reads as.
        /// </summary>
        public bool OmitKeyId { get; set; }

        /// <summary>
        /// Covered components to add beyond '@authority' and the signature
        /// agent, given as a component identifier and the value that
        /// belongs on its line of the signature base. For example
        /// '"@target-uri"' and 'https://example.com/path'.
        /// </summary>
        public IList<KeyValuePair<string, string>> ExtraComponents { get; } =
            new List<KeyValuePair<string, string>>();
    }

    /// <summary>
    /// Signs requests the way an automated agent would, so that the tests
    /// can make signatures that the standard's own vectors do not carry.
    /// The element itself never signs anything.
    /// </summary>
    public static class RequestSigner
    {
        /// <summary>
        /// Sign a request.
        /// </summary>
        /// <param name="options">How to sign it.</param>
        /// <returns>The headers to put into evidence.</returns>
        public static SignedRequest Sign(SigningOptions options)
        {
            var components = new List<KeyValuePair<string, string>>();
            if (options.CoverAuthority)
            {
                components.Add(new KeyValuePair<string, string>(
                    "\"@authority\"", options.Host.ToLowerInvariant()));
            }

            string signatureAgentHeader = null;
            if (options.SignatureAgent != null)
            {
                var quoted = "\"" + options.SignatureAgent + "\"";
                if (options.SignatureAgentLabel == null)
                {
                    signatureAgentHeader = quoted;
                    components.Add(new KeyValuePair<string, string>(
                        "\"signature-agent\"", quoted));
                }
                else
                {
                    signatureAgentHeader =
                        options.SignatureAgentLabel + "=" + quoted;
                    components.Add(new KeyValuePair<string, string>(
                        "\"signature-agent\";key=\"" +
                            options.SignatureAgentLabel + "\"",
                        quoted));
                }
            }
            foreach (var extra in options.ExtraComponents)
            {
                components.Add(extra);
            }

            var innerList = new StringBuilder("(");
            for (var i = 0; i < components.Count; i++)
            {
                if (i > 0)
                {
                    innerList.Append(" ");
                }
                innerList.Append(components[i].Key);
            }
            innerList.Append(")");

            var parameters = new StringBuilder(innerList.ToString());
            Append(parameters, "created",
                options.CreatedText ??
                    options.Created.ToUnixTimeSeconds().ToString(
                        CultureInfo.InvariantCulture));
            Append(parameters, "expires",
                options.ExpiresText ??
                    options.Expires.ToUnixTimeSeconds().ToString(
                        CultureInfo.InvariantCulture));
            if (options.OmitKeyId == false)
            {
                AppendQuoted(parameters, "keyid", options.KeyId);
            }
            if (options.Algorithm != null)
            {
                AppendQuoted(parameters, "alg", options.Algorithm);
            }
            if (options.Nonce != null)
            {
                AppendQuoted(parameters, "nonce", options.Nonce);
            }
            if (options.Tag != null)
            {
                AppendQuoted(parameters, "tag", options.Tag);
            }

            var signatureParams = parameters.ToString();
            var signatureBase = BuildBase(components, signatureParams);
            var signature = SignBytes(
                options.KeyJson, Encoding.ASCII.GetBytes(signatureBase));

            return new SignedRequest
            {
                Signature = options.Label + "=:" +
                    Convert.ToBase64String(signature) + ":",
                SignatureInput = options.Label + "=" + signatureParams,
                SignatureAgent = signatureAgentHeader,
                SignatureBase = signatureBase,
            };
        }

        /// <summary>
        /// Build the text that is signed, being one line per covered
        /// component followed by the signature parameters.
        /// </summary>
        /// <param name="components">
        /// The component identifiers and their values.
        /// </param>
        /// <param name="signatureParams">The signature parameters.</param>
        /// <returns>The signature base.</returns>
        public static string BuildBase(
            IEnumerable<KeyValuePair<string, string>> components,
            string signatureParams)
        {
            var builder = new StringBuilder();
            foreach (var component in components)
            {
                builder.Append(component.Key);
                builder.Append(": ");
                builder.Append(component.Value);
                builder.Append("\n");
            }
            builder.Append("\"@signature-params\": ");
            builder.Append(signatureParams);
            return builder.ToString();
        }

        /// <summary>
        /// Sign bytes with the key given, choosing the algorithm from the
        /// key type.
        /// </summary>
        /// <param name="keyJson">
        /// The key as a JSON Web Key including its private part.
        /// </param>
        /// <param name="content">The bytes to sign.</param>
        /// <returns>The signature.</returns>
        public static byte[] SignBytes(string keyJson, byte[] content)
        {
            using (var document = JsonDocument.Parse(keyJson))
            {
                var root = document.RootElement;
                var keyType = root.GetProperty("kty").GetString();
                if (keyType == "OKP")
                {
                    var privateKey = new Ed25519PrivateKeyParameters(
                        Decode(root.GetProperty("d").GetString()), 0);
                    var signer = new Ed25519Signer();
                    signer.Init(true, privateKey);
                    signer.BlockUpdate(content, 0, content.Length);
                    return signer.GenerateSignature();
                }
                if (keyType == "RSA")
                {
                    using (var rsa = RSA.Create())
                    {
                        rsa.ImportParameters(ReadRsaParameters(root));
                        return rsa.SignData(
                            content,
                            HashAlgorithmName.SHA512,
                            RSASignaturePadding.Pss);
                    }
                }
                throw new NotSupportedException(
                    "The tests can sign with OKP and RSA keys only, not '" +
                    keyType + "'.");
            }
        }

        /// <summary>
        /// Take the public part of a key, so that a test can serve the
        /// public key from a fake directory whilst signing with the
        /// private one.
        /// </summary>
        /// <param name="keyJson">The key as JSON.</param>
        /// <returns>The public key as JSON.</returns>
        public static string PublicPart(string keyJson)
        {
            using (var document = JsonDocument.Parse(keyJson))
            {
                var root = document.RootElement;
                var builder = new StringBuilder("{");
                var first = true;
                foreach (var name in new[]
                    { "kty", "kid", "use", "alg", "crv", "x", "y", "n", "e" })
                {
                    if (root.TryGetProperty(name, out var value) == false)
                    {
                        continue;
                    }
                    if (first == false)
                    {
                        builder.Append(",");
                    }
                    first = false;
                    builder.Append("\"").Append(name).Append("\":\"")
                        .Append(value.GetString()).Append("\"");
                }
                builder.Append("}");
                return builder.ToString();
            }
        }

        private static RSAParameters ReadRsaParameters(JsonElement root)
        {
            var modulus = Decode(root.GetProperty("n").GetString());
            var half = (modulus.Length + 1) / 2;
            return new RSAParameters
            {
                Modulus = modulus,
                Exponent = Decode(root.GetProperty("e").GetString()),
                D = Pad(
                    Decode(root.GetProperty("d").GetString()),
                    modulus.Length),
                P = Pad(Decode(root.GetProperty("p").GetString()), half),
                Q = Pad(Decode(root.GetProperty("q").GetString()), half),
                DP = Pad(Decode(root.GetProperty("dp").GetString()), half),
                DQ = Pad(Decode(root.GetProperty("dq").GetString()), half),
                InverseQ = Pad(
                    Decode(root.GetProperty("qi").GetString()), half),
            };
        }

        private static byte[] Pad(byte[] value, int length)
        {
            if (value.Length >= length)
            {
                return value;
            }
            var padded = new byte[length];
            Array.Copy(
                value, 0, padded, length - value.Length, value.Length);
            return padded;
        }

        private static byte[] Decode(string base64Url)
        {
            var padded = base64Url.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2:
                    padded += "==";
                    break;
                case 3:
                    padded += "=";
                    break;
                default:
                    break;
            }
            return Convert.FromBase64String(padded);
        }

        private static void Append(
            StringBuilder builder,
            string name,
            string value)
        {
            builder.Append(";").Append(name).Append("=").Append(value);
        }

        private static void AppendQuoted(
            StringBuilder builder,
            string name,
            string value)
        {
            builder.Append(";").Append(name).Append("=\"")
                .Append(value).Append("\"");
        }
    }
}
