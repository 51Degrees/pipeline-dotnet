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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.AgentSignature.Tests.Helpers
{
    /// <summary>
    /// What a fake handler answers one URL with.
    /// </summary>
    public class FakeResponse
    {
        /// <summary>The status code to answer with.</summary>
        public HttpStatusCode StatusCode { get; set; } =
            HttpStatusCode.OK;

        /// <summary>The body to answer with.</summary>
        public string Body { get; set; } = string.Empty;

        /// <summary>The media type of the body.</summary>
        public string MediaType { get; set; } =
            "application/http-message-signatures-directory+json";

        /// <summary>
        /// Headers to add to the response beyond the content type.
        /// </summary>
        public IDictionary<string, string> Headers { get; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// How long the response asks to be reused for, or null when it
        /// asks for nothing.
        /// </summary>
        public TimeSpan? MaxAge { get; set; }
    }

    /// <summary>
    /// An <see cref="HttpMessageHandler"/> that answers from a table of URLs
    /// rather than from the network, counts what it was asked for, and can
    /// be held open so that a test can see what a request does while a fetch
    /// is still running.
    /// </summary>
    public class FakeHttpHandler : HttpMessageHandler
    {
        private readonly ConcurrentDictionary<string, FakeResponse>
            _responses = new ConcurrentDictionary<string, FakeResponse>(
                StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentBag<string> _requested =
            new ConcurrentBag<string>();
        private readonly ManualResetEventSlim _gate =
            new ManualResetEventSlim(true);
        private int _callCount;

        /// <summary>
        /// The number of requests the handler has been asked to make.
        /// </summary>
        public int CallCount => _callCount;

        /// <summary>
        /// The URLs the handler has been asked for, in no particular order.
        /// </summary>
        public IList<string> RequestedUrls => _requested.ToList();

        /// <summary>
        /// The last 'Accept' header value the handler saw.
        /// </summary>
        public string LastAcceptHeader { get; private set; }

        /// <summary>
        /// The last 'User-Agent' header value the handler saw.
        /// </summary>
        public string LastUserAgentHeader { get; private set; }

        /// <summary>
        /// Answer the given URL with a key directory holding the given keys.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="keysJson">
        /// The keys, each being a JSON object, which are placed in a 'keys'
        /// array.
        /// </param>
        /// <returns>This handler.</returns>
        public FakeHttpHandler AddDirectory(
            string url,
            params string[] keysJson)
        {
            return AddDirectoryWithPurpose(url, null, keysJson);
        }

        /// <summary>
        /// Answer the given URL with a key directory holding the given keys
        /// and a stated purpose.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="purpose">
        /// What the agent says the keys are for, or null to say nothing.
        /// </param>
        /// <param name="keysJson">The keys, each being a JSON object.</param>
        /// <returns>This handler.</returns>
        public FakeHttpHandler AddDirectoryWithPurpose(
            string url,
            string purpose,
            params string[] keysJson)
        {
            var body = new StringBuilder();
            body.Append("{");
            if (purpose != null)
            {
                body.Append("\"purpose\":\"").Append(purpose).Append("\",");
            }
            body.Append("\"keys\":[");
            body.Append(string.Join(",", keysJson));
            body.Append("]}");
            return Add(url, new FakeResponse { Body = body.ToString() });
        }

        /// <summary>
        /// Answer the given URL with the given response.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="response">The response.</param>
        /// <returns>This handler.</returns>
        public FakeHttpHandler Add(string url, FakeResponse response)
        {
            _responses[url] = response;
            return this;
        }

        /// <summary>
        /// Answer the given URL with the given body and media type.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="body">The body.</param>
        /// <param name="mediaType">The media type.</param>
        /// <returns>This handler.</returns>
        public FakeHttpHandler Add(
            string url,
            string body,
            string mediaType)
        {
            return Add(url, new FakeResponse
            {
                Body = body,
                MediaType = mediaType,
            });
        }

        /// <summary>
        /// Answer the given URL with the given status code and no body.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="statusCode">The status code.</param>
        /// <returns>This handler.</returns>
        public FakeHttpHandler AddStatus(
            string url,
            HttpStatusCode statusCode)
        {
            return Add(url, new FakeResponse
            {
                StatusCode = statusCode,
                Body = string.Empty,
            });
        }

        /// <summary>
        /// Remove the answer for the given URL, so that later requests for
        /// it fail as though the agent had gone away.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <returns>This handler.</returns>
        public FakeHttpHandler Remove(string url)
        {
            _responses.TryRemove(url, out _);
            return this;
        }

        /// <summary>
        /// Hold every request open until <see cref="Release"/> is called, so
        /// that a test can see what a request does while a fetch is still
        /// running.
        /// </summary>
        public void Hold()
        {
            _gate.Reset();
        }

        /// <summary>
        /// Let the held requests finish.
        /// </summary>
        public void Release()
        {
            _gate.Set();
        }

        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            var url = request.RequestUri.AbsoluteUri;
            _requested.Add(url);
            if (request.Headers.TryGetValues("Accept", out var accept))
            {
                LastAcceptHeader = string.Join(", ", accept);
            }
            if (request.Headers.TryGetValues("User-Agent", out var agent))
            {
                LastUserAgentHeader = string.Join(", ", agent);
            }

            if (_gate.IsSet == false)
            {
                await Task.Run(
                    () => _gate.Wait(cancellationToken),
                    cancellationToken).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();

            if (_responses.TryGetValue(url, out var configured) == false)
            {
                throw new HttpRequestException(
                    "The test handler was not told what to answer '" +
                    url + "' with.");
            }

            var message = new HttpResponseMessage(configured.StatusCode)
            {
                Content = new StringContent(
                    configured.Body, Encoding.UTF8),
                RequestMessage = request,
            };
            message.Content.Headers.ContentType =
                new MediaTypeHeaderValue(configured.MediaType)
                {
                    CharSet = "utf-8",
                };
            if (configured.MaxAge.HasValue)
            {
                message.Headers.CacheControl = new CacheControlHeaderValue
                {
                    MaxAge = configured.MaxAge.Value,
                };
            }
            foreach (var header in configured.Headers)
            {
                message.Headers.TryAddWithoutValidation(
                    header.Key, header.Value);
            }
            return message;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _gate.Set();
                _gate.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
