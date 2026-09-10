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

using FiftyOne.Pipeline.AgentSignature.Keys;
using System;
using System.Security.Cryptography;
using System.Text;

namespace FiftyOne.Pipeline.AgentSignature.Verification
{
    /// <summary>
    /// Computes the thumbprint of a public key, being the short fingerprint
    /// that the 'keyid' signature parameter carries. RFC 7638 defines the
    /// thumbprint for RSA and EC keys and RFC 8037 Appendix A.3 adds the
    /// OKP keys that Ed25519 uses.
    /// </summary>
    internal static class JwkThumbprint
    {
        /// <summary>
        /// Compute the thumbprint of a key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// The thumbprint as base64url text, or null when the key is not of
        /// a type this element knows how to fingerprint.
        /// </returns>
        public static string Compute(JsonWebKey key)
        {
            var canonical = BuildCanonicalJson(key);
            if (canonical == null)
            {
                return null;
            }
            return ComputeFromCanonicalJson(canonical);
        }

        /// <summary>
        /// Compute the thumbprint of a key given the canonical JSON that
        /// RFC 7638 section 3 step 1 describes.
        /// </summary>
        /// <param name="canonicalJson">
        /// The required members of the key, in lexicographic order of name,
        /// with no whitespace.
        /// </param>
        /// <returns>The thumbprint as base64url text.</returns>
        public static string ComputeFromCanonicalJson(string canonicalJson)
        {
            using (var hash = SHA256.Create())
            {
                return Base64Url.Encode(
                    hash.ComputeHash(Encoding.UTF8.GetBytes(canonicalJson)));
            }
        }

        /// <summary>
        /// Build the canonical JSON for a key, being only the members that
        /// RFC 7638 and RFC 8037 name as required, in lexicographic order of
        /// name and with no whitespace.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// The canonical JSON, or null when the key is not of a type this
        /// element knows how to fingerprint, or is missing a required
        /// member.
        /// </returns>
        public static string BuildCanonicalJson(JsonWebKey key)
        {
            if (key == null)
            {
                return null;
            }
            var builder = new StringBuilder();
            switch (key.KeyType)
            {
                case "OKP":
                    // RFC 8037 Appendix A.3 names crv, kty and x.
                    if (string.IsNullOrEmpty(key.Curve) ||
                        string.IsNullOrEmpty(key.X))
                    {
                        return null;
                    }
                    builder.Append("{");
                    AppendMember(builder, "crv", key.Curve, true);
                    AppendMember(builder, "kty", key.KeyType, false);
                    AppendMember(builder, "x", key.X, false);
                    builder.Append("}");
                    break;
                case "EC":
                    // RFC 7638 section 3.2 names crv, kty, x and y.
                    if (string.IsNullOrEmpty(key.Curve) ||
                        string.IsNullOrEmpty(key.X) ||
                        string.IsNullOrEmpty(key.Y))
                    {
                        return null;
                    }
                    builder.Append("{");
                    AppendMember(builder, "crv", key.Curve, true);
                    AppendMember(builder, "kty", key.KeyType, false);
                    AppendMember(builder, "x", key.X, false);
                    AppendMember(builder, "y", key.Y, false);
                    builder.Append("}");
                    break;
                case "RSA":
                    // RFC 7638 section 3.2 names e, kty and n.
                    if (string.IsNullOrEmpty(key.Exponent) ||
                        string.IsNullOrEmpty(key.Modulus))
                    {
                        return null;
                    }
                    builder.Append("{");
                    AppendMember(builder, "e", key.Exponent, true);
                    AppendMember(builder, "kty", key.KeyType, false);
                    AppendMember(builder, "n", key.Modulus, false);
                    builder.Append("}");
                    break;
                default:
                    return null;
            }
            return builder.ToString();
        }

        private static void AppendMember(
            StringBuilder builder,
            string name,
            string value,
            bool first)
        {
            if (first == false)
            {
                builder.Append(",");
            }
            builder.Append("\"");
            builder.Append(name);
            builder.Append("\":\"");
            AppendEscaped(builder, value);
            builder.Append("\"");
        }

        private static void AppendEscaped(StringBuilder builder, string value)
        {
            foreach (var character in value)
            {
                switch (character)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    default:
                        if (character < ' ')
                        {
                            builder.Append("\\u");
                            builder.Append(((int)character).ToString(
                                "x4",
                                System.Globalization.CultureInfo
                                    .InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }
                        break;
                }
            }
        }
    }
}
