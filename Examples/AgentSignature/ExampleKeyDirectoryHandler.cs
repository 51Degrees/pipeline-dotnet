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
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Examples.AgentSignature
{
    /// <summary>
    /// Answers the element's key directory fetches from a table held in
    /// memory rather than from the network, so that this example prints the
    /// same thing every time it runs and needs nothing to be reachable.
    /// </summary>
    /// <remarks>
    /// A real deployment does not do this. The element makes an ordinary
    /// <see cref="HttpClient"/> of its own and fetches each agent's key
    /// directory over HTTPS, holding what it reads in a cache so that a
    /// burst of requests from one agent causes one fetch.
    /// </remarks>
    public class ExampleKeyDirectoryHandler : HttpMessageHandler
    {
        private readonly IDictionary<string, string> _directories =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Answer the given URL with a key directory holding the given
        /// public keys.
        /// </summary>
        /// <param name="url">
        /// The URL the directory is served from, which is the agent's origin
        /// followed by the well known path the protocol reserves.
        /// </param>
        /// <param name="purpose">
        /// What the agent says it uses the pages it fetches for, or null to
        /// say nothing.
        /// </param>
        /// <param name="keysJson">
        /// The public keys, each being a JSON Web Key.
        /// </param>
        /// <returns>This handler.</returns>
        public ExampleKeyDirectoryHandler AddDirectory(
            string url,
            string purpose,
            params string[] keysJson)
        {
            var body = new StringBuilder("{");
            if (purpose != null)
            {
                body.Append("\"purpose\":\"").Append(purpose).Append("\",");
            }
            body.Append("\"keys\":[")
                .Append(string.Join(",", keysJson))
                .Append("]}");
            _directories[url] = body.ToString();
            return this;
        }

        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            cancellationToken.ThrowIfCancellationRequested();

            var url = request.RequestUri.AbsoluteUri;
            if (_directories.TryGetValue(url, out var body) == false)
            {
                // An agent that publishes no keys looks exactly like this,
                // which the element reports as Unverified rather than as
                // evidence against the agent.
                throw new HttpRequestException(
                    "This example serves no key directory at '" + url + "'.");
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8),
                RequestMessage = request,
            };
            response.Content.Headers.ContentType =
                new MediaTypeHeaderValue(Constants.DIRECTORY_MEDIA_TYPE)
                {
                    CharSet = "utf-8",
                };
            return Task.FromResult(response);
        }
    }
}
