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
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using System;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Examples.AgentSignature
{
    /// <summary>
    /// The three headers a signed request carries.
    /// </summary>
    public class SignedRequestHeaders
    {
        /// <summary>
        /// The value of the 'Signature' header, being the signature itself.
        /// </summary>
        public string Signature { get; set; }

        /// <summary>
        /// The value of the 'Signature-Input' header, being the list of
        /// what the signature covers and the parameters it was made with.
        /// </summary>
        public string SignatureInput { get; set; }

        /// <summary>
        /// The value of the 'Signature-Agent' header, being the address the
        /// agent publishes its public keys at.
        /// </summary>
        public string SignatureAgent { get; set; }
    }

    /// <summary>
    /// Signs a request the way an automated agent would, so that this
    /// example has a signature to show the element reading. The element
    /// itself never signs anything, which is why the signing code lives
    /// here in the example rather than in the element.
    /// </summary>
    public static class AgentRequestSigner
    {
        /// <summary>
        /// The label the two signature headers share, which lets a request
        /// carry more than one signature.
        /// </summary>
        private const string SignatureLabel = "sig1";

        /// <summary>
        /// The label of the 'Signature-Agent' member the signature covers.
        /// </summary>
        private const string AgentLabel = "agent1";

        /// <summary>
        /// Sign a request with the Ed25519 key given.
        /// </summary>
        /// <param name="keyJson">
        /// The key as a JSON Web Key including its private part.
        /// </param>
        /// <param name="keyId">
        /// The key id to name, being the thumbprint of the public key.
        /// </param>
        /// <param name="host">The host the request is made to.</param>
        /// <param name="agentOrigin">
        /// The origin the agent publishes its keys at.
        /// </param>
        /// <param name="created">When the signature is made.</param>
        /// <param name="expires">
        /// When the signature stops being valid.
        /// </param>
        /// <returns>The three headers.</returns>
        public static SignedRequestHeaders Sign(
            string keyJson,
            string keyId,
            string host,
            string agentOrigin,
            DateTimeOffset created,
            DateTimeOffset expires)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            // The signature covers the host the request was made to and the
            // 'Signature-Agent' member that names where the keys live. A
            // member the signature does not cover says nothing about the
            // agent, because anyone can add a header to a request.
            var quotedOrigin = "\"" + agentOrigin + "\"";
            var agentComponent =
                "\"signature-agent\";key=\"" + AgentLabel + "\"";
            var coveredList =
                "(\"@authority\" " + agentComponent + ")";

            var parameters = new StringBuilder(coveredList);
            Append(parameters, "created", Seconds(created));
            Append(parameters, "expires", Seconds(expires));
            AppendQuoted(parameters, "keyid", keyId);
            AppendQuoted(
                parameters, "alg", Constants.ALGORITHM_ED25519);
            AppendQuoted(
                parameters, "tag", Constants.TAG_WEB_BOT_AUTH);
            var signatureParams = parameters.ToString();

            // The text that is signed is one line for each covered
            // component followed by the parameters above.
            var signatureBase = new StringBuilder()
                .Append("\"@authority\": ")
                .Append(host.ToLowerInvariant())
                .Append("\n")
                .Append(agentComponent)
                .Append(": ")
                .Append(quotedOrigin)
                .Append("\n")
                .Append("\"@signature-params\": ")
                .Append(signatureParams)
                .ToString();

            var signature = SignBytes(
                keyJson, Encoding.ASCII.GetBytes(signatureBase));

            return new SignedRequestHeaders()
            {
                Signature = SignatureLabel + "=:" +
                    Convert.ToBase64String(signature) + ":",
                SignatureInput = SignatureLabel + "=" + signatureParams,
                SignatureAgent = AgentLabel + "=" + quotedOrigin,
            };
        }

        /// <summary>
        /// Change one byte of a 'Signature' header value, so that the
        /// example can show what a signature that does not check out reads
        /// as. The result is still a well formed header, which is the point,
        /// because a header that could not be read would report Malformed
        /// instead.
        /// </summary>
        /// <param name="signatureHeader">The header value.</param>
        /// <returns>The header value with one byte changed.</returns>
        public static string ChangeOneByte(string signatureHeader)
        {
            if (signatureHeader == null)
            {
                throw new ArgumentNullException(nameof(signatureHeader));
            }
            var start = signatureHeader.IndexOf(":", StringComparison.Ordinal);
            var end = signatureHeader.LastIndexOf(
                ":", StringComparison.Ordinal);
            var bytes = Convert.FromBase64String(
                signatureHeader.Substring(start + 1, end - start - 1));
            bytes[0] = (byte)(bytes[0] ^ 0x01);
            return signatureHeader.Substring(0, start + 1) +
                Convert.ToBase64String(bytes) +
                signatureHeader.Substring(end);
        }

        /// <summary>
        /// Sign bytes with the Ed25519 key given.
        /// </summary>
        /// <param name="keyJson">
        /// The key as a JSON Web Key including its private part.
        /// </param>
        /// <param name="content">The bytes to sign.</param>
        /// <returns>The signature.</returns>
        private static byte[] SignBytes(string keyJson, byte[] content)
        {
            using (var document = JsonDocument.Parse(keyJson))
            {
                var privateKey = new Ed25519PrivateKeyParameters(
                    Decode(document.RootElement
                        .GetProperty("d").GetString()),
                    0);
                var signer = new Ed25519Signer();
                signer.Init(true, privateKey);
                signer.BlockUpdate(content, 0, content.Length);
                return signer.GenerateSignature();
            }
        }

        /// <summary>
        /// Decode the base64url form that a JSON Web Key holds its numbers
        /// in, which is ordinary base64 with two characters swapped and the
        /// padding left off.
        /// </summary>
        /// <param name="base64Url">The encoded value.</param>
        /// <returns>The bytes.</returns>
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

        private static string Seconds(DateTimeOffset time)
        {
            return time.ToUnixTimeSeconds().ToString(
                CultureInfo.InvariantCulture);
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
