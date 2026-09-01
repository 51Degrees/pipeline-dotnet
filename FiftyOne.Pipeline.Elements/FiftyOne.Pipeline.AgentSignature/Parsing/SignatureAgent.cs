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
using System.Text;

namespace FiftyOne.Pipeline.AgentSignature.Parsing
{
    /// <summary>
    /// One member of the 'Signature-Agent' header, being a URI that says
    /// where the agent publishes the key it signed with.
    /// </summary>
    internal sealed class SignatureAgentEntry
    {
        /// <summary>
        /// The label of the member, which the covered component
        /// '"signature-agent";key="&lt;label&gt;"' names. The bare quoted
        /// string form has no label, so this is empty for it.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// The URI exactly as the agent sent it, which is the value the
        /// AgentSignatureAgent property returns.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// The member type, one of 'directory', 'jwks_uri' and 'cimd'.
        /// </summary>
        public string Type { get; }

        /// <summary>
        /// The URL the keys are fetched from, which is also the key the
        /// directory cache uses. Null when the member carried the directory
        /// inline in a 'data:' URI.
        /// </summary>
        public string KeyUrl { get; }

        /// <summary>
        /// The bytes of a directory carried inline in a 'data:' URI, or
        /// null when the keys are fetched.
        /// </summary>
        public byte[] InlineDirectory { get; }

        private SignatureAgentEntry(
            string label,
            string value,
            string type,
            string keyUrl,
            byte[] inlineDirectory)
        {
            Label = label;
            Value = value;
            Type = type;
            KeyUrl = keyUrl;
            InlineDirectory = inlineDirectory;
        }

        /// <summary>
        /// Parse the 'Signature-Agent' header.
        /// </summary>
        /// <param name="header">The header value.</param>
        /// <param name="allowLegacyForm">
        /// True when the bare quoted string form, which carries no label, is
        /// accepted.
        /// </param>
        /// <param name="entries">The members parsed.</param>
        /// <returns>
        /// False when the header could not be read, which the element
        /// reports as Malformed.
        /// </returns>
        public static bool TryParse(
            string header,
            bool allowLegacyForm,
            out IList<SignatureAgentEntry> entries)
        {
            entries = null;
            if (header == null)
            {
                return false;
            }

            var result = new List<SignatureAgentEntry>();
            var trimmed = header.Trim();
            if (trimmed.Length > 0 && trimmed[0] == '"')
            {
                // The bare quoted string form that the earlier drafts used.
                if (allowLegacyForm == false)
                {
                    return false;
                }
                if (StructuredFieldParser.TryParseItem(trimmed, out var item)
                    == false)
                {
                    return false;
                }
                if (TryCreate(string.Empty, item, out var legacyEntry)
                    == false)
                {
                    return false;
                }
                result.Add(legacyEntry);
                entries = result;
                return true;
            }

            if (StructuredFieldParser.TryParseDictionary(
                header, out var dictionary) == false)
            {
                return false;
            }
            foreach (var member in dictionary.Members)
            {
                if (member.Value.IsInnerList)
                {
                    return false;
                }
                if (TryCreate(
                    member.Key, member.Value.Item, out var entry) == false)
                {
                    return false;
                }
                result.Add(entry);
            }

            entries = result;
            return true;
        }

        private static bool TryCreate(
            string label,
            SfItem item,
            out SignatureAgentEntry entry)
        {
            entry = null;
            if ((item.Value is string value) == false)
            {
                return false;
            }

            var type = item.GetStringParameter("type") ??
                Constants.AGENT_TYPE_DIRECTORY;
            if (string.Equals(
                    type, Constants.AGENT_TYPE_DIRECTORY,
                    StringComparison.Ordinal) == false &&
                string.Equals(
                    type, Constants.AGENT_TYPE_JWKS_URI,
                    StringComparison.Ordinal) == false &&
                string.Equals(
                    type, Constants.AGENT_TYPE_CIMD,
                    StringComparison.Ordinal) == false)
            {
                return false;
            }

            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) == false)
            {
                return false;
            }

            // A directory may be carried inline in a 'data:' URI, which
            // needs no fetch at all.
            if (string.Equals(
                uri.Scheme, "data", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(
                        type, Constants.AGENT_TYPE_DIRECTORY,
                        StringComparison.Ordinal) == false ||
                    TryDecodeDataUri(value, out var inline) == false)
                {
                    return false;
                }
                entry = new SignatureAgentEntry(
                    label, value, type, null, inline);
                return true;
            }

            // A key fetched over plain HTTP proves nothing about the agent,
            // so this element requires HTTPS even though the draft allows
            // HTTP.
            if (string.Equals(
                uri.Scheme, Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase) == false)
            {
                return false;
            }

            string keyUrl;
            if (string.Equals(
                type, Constants.AGENT_TYPE_DIRECTORY,
                StringComparison.Ordinal))
            {
                keyUrl = value.TrimEnd('/') + Constants.DIRECTORY_PATH;
            }
            else
            {
                keyUrl = value;
            }

            entry = new SignatureAgentEntry(label, value, type, keyUrl, null);
            return true;
        }

        private static bool TryDecodeDataUri(string value, out byte[] data)
        {
            data = null;
            var comma = value.IndexOf(',');
            if (comma < 0)
            {
                return false;
            }
            var metadata = value.Substring(5, comma - 5);
            var payload = value.Substring(comma + 1);
            var isBase64 = metadata.EndsWith(
                ";base64", StringComparison.OrdinalIgnoreCase);
            if (isBase64)
            {
                metadata = metadata.Substring(
                    0, metadata.Length - ";base64".Length);
            }
            // An empty media type means 'text/plain', which is not a key
            // directory, so the media type has to be stated and has to be
            // the directory one.
            if (metadata.StartsWith(
                Constants.DIRECTORY_MEDIA_TYPE,
                StringComparison.OrdinalIgnoreCase) == false)
            {
                return false;
            }
            try
            {
                data = isBase64
                    ? Convert.FromBase64String(payload)
                    : Encoding.UTF8.GetBytes(Uri.UnescapeDataString(payload));
            }
            catch (FormatException)
            {
                return false;
            }
            return true;
        }
    }
}
