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
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Did.Tests
{
    /// <summary>
    /// An HTTP transport for the client tests that records every request
    /// it is given and answers from a queue, so no test touches the
    /// network. The request body is read at once, because the client
    /// disposes the request after sending it.
    /// </summary>
    internal sealed class FakeHttpHandler : HttpMessageHandler
    {
        /// <summary>One request as the handler saw it.</summary>
        public sealed class Recorded
        {
            public HttpMethod Method { get; init; } = HttpMethod.Get;
            public Uri Uri { get; init; } = new Uri("http://unset/");
            public string? Body { get; init; }
            public string? ContentType { get; init; }
            public string? UserAgent { get; init; }
        }

        private readonly Queue<Func<Recorded, HttpResponseMessage>> _responses =
            new Queue<Func<Recorded, HttpResponseMessage>>();

        /// <summary>Every request, in the order received.</summary>
        public List<Recorded> Requests { get; } = new List<Recorded>();

        /// <summary>A client that sends through this handler.</summary>
        public HttpClient Client => new HttpClient(this, disposeHandler: false);

        /// <summary>Queue a response with the given status and body.</summary>
        public void Enqueue(
            HttpStatusCode status,
            string body,
            string contentType = "application/json")
        {
            _responses.Enqueue(_ => new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, contentType),
            });
        }

        /// <summary>Queue a transport failure.</summary>
        public void EnqueueFailure(Exception exception)
        {
            _responses.Enqueue(_ => throw exception);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var recorded = new Recorded
            {
                Method = request.Method,
                Uri = request.RequestUri!,
                Body = request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync(cancellationToken),
                ContentType = request.Content?.Headers.ContentType?.MediaType,
                UserAgent = request.Headers.TryGetValues("User-Agent", out var agent)
                    ? string.Join(" ", agent)
                    : null,
            };
            Requests.Add(recorded);
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException(
                    "No response was queued for " + request.RequestUri);
            }
            return _responses.Dequeue()(recorded);
        }
    }
}
