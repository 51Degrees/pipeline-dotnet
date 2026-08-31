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

using FiftyOne.Did.Client;
using FiftyOne.Did.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using static FiftyOne.Did.Tests.FodIdTestFactory;

namespace FiftyOne.Did.Tests
{
    /// <summary>
    /// Tests that <see cref="DidClient"/> refuses a malformed identifier
    /// before any key is fetched or any call is made, that the length
    /// guard is the client's own check and separate from the parser, and
    /// that a signature which does not match is told apart from a key that
    /// could not be obtained.
    /// </summary>
    [TestClass]
    public class DidClientMalformedInputTests
    {
        private const string Resource = "AQAAAresourcekey";
        private const string Endpoint = "https://cloud.example.test/api/v4/";

        /// <summary>A Monday, the start of one key period.</summary>
        private static readonly DateTime T0 =
            new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>The start of the next key period.</summary>
        private static readonly DateTime T1 = T0.AddDays(7);

        private FodIdTestFactory _factory = null!;
        private FakeHttpHandler _handler = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _factory = new FodIdTestFactory();
            _handler = new FakeHttpHandler();
        }

        private DidClient NewClient() =>
            new DidClient(Resource, null, Endpoint, _handler.Client);

        private static string KeysJson(
            params (DateTime Start, string Pem)[] keys)
            => JsonSerializer.Serialize(keys.Select(key =>
                new Dictionary<string, string>
                {
                    ["startsAt"] = key.Start.ToString("o"),
                    ["publicKey"] = key.Pem,
                }).ToArray());

        /// <summary>
        /// One malformed value of each kind the parser can refuse, each
        /// paired with the status the client's message must name.
        /// </summary>
        private IEnumerable<(string Value, FodIdParseStatus Status)> Malformed()
        {
            yield return ("not base64 in any alphabet!", FodIdParseStatus.InvalidBase64);
            var shortRandom = CanonicalRandomPayload()
                .Take(FodId.RandomPayloadLength - 1).ToArray();
            yield return (
                _factory.SignedOwidBase64(shortRandom),
                FodIdParseStatus.InvalidTypePayloadLength);
            yield return (
                _factory.SignedOwidBase64(new byte[FodId.HeaderLength - 1]),
                FodIdParseStatus.PayloadTooShort);
            var trailing = _factory.SignedBytes(CanonicalPayload(), T0)
                .Concat(new byte[] { 0 }).ToArray();
            yield return (
                Convert.ToBase64String(trailing),
                FodIdParseStatus.ByteCountMismatch);
            yield return (
                Convert.ToBase64String(new byte[] { 9, 1, 2 }),
                FodIdParseStatus.UnsupportedVersion);
        }

        [TestMethod]
        public async Task Verify_MalformedString_IsRefusedBeforeAnyCall()
        {
            using var client = NewClient();
            foreach (var (value, status) in Malformed())
            {
                var error = await Assert.ThrowsExactlyAsync<ArgumentException>(
                    () => client.VerifyAsync(value), status.ToString());

                Assert.AreEqual("fodId", error.ParamName);
                StringAssert.Contains(error.Message, status.ToString());
            }
            Assert.AreEqual(0, _handler.Requests.Count, "no key fetch and no call");
        }

        [TestMethod]
        public async Task Redeem_MalformedString_IsRefusedBeforeAnyCall()
        {
            using var client = NewClient();
            foreach (var (value, status) in Malformed())
            {
                var error = await Assert.ThrowsExactlyAsync<ArgumentException>(
                    () => client.RedeemAsync(value, "sealed", "abc"),
                    status.ToString());

                Assert.AreEqual("fodId", error.ParamName);
                StringAssert.Contains(error.Message, status.ToString());
            }
            Assert.AreEqual(0, _handler.Requests.Count, "no key fetch and no call");
        }

        [TestMethod]
        public async Task Verify_WellFormedString_ReachesTheCloudOnce()
        {
            // The local check lets a well formed identifier through, the
            // signature being the cloud's question and not the parser's,
            // and an identifier the cloud finds unsigned is still a plain
            // false.
            _handler.Enqueue(HttpStatusCode.OK, "{\"valid\":false}");
            using var client = NewClient();
            var bytes = _factory.SignedBytes(CanonicalPayload(), T0);
            bytes[bytes.Length - 1] ^= 0xFF;

            var valid = await client.VerifyAsync(
                FodId.ToBase64Url(Convert.ToBase64String(bytes)));

            Assert.IsFalse(valid);
            Assert.AreEqual(1, _handler.Requests.Count);
        }

        [TestMethod]
        public async Task Guard_IsTheClientsOwnCheckAndNotTheParser()
        {
            using var client = NewClient();

            // Beyond the guard, the client answers with its own wording
            // and the parser is never asked.
            var beyond = await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => client.VerifyAsync(new string('A', 4097)));
            StringAssert.Contains(beyond.Message, "too long");

            // Within the guard, the same character repeated is valid
            // base64 for bytes that are not an envelope, so the refusal
            // comes from the parser and names a parse status instead.
            var within = await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => client.VerifyAsync(new string('A', 4096)));
            Assert.IsFalse(within.Message.Contains("too long"));
            StringAssert.Contains(
                within.Message, nameof(FodIdParseStatus.AbsentNode));

            Assert.AreEqual(0, _handler.Requests.Count);
        }

        [TestMethod]
        public async Task VerifySignature_AlteredSignature_IsInvalidNotAnError()
        {
            _handler.Enqueue(
                HttpStatusCode.OK, KeysJson((T0, _factory.PublicPem), (T1, "pem1")));
            using var client = NewClient();
            var bytes = _factory.SignedBytes(CanonicalPayload(), T0.AddDays(1));
            bytes[bytes.Length - 1] ^= 0xFF;
            var fodId = new FodId(bytes);

            Assert.AreEqual(
                SignatureCheck.Invalid,
                await client.VerifySignatureDetailedAsync(fodId));
            Assert.IsFalse(await client.VerifySignatureAsync(fodId));
        }

        [TestMethod]
        public async Task VerifySignature_KeyFetchFails_IsAnErrorNotInvalid()
        {
            // A key that cannot be obtained leaves the signature unjudged,
            // so the client throws rather than reporting a forgery.
            _handler.Enqueue(HttpStatusCode.ServiceUnavailable, "down");
            using var client = NewClient();
            var fodId = new FodId(
                _factory.SignedBytes(CanonicalPayload(), T0.AddDays(1)));

            var error = await Assert.ThrowsExactlyAsync<HttpRequestException>(
                () => client.VerifySignatureDetailedAsync(fodId));

            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        }

        [TestMethod]
        public async Task VerifySignature_NoKeyForDate_IsNotInvalid()
        {
            // The schedule starts after the identifier's date, so no key
            // covers it. The answer says so rather than calling the
            // signature bad.
            var keys = KeysJson((T1, _factory.PublicPem), (T1.AddDays(7), "pem2"));
            _handler.Enqueue(HttpStatusCode.OK, keys);
            _handler.Enqueue(HttpStatusCode.OK, keys);
            using var client = NewClient();
            var fodId = new FodId(
                _factory.SignedBytes(CanonicalPayload(), T0.AddDays(1)));

            Assert.AreEqual(
                SignatureCheck.NoKeyForDate,
                await client.VerifySignatureDetailedAsync(fodId));
        }
    }
}
