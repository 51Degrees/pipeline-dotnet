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

namespace FiftyOne.Pipeline.AgentSignature.Keys
{
    /// <summary>
    /// A Signature Agent Card, which section 3 of the registry draft
    /// defines. The card is a JSON document in which an agent says who it
    /// is, what it is for and where its keys are.
    /// </summary>
    internal sealed class AgentCard
    {
        /// <summary>
        /// The identifier of the agent, which the registry draft requires
        /// to be the URL the card itself was fetched from.
        /// </summary>
        public string ClientId { get; private set; }

        /// <summary>
        /// The name the agent gives itself, for example 'Example Bot'.
        /// </summary>
        public string ClientName { get; private set; }

        /// <summary>
        /// A page describing the agent.
        /// </summary>
        public string ClientUri { get; private set; }

        /// <summary>
        /// Ways of reaching whoever runs the agent.
        /// </summary>
        public IList<string> Contacts { get; private set; }

        /// <summary>
        /// The URL of the agent's keys, or null when the card carries the
        /// keys inline instead.
        /// </summary>
        public string JwksUri { get; private set; }

        /// <summary>
        /// The agent's keys carried inline, or null when the card names a
        /// URL for them instead.
        /// </summary>
        public KeyDirectory Jwks { get; private set; }

        /// <summary>
        /// The robots.txt product token, being the name the agent answers to
        /// in a robots.txt file. A customer can join this to the
        /// CrawlerProductTokens property.
        /// </summary>
        public string ProductToken { get; private set; }

        /// <summary>
        /// What the agent uses the pages it fetches for.
        /// </summary>
        public string Purpose { get; private set; }

        /// <summary>
        /// What causes the agent to make a request, for example 'fetcher'.
        /// </summary>
        public string Trigger { get; private set; }

        /// <summary>
        /// The User-Agent header the agent says it sends.
        /// </summary>
        public string ExpectedUserAgent { get; private set; }

        /// <summary>
        /// Read a card from its JSON and check it against the URL it came
        /// from.
        /// </summary>
        /// <param name="json">The card document.</param>
        /// <param name="cardUrl">
        /// The URL the card was fetched from, which the 'client_id' field
        /// has to match.
        /// </param>
        /// <param name="card">The card read.</param>
        /// <returns>
        /// False when the document could not be read, when 'client_id' does
        /// not match the URL, or when the card carries both 'jwks' and
        /// 'jwks_uri', which the registry draft forbids.
        /// </returns>
        public static bool TryParse(
            string json,
            string cardUrl,
            out AgentCard card)
        {
            card = null;
            if (JsonReader.TryParseObject(json, out var root) == false)
            {
                return false;
            }

            var clientId = JsonReader.GetString(root, "client_id");
            if (string.IsNullOrEmpty(clientId) ||
                string.Equals(
                    clientId, cardUrl, StringComparison.Ordinal) == false)
            {
                return false;
            }

            var jwksUri = JsonReader.GetString(root, "jwks_uri");
            var jwksObject = JsonReader.GetObject(root, "jwks");
            if (string.IsNullOrEmpty(jwksUri) == false && jwksObject != null)
            {
                return false;
            }

            KeyDirectory jwks = null;
            if (jwksObject != null &&
                KeyDirectory.TryParse(jwksObject, out jwks) == false)
            {
                return false;
            }

            var result = new AgentCard
            {
                ClientId = clientId,
                ClientName = JsonReader.GetString(root, "client_name"),
                ClientUri = JsonReader.GetString(root, "client_uri"),
                JwksUri = jwksUri,
                Jwks = jwks,
            };

            var contacts = JsonReader.GetArray(root, "contacts");
            if (contacts != null)
            {
                var list = new List<string>(contacts.Count);
                foreach (var contact in contacts)
                {
                    if (contact is string text)
                    {
                        list.Add(text);
                    }
                }
                result.Contacts = list;
            }

            var webBotAuth = JsonReader.GetObject(root, "web_bot_auth");
            if (webBotAuth != null)
            {
                result.ProductToken = JsonReader.GetString(
                    webBotAuth, "rfc9309-product-token");
                result.Purpose = JsonReader.GetString(webBotAuth, "purpose");
                result.Trigger = JsonReader.GetString(webBotAuth, "trigger");
                result.ExpectedUserAgent = JsonReader.GetString(
                    webBotAuth, "expected-user-agent");
            }

            card = result;
            return true;
        }
    }
}
