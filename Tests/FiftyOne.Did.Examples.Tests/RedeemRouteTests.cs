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

using Examples.Did.CreatorContextWeb;
using FiftyOne.Did.Client;
using FiftyOne.Did.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Did.Examples.Tests
{
    /// <summary>
    /// Tests for the creator context example's /redeem route, against a
    /// recorded transport so nothing here touches the network. The route
    /// must answer in the cloud's own shape with <c>serverSignature</c>
    /// added, and keep the error behaviour the page relies on.
    /// </summary>
    [TestClass]
    public class RedeemRouteTests
    {
        private const string Endpoint = "https://cloud.example.test/api/v4/";

        private static readonly DateTime KeyStart =
            new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc);

        private ECDsa _signer = null!;
        private string _publicPem = null!;
        private FakeHandler _handler = null!;
        private DidClient _client = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            _publicPem = new string(PemEncoding.Write(
                "PUBLIC KEY", _signer.ExportSubjectPublicKeyInfo()));
            _handler = new FakeHandler();
            _client = new DidClient(
                "resource-key", "licence-key", Endpoint,
                new HttpClient(_handler, disposeHandler: false));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _client.Dispose();
            _signer.Dispose();
        }

        /// <summary>A signed 51Did dated inside the key's period.</summary>
        private FodId Signed()
        {
            var payload = new byte[FodId.PayloadLength];
            payload[FodId.FlagsOffset] = 0b0000_0101;
            for (var i = FodId.HashOffset; i < payload.Length; i++)
            {
                payload[i] = (byte)i;
            }
            // The envelope is written by hand and signed directly rather
            // than through the OWID library's Creator, which stamps the
            // current time, because the date has to sit inside the key's
            // period. The layout is the version byte, the zero terminated
            // domain, the little endian minutes since 2020, the little
            // endian payload length, the payload and the signature.
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
            {
                writer.Write((byte)3);
                writer.Write(Encoding.ASCII.GetBytes("51degrees.com"));
                writer.Write((byte)0);
                writer.Write((uint)(KeyStart.AddDays(1) - FodId.DateBase).TotalMinutes);
                writer.Write((uint)payload.Length);
                writer.Write(payload);
            }
            var unsigned = stream.ToArray();
            var signature = _signer.SignData(unsigned, HashAlgorithmName.SHA256);
            stream.Write(signature, 0, signature.Length);
            return new FodId(stream.ToArray());
        }

        private void QueueKeys(string pem)
        {
            _handler.Enqueue(HttpStatusCode.OK, JsonSerializer.Serialize(new[]
            {
                new Dictionary<string, string>
                {
                    ["startsAt"] = KeyStart.ToString("o"),
                    ["publicKey"] = pem,
                },
            }));
        }

        private static JsonElement Parse(RedeemAnswer answer)
            => JsonDocument.Parse(answer.Body).RootElement;

        [TestMethod]
        public async Task Redeem_AnswersInTheCloudShapeWithServerSignature()
        {
            QueueKeys(_publicPem);
            _handler.Enqueue(HttpStatusCode.OK,
                "{\"signature\":\"verified\",\"context\":\"verified\","
                + "\"verifiedAt\":\"2026-08-07T09:15:32Z\",\"secondsSinceVerified\":2}");
            var fodId = Signed();

            var answer = await RedeemRoute.HandleAsync(
                _client, fodId.AsBase64Url(), "sealed", "abc");

            Assert.AreEqual(200, answer.StatusCode);
            Assert.AreEqual("application/json", answer.ContentType);
            var json = Parse(answer);
            Assert.AreEqual("verified", json.GetProperty("signature").GetString());
            Assert.AreEqual("verified", json.GetProperty("context").GetString());
            Assert.AreEqual(
                "2026-08-07T09:15:32Z", json.GetProperty("verifiedAt").GetString());
            Assert.AreEqual(2, json.GetProperty("secondsSinceVerified").GetInt32());
            Assert.AreEqual("verified", json.GetProperty("serverSignature").GetString());
            Assert.IsFalse(json.TryGetProperty("factors", out _));

            // The redeem call carried what the page sent, with the licence
            // key, in the form body.
            var redeem = _handler.Requests[1];
            Assert.AreEqual(HttpMethod.Post, redeem.Method);
            Assert.AreEqual(Endpoint + "id/redeem", redeem.Uri.AbsoluteUri);
            StringAssert.Contains(redeem.Body, "51did=" + fodId.AsBase64Url());
            StringAssert.Contains(redeem.Body, "result=sealed");
            StringAssert.Contains(redeem.Body, "challenge=abc");
            StringAssert.Contains(redeem.Body, "license=licence-key");
        }

        [TestMethod]
        public async Task Redeem_MismatchCarriesFactors()
        {
            QueueKeys(_publicPem);
            _handler.Enqueue(HttpStatusCode.OK,
                "{\"signature\":\"verified\",\"context\":\"mismatch\","
                + "\"factors\":{\"transport\":\"verified\",\"device\":\"mismatch\"},"
                + "\"verifiedAt\":\"2026-08-07T09:15:32Z\",\"secondsSinceVerified\":3}");

            var answer = await RedeemRoute.HandleAsync(
                _client, Signed().AsBase64Url(), "sealed", "abc");

            var json = Parse(answer);
            Assert.AreEqual("mismatch", json.GetProperty("context").GetString());
            var factors = json.GetProperty("factors");
            Assert.AreEqual("verified", factors.GetProperty("transport").GetString());
            Assert.AreEqual("mismatch", factors.GetProperty("device").GetString());
        }

        [TestMethod]
        public async Task Redeem_WrongKey_ServerSignatureInvalid()
        {
            using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            QueueKeys(new string(PemEncoding.Write(
                "PUBLIC KEY", other.ExportSubjectPublicKeyInfo())));
            _handler.Enqueue(HttpStatusCode.OK, "{\"context\":\"replayed\"}");

            var answer = await RedeemRoute.HandleAsync(
                _client, Signed().AsBase64Url(), "sealed", "abc");

            var json = Parse(answer);
            Assert.AreEqual("invalid", json.GetProperty("serverSignature").GetString());
            Assert.AreEqual("replayed", json.GetProperty("context").GetString());
            Assert.IsFalse(json.TryGetProperty("signature", out _));
        }

        [TestMethod]
        public async Task Redeem_Unconfirmed_KeepsTheCloudStatus()
        {
            QueueKeys(_publicPem);
            _handler.Enqueue(HttpStatusCode.ServiceUnavailable, "{\"context\":\"unconfirmed\"}");

            var answer = await RedeemRoute.HandleAsync(
                _client, Signed().AsBase64Url(), "sealed", "abc");

            Assert.AreEqual(503, answer.StatusCode);
            Assert.AreEqual("unconfirmed", Parse(answer).GetProperty("context").GetString());
        }

        [TestMethod]
        public async Task Redeem_HostWithoutTheFeature_Answers404Text()
        {
            QueueKeys(_publicPem);
            _handler.Enqueue(HttpStatusCode.NotFound, "", "text/plain");

            var answer = await RedeemRoute.HandleAsync(
                _client, Signed().AsBase64Url(), "sealed", "abc");

            Assert.AreEqual(404, answer.StatusCode);
            Assert.AreEqual("text/plain", answer.ContentType);
            StringAssert.Contains(answer.Body, "does not support");
        }

        [TestMethod]
        public async Task Redeem_UnreachableCloud_Answers502WithError()
        {
            _handler.EnqueueFailure(new HttpRequestException("no route to host"));

            var answer = await RedeemRoute.HandleAsync(
                _client, Signed().AsBase64Url(), "sealed", "abc");

            Assert.AreEqual(502, answer.StatusCode);
            Assert.AreEqual("application/json", answer.ContentType);
            StringAssert.Contains(
                Parse(answer).GetProperty("error").GetString(), "no route to host");
        }

        [TestMethod]
        public async Task Redeem_MalformedIdentifier_Answers400Errors()
        {
            var answer = await RedeemRoute.HandleAsync(
                _client, "not a 51did", "sealed", "abc");

            Assert.AreEqual(400, answer.StatusCode);
            var errors = Parse(answer).GetProperty("errors");
            Assert.AreEqual(1, errors.GetArrayLength());
            StringAssert.Contains(errors[0].GetString(), "not a valid Base64-encoded 51Did");
            Assert.AreEqual(0, _handler.Requests.Count);
        }

        [TestMethod]
        public async Task Redeem_CloudRefusesIdentifier_Answers400Errors()
        {
            QueueKeys(_publicPem);
            _handler.Enqueue(HttpStatusCode.BadRequest,
                "{\"errors\":[\"'x' is not a valid Base64-encoded 51Did.\"]}");

            var answer = await RedeemRoute.HandleAsync(
                _client, Signed().AsBase64Url(), "sealed", "abc");

            Assert.AreEqual(400, answer.StatusCode);
            StringAssert.Contains(
                Parse(answer).GetProperty("errors")[0].GetString(),
                "not a valid Base64-encoded 51Did");
        }

        /// <summary>
        /// A transport that records requests and answers from a queue.
        /// </summary>
        private sealed class FakeHandler : HttpMessageHandler
        {
            public sealed class Recorded
            {
                public HttpMethod Method { get; init; } = HttpMethod.Get;
                public Uri Uri { get; init; } = new Uri("http://unset/");
                public string? Body { get; init; }
            }

            private readonly Queue<Func<HttpResponseMessage>> _responses =
                new Queue<Func<HttpResponseMessage>>();

            public List<Recorded> Requests { get; } = new List<Recorded>();

            public void Enqueue(
                HttpStatusCode status,
                string body,
                string contentType = "application/json")
            {
                _responses.Enqueue(() => new HttpResponseMessage(status)
                {
                    Content = new StringContent(body, Encoding.UTF8, contentType),
                });
            }

            public void EnqueueFailure(Exception exception)
            {
                _responses.Enqueue(() => throw exception);
            }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                Requests.Add(new Recorded
                {
                    Method = request.Method,
                    Uri = request.RequestUri!,
                    Body = request.Content is null
                        ? null
                        : await request.Content.ReadAsStringAsync(cancellationToken),
                });
                if (_responses.Count == 0)
                {
                    throw new InvalidOperationException(
                        "No response was queued for " + request.RequestUri);
                }
                return _responses.Dequeue()();
            }
        }
    }
}
