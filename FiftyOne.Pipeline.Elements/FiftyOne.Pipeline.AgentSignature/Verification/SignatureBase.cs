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

using FiftyOne.Pipeline.AgentSignature.Parsing;
using System.Collections.Generic;
using System.Text;

namespace FiftyOne.Pipeline.AgentSignature.Verification
{
    /// <summary>
    /// Supplies the value of one covered component to the signature base
    /// builder. The element resolves components from the evidence in a flow
    /// data, whilst the key directory fetcher resolves them from the
    /// response it received.
    /// </summary>
    internal interface IComponentResolver
    {
        /// <summary>
        /// Find the value of one covered component.
        /// </summary>
        /// <param name="name">
        /// The component name in lower case, for example '@authority' or
        /// 'signature-agent'.
        /// </param>
        /// <param name="component">
        /// The component as the signer wrote it, so that its parameters,
        /// such as 'key' and 'req', can be read.
        /// </param>
        /// <param name="value">The value found.</param>
        /// <returns>
        /// False when the value cannot be rebuilt, which the element
        /// reports as Unverified with the ComponentUnavailable reason.
        /// </returns>
        bool TryResolve(string name, SfItem component, out string value);
    }

    /// <summary>
    /// Builds the signature base, being the exact text that the agent
    /// signed. RFC 9421 section 2.5 defines it as one line per covered
    /// component followed by a line holding the signature parameters.
    /// </summary>
    internal static class SignatureBase
    {
        /// <summary>
        /// The name of the line that carries the signature parameters,
        /// which is always the last line of the signature base.
        /// </summary>
        public const string SignatureParamsName = "@signature-params";

        /// <summary>
        /// Build the signature base.
        /// </summary>
        /// <param name="components">
        /// The covered components in the order the signer listed them.
        /// </param>
        /// <param name="signatureParams">
        /// The signature parameters, being the strict serialisation of the
        /// member value of the 'Signature-Input' header.
        /// </param>
        /// <param name="resolver">
        /// The source of the component values.
        /// </param>
        /// <param name="text">The signature base.</param>
        /// <returns>
        /// False when one of the covered components could not be rebuilt.
        /// </returns>
        public static bool TryBuild(
            IList<SfItem> components,
            string signatureParams,
            IComponentResolver resolver,
            out string text)
        {
            text = null;
            var builder = new StringBuilder();
            var seen = new HashSet<string>();
            foreach (var component in components)
            {
                if ((component.Value is string name) == false)
                {
                    // RFC 9421 section 2.1 requires every component
                    // identifier to be a string.
                    return false;
                }
                // RFC 9421 section 2.5 writes each component identifier
                // in its strict serialisation, so the line starts the way
                // a compliant signer wrote it whatever legal spelling the
                // header used.
                var identifier =
                    StructuredFieldSerializer.Serialize(component);
                // A component listed twice makes the base ambiguous, which
                // RFC 9421 section 2.5 forbids. Comparing the serialised
                // identifiers catches two spellings of one identifier as
                // well as two identical ones.
                if (seen.Add(identifier) == false)
                {
                    return false;
                }
                if (resolver.TryResolve(name, component, out var value)
                    == false)
                {
                    return false;
                }
                builder.Append(identifier);
                builder.Append(": ");
                builder.Append(value);
                builder.Append("\n");
            }
            builder.Append("\"");
            builder.Append(SignatureParamsName);
            builder.Append("\": ");
            builder.Append(signatureParams);
            text = builder.ToString();
            return true;
        }

        /// <summary>
        /// Build the '@authority' derived component from the value of the
        /// Host header, as RFC 9421 section 2.2.3 describes. The host is
        /// lower cased and a port that is the default for the scheme is
        /// removed.
        /// </summary>
        /// <param name="host">The value of the Host header.</param>
        /// <param name="scheme">
        /// The scheme of the request, used only to decide which port is the
        /// default one. May be null.
        /// </param>
        /// <returns>The authority.</returns>
        public static string BuildAuthority(string host, string scheme)
        {
            if (string.IsNullOrEmpty(host))
            {
                return null;
            }
            var authority = host.Trim().ToLowerInvariant();
            var defaultPort =
                string.Equals(scheme, "http", System.StringComparison.Ordinal)
                    ? ":80"
                    : ":443";
            // An IPv6 host is written in square brackets and may carry a
            // port after the closing bracket, so only look for the port
            // separator after that bracket.
            var searchFrom = authority.LastIndexOf(']');
            var separator = authority.IndexOf(
                ':', searchFrom < 0 ? 0 : searchFrom);
            if (separator >= 0 &&
                authority.Substring(separator) == defaultPort)
            {
                authority = authority.Substring(0, separator);
            }
            return authority;
        }
    }
}
