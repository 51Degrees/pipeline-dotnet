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
using Owid.Client;
using Owid.Client.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using static FiftyOne.Did.Tests.FodIdTestFactory;

namespace FiftyOne.Did.Tests
{
    /// <summary>
    /// Tests for <see cref="DidClient"/> against a recorded transport, so
    /// nothing here touches the network.
    /// </summary>
    [TestClass]
    public class DidClientTests
    {
        private const string Resource = "AQAAAresourcekey";
        private const string Licence = "AQAAAlicencekey";
        private const string Endpoint = "https://cloud.example.test/api/v4/";

        /// <summary>A Monday, the start of one key period.</summary>
        private static readonly DateTime T0 =
            new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>The start of the next key period.</summary>
        private static readonly DateTime T1 = T0.AddDays(7);

        private FodIdTestFactory _factory = null!;
        private FakeHttpHandler _handler = null!;
        private FakeTimeProvider _time = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _factory = new FodIdTestFactory();
            _handler = new FakeHttpHandler();
            _time = new FakeTimeProvider(T0.AddDays(1));
        }

        private DidClient NewClient(string? licence = Licence) =>
            new DidClient(Resource, licence, Endpoint, _handler.Client, _time);

        private static string KeysJson(
            params (DateTime Start, string Pem)[] keys)
            => JsonSerializer.Serialize(keys.Select(key =>
                new Dictionary<string, string>
                {
                    ["startsAt"] = key.Start.ToString("o"),
                    ["weekStart"] = key.Start.ToString("o"),
                    ["created"] = key.Start.AddDays(-60).ToString("o"),
                    ["publicKey"] = key.Pem,
                }).ToArray());

        private FodId SignedAt(
            DateTime date,
            byte[]? payload = null,
            string domain = TestDomain) =>
            new FodId(_factory.SignedOwid(
                payload ?? CanonicalPayload(),
                date,
                OwidVersion.Version3,
                domain));

        // A payload with a creator context section after the value. The
        // section's length belongs to the cloud, so this is simply longer
        // than the base and nothing here depends on how much longer.
        private static byte[] PayloadWithContext()
        {
            var payload = new byte[FodId.PayloadLength + 128];
            CanonicalPayload().CopyTo(payload, 0);
            return payload;
        }

        private static IReadOnlyList<DidPublicKey> Schedule() =>
            new[]
            {
                new DidPublicKey(T0, "pem0"),
                new DidPublicKey(T1, "pem1"),
            };

        // ----------------------------------------------------------------
        // Construction and endpoint
        // ----------------------------------------------------------------

        [TestMethod]
        public void Constructor_EmptyResourceKey_Throws()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new DidClient(" ", null, Endpoint, _handler.Client));
        }

        [TestMethod]
        public void Endpoint_TrailingSlashIsNormalised()
        {
            using var without = new DidClient(
                Resource, null, "https://x.example/api/v4", _handler.Client);
            using var several = new DidClient(
                Resource, null, "https://x.example/api/v4///", _handler.Client);

            Assert.AreEqual("https://x.example/api/v4/", without.Endpoint);
            Assert.AreEqual("https://x.example/api/v4/", several.Endpoint);
        }

        [TestMethod]
        public void Endpoint_ReadFromEnvironmentWhenAbsent()
        {
            var previous = Environment.GetEnvironmentVariable(
                DidClient.EndpointEnvironmentVariable);
            try
            {
                Environment.SetEnvironmentVariable(
                    DidClient.EndpointEnvironmentVariable,
                    "https://private.example/api/v4");
                using var fromEnvironment = new DidClient(
                    Resource, null, null, _handler.Client);
                Assert.AreEqual(
                    "https://private.example/api/v4/",
                    fromEnvironment.Endpoint);

                Environment.SetEnvironmentVariable(
                    DidClient.EndpointEnvironmentVariable, null);
                using var fromDefault = new DidClient(
                    Resource, null, null, _handler.Client);
                Assert.AreEqual(DidClient.DefaultEndpoint, fromDefault.Endpoint);
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    DidClient.EndpointEnvironmentVariable, previous);
            }
        }

        [TestMethod]
        public void Endpoint_NotAbsolute_Throws()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new DidClient(Resource, null, "api/v4", _handler.Client));
        }

        [TestMethod]
        public void HasLicenceKey_ReflectsConstructor()
        {
            using var with = NewClient();
            using var without = NewClient(null);
            using var empty = NewClient(string.Empty);

            Assert.IsTrue(with.HasLicenceKey);
            Assert.IsFalse(without.HasLicenceKey);
            Assert.IsFalse(empty.HasLicenceKey);
        }

        // ----------------------------------------------------------------
        // Key list and cache
        // ----------------------------------------------------------------

        [TestMethod]
        public async Task PublicKeys_ReadsStartsAtAndPublicKey()
        {
            _handler.Enqueue(HttpStatusCode.OK, KeysJson(
                (T1, "pem1"), (T0, "pem0")));
            using var client = NewClient();

            var keys = await client.PublicKeysAsync();

            Assert.AreEqual(2, keys.Count);
            // Sorted by start whatever order the cloud sent.
            Assert.AreEqual(T0, keys[0].StartsAt);
            Assert.AreEqual(DateTimeKind.Utc, keys[0].StartsAt.Kind);
            Assert.AreEqual("pem0", keys[0].PublicKeyPem);
            Assert.AreEqual(T1, keys[1].StartsAt);
            Assert.AreEqual("pem1", keys[1].PublicKeyPem);

            var request = _handler.Requests.Single();
            Assert.AreEqual(HttpMethod.Get, request.Method);
            Assert.AreEqual(Endpoint + "id/key/" + Resource, request.Uri.AbsoluteUri);
            Assert.AreEqual(DidClient.UserAgent, request.UserAgent);
            StringAssert.StartsWith(request.UserAgent, "FiftyOne.Did/");
        }

        [TestMethod]
        public async Task PublicKeys_FallsBackToCreated()
        {
            // The shape the cloud emitted before startsAt existed.
            var created = T0.AddDays(-3);
            _handler.Enqueue(HttpStatusCode.OK, JsonSerializer.Serialize(new[]
            {
                new Dictionary<string, string>
                {
                    ["created"] = created.ToString("o"),
                    ["publicKey"] = "pem0",
                },
            }));
            using var client = NewClient();

            var keys = await client.PublicKeysAsync();

            Assert.AreEqual(1, keys.Count);
            Assert.AreEqual(created, keys[0].StartsAt);
        }

        [TestMethod]
        public async Task PublicKeys_SecondCallAnswersFromCache()
        {
            _handler.Enqueue(HttpStatusCode.OK, KeysJson((T0, "pem0")));
            using var client = NewClient();

            var first = await client.PublicKeysAsync();
            var second = await client.PublicKeysAsync();

            Assert.AreSame(first, second);
            Assert.AreEqual(1, _handler.Requests.Count);
        }

        [TestMethod]
        public async Task PublicKeys_NonSuccess_ThrowsWithStatus()
        {
            _handler.Enqueue(HttpStatusCode.Unauthorized, "{\"errors\":[\"bad key\"]}");
            using var client = NewClient();

            var error = await Assert.ThrowsExactlyAsync<HttpRequestException>(
                () => client.PublicKeysAsync());

            Assert.AreEqual(HttpStatusCode.Unauthorized, error.StatusCode);
            StringAssert.Contains(error.Message, "bad key");
        }

        [TestMethod]
        public void ParseKeys_NotAnArray_Throws()
        {
            Assert.ThrowsExactly<FormatException>(
                () => DidClient.ParseKeys("{\"errors\":[\"x\"]}"));
        }

        [TestMethod]
        public void ParseKeys_EntryWithoutPublicKey_Throws()
        {
            Assert.ThrowsExactly<FormatException>(
                () => DidClient.ParseKeys("[{\"startsAt\":\"2026-08-03T00:00:00Z\"}]"));
        }

        [TestMethod]
        public async Task PublicKeyFor_AnswersFromCacheOtherwise()
        {
            // The newest start is in the future, as it is on the cloud,
            // which publishes ahead, so nothing prompts a fetch.
            _handler.Enqueue(HttpStatusCode.OK, KeysJson((T0, "pem0"), (T1, "pem1")));
            using var client = NewClient();
            var fodId = SignedAt(T0.AddDays(1));

            var first = await client.PublicKeyForAsync(fodId);
            var second = await client.PublicKeyForAsync(fodId);

            Assert.AreEqual("pem0", first!.PublicKeyPem);
            Assert.AreSame(first, second);
            Assert.AreEqual(1, _handler.Requests.Count);
        }

        [TestMethod]
        public async Task PublicKeyFor_LongDomainAndContext_IsServedAKey()
        {
            // A self-hosted container signs with its own creator domain,
            // which may be much longer than the public cloud's, and the
            // context section length is the cloud's business, so neither
            // stops a key being found.
            _handler.Enqueue(
                HttpStatusCode.OK, KeysJson((T0, "pem0"), (T1, "pem1")));
            using var client = NewClient();
            var fodId = SignedAt(
                T0.AddDays(1),
                PayloadWithContext(),
                "a-very-long-self-hosted-creator-domain.example.com");

            var key = await client.PublicKeyForAsync(fodId);

            Assert.IsNotNull(key);
            Assert.AreEqual("pem0", key!.PublicKeyPem);
            Assert.AreEqual(1, _handler.Requests.Count);
        }

        [TestMethod]
        public async Task PublicKeyFor_RefetchesWhenDateIsLaterThanNewestStart()
        {
            _handler.Enqueue(HttpStatusCode.OK, KeysJson((T0, "pem0")));
            _handler.Enqueue(HttpStatusCode.OK, KeysJson((T0, "pem0"), (T1, "pem1")));
            using var client = NewClient();
            await client.PublicKeysAsync();

            var key = await client.PublicKeyForAsync(SignedAt(T1.AddDays(1)));

            Assert.AreEqual("pem1", key!.PublicKeyPem);
            Assert.AreEqual(2, _handler.Requests.Count);
        }

        [TestMethod]
        public async Task PublicKeyFor_RefetchesOnceWhenNoEntryOnOrBeforeDate()
        {
            _handler.Enqueue(HttpStatusCode.OK, KeysJson((T0, "pem0")));
            _handler.Enqueue(HttpStatusCode.OK, KeysJson((T0, "pem0")));
            using var client = NewClient();
            await client.PublicKeysAsync();

            var key = await client.PublicKeyForAsync(SignedAt(T0.AddDays(-1)));

            Assert.IsNull(key);
            Assert.AreEqual(2, _handler.Requests.Count);
        }

        [TestMethod]
        public async Task PublicKeyFor_RefetchesWhenCacheIsADayOld()
        {
            _handler.Enqueue(HttpStatusCode.OK, KeysJson((T0, "pem0"), (T1, "pem1")));
            _handler.Enqueue(HttpStatusCode.OK, KeysJson((T0, "pem0"), (T1, "pem1")));
            using var client = NewClient();
            var fodId = SignedAt(T0.AddDays(1));
            await client.PublicKeyForAsync(fodId);

            _time.Now = _time.Now.AddHours(23);
            await client.PublicKeyForAsync(fodId);
            Assert.AreEqual(1, _handler.Requests.Count, "under a day old");

            _time.Now = _time.Now.AddHours(2);
            await client.PublicKeyForAsync(fodId);
            Assert.AreEqual(2, _handler.Requests.Count, "over a day old");
        }

        // ----------------------------------------------------------------
        // Key selection
        // ----------------------------------------------------------------

        [TestMethod]
        public void InForceAt_NewestStartOnOrBefore()
        {
            var keys = Schedule();

            Assert.AreEqual("pem0", DidClient.InForceAt(keys, T0)!.PublicKeyPem);
            Assert.AreEqual("pem0", DidClient.InForceAt(keys, T0.AddDays(3))!.PublicKeyPem);
            Assert.AreEqual("pem1", DidClient.InForceAt(keys, T1)!.PublicKeyPem);
            Assert.AreEqual("pem1", DidClient.InForceAt(keys, T1.AddDays(300))!.PublicKeyPem);
            Assert.IsNull(DidClient.InForceAt(keys, T0.AddMinutes(-1)));
        }

        [TestMethod]
        public void Candidates_InsidePeriod_OnlyTheKeyInForce()
        {
            var candidates = DidClient.CandidatesForDate(Schedule(), T0.AddDays(3));

            CollectionAssert.AreEqual(
                new[] { "pem0" }, candidates.Select(c => c.PublicKeyPem).ToArray());
        }

        [TestMethod]
        public void Candidates_JustAfterBoundary_AddsEarlierNeighbour()
        {
            var candidates = DidClient.CandidatesForDate(Schedule(), T1.AddMinutes(1));

            CollectionAssert.AreEqual(
                new[] { "pem1", "pem0" }, candidates.Select(c => c.PublicKeyPem).ToArray());
        }

        [TestMethod]
        public void Candidates_JustBeforeBoundary_AddsLaterNeighbour()
        {
            var candidates = DidClient.CandidatesForDate(Schedule(), T1.AddMinutes(-1));

            CollectionAssert.AreEqual(
                new[] { "pem0", "pem1" }, candidates.Select(c => c.PublicKeyPem).ToArray());
        }

        [TestMethod]
        public void Candidates_OutsideTolerance_OnlyTheKeyInForce()
        {
            var after = DidClient.CandidatesForDate(Schedule(), T1.AddHours(1));
            var before = DidClient.CandidatesForDate(Schedule(), T1.AddHours(-1));

            CollectionAssert.AreEqual(
                new[] { "pem1" }, after.Select(c => c.PublicKeyPem).ToArray());
            CollectionAssert.AreEqual(
                new[] { "pem0" }, before.Select(c => c.PublicKeyPem).ToArray());
        }

        [TestMethod]
        public void Candidates_BeforeSchedule_NoneBeyondTolerance()
        {
            Assert.AreEqual(
                0, DidClient.CandidatesForDate(Schedule(), T0.AddHours(-1)).Count);
            // Within the tolerance of the first start the first key is tried.
            CollectionAssert.AreEqual(
                new[] { "pem0" },
                DidClient.CandidatesForDate(Schedule(), T0.AddMinutes(-1))
                    .Select(c => c.PublicKeyPem).ToArray());
        }

        [TestMethod]
        public void Candidates_ExtremeDates_DoNotThrow()
        {
            Assert.AreEqual(
                0, DidClient.CandidatesForDate(Schedule(), DateTime.MinValue).Count);
            Assert.AreEqual(
                1, DidClient.CandidatesForDate(Schedule(), DateTime.MaxValue).Count);
        }

        // ----------------------------------------------------------------
        // Offline signature verification
        // ----------------------------------------------------------------

        [TestMethod]
        public async Task VerifySignature_TrueWithTheSigningKey()
        {
            _handler.Enqueue(HttpStatusCode.OK, KeysJson((T0, _factory.PublicPem), (T1, "pem1")));
            using var client = NewClient();
            var fodId = SignedAt(T0.AddDays(1));

            Assert.IsTrue(await client.VerifySignatureAsync(fodId));
            Assert.AreEqual(
                SignatureCheck.Verified,
                await client.VerifySignatureDetailedAsync(fodId));
            Assert.AreEqual(1, _handler.Requests.Count);
        }

        [TestMethod]
        public async Task VerifySignature_FalseWithTheWrongKey()
        {
            var other = new FodIdTestFactory();
            _handler.Enqueue(HttpStatusCode.OK, KeysJson((T0, other.PublicPem), (T1, "pem1")));
            using var client = NewClient();
            var fodId = SignedAt(T0.AddDays(1));

            Assert.IsFalse(await client.VerifySignatureAsync(fodId));
            Assert.AreEqual(
                SignatureCheck.Invalid,
                await client.VerifySignatureDetailedAsync(fodId));
        }

        [TestMethod]
        public async Task VerifySignature_TriesTheNeighbourAtABoundary()
        {
            // Signed under the first key a moment into the second period,
            // as a creator stamping its date just after rollover.
            _handler.Enqueue(HttpStatusCode.OK, KeysJson(
                (T0, _factory.PublicPem),
                (T1, new FodIdTestFactory().PublicPem),
                (T1.AddDays(7), "pem2")));
            using var client = NewClient();

            Assert.IsTrue(await client.VerifySignatureAsync(SignedAt(T1.AddMinutes(1))));
            Assert.IsFalse(await client.VerifySignatureAsync(SignedAt(T1.AddHours(1))));
        }

        [TestMethod]
        public async Task VerifySignature_FalseForVersion2()
        {
            using var client = NewClient();
            var fodId = new FodId(_factory.SignedOwid(
                CanonicalPayload(), T0.AddDays(1), OwidVersion.Version2));

            Assert.IsFalse(await client.VerifySignatureAsync(fodId));
            Assert.AreEqual(
                SignatureCheck.UnsupportedVersion,
                await client.VerifySignatureDetailedAsync(fodId));
            // Refused before any key is needed.
            Assert.AreEqual(0, _handler.Requests.Count);
        }

        [TestMethod]
        public async Task VerifySignature_FalseForPayloadShorterThanBase()
        {
            using var client = NewClient();
            // The reader accepts a Reserved type down to the five header
            // bytes, and the base for anything but Random is 37.
            var payload = new byte[FodId.HeaderLength + 10];
            payload[FodId.FlagsOffset] = 0b1100_0000;
            var fodId = SignedAt(T0.AddDays(1), payload);

            Assert.IsFalse(await client.VerifySignatureAsync(fodId));
            Assert.AreEqual(
                SignatureCheck.InvalidLength,
                await client.VerifySignatureDetailedAsync(fodId));
            Assert.AreEqual(0, _handler.Requests.Count);
        }

        [TestMethod]
        public async Task VerifySignature_TrueForPayloadLongerThanBase()
        {
            _handler.Enqueue(HttpStatusCode.OK, KeysJson((T0, _factory.PublicPem), (T1, "pem1")));
            using var client = NewClient();
            // A creator context section after the value. Its length is the
            // cloud's business, so the check only cares that the payload
            // reaches its base length.
            var payload = PayloadWithContext();
            payload[FodId.PayloadLength] = 0;
            var fodId = SignedAt(T0.AddDays(1), payload);

            Assert.IsTrue(await client.VerifySignatureAsync(fodId));
        }

        [TestMethod]
        public async Task VerifySignature_TrueForRandomTypeAtItsOwnBase()
        {
            _handler.Enqueue(HttpStatusCode.OK, KeysJson((T0, _factory.PublicPem), (T1, "pem1")));
            using var client = NewClient();
            var fodId = SignedAt(T0.AddDays(1), CanonicalRandomPayload());

            Assert.IsTrue(await client.VerifySignatureAsync(fodId));
        }

        [TestMethod]
        public async Task VerifySignature_NoKeyCoversTheDate()
        {
            // The date precedes the schedule, which prompts one refetch, and
            // the refetched schedule still does not reach it.
            _handler.Enqueue(HttpStatusCode.OK, KeysJson((T0, _factory.PublicPem)));
            _handler.Enqueue(HttpStatusCode.OK, KeysJson((T0, _factory.PublicPem)));
            using var client = NewClient();
            var fodId = SignedAt(T0.AddDays(-2));

            Assert.IsFalse(await client.VerifySignatureAsync(fodId));
            _handler.Enqueue(HttpStatusCode.OK, KeysJson((T0, _factory.PublicPem)));
            Assert.AreEqual(
                SignatureCheck.NoKeyForDate,
                await client.VerifySignatureDetailedAsync(fodId));
        }

        // ----------------------------------------------------------------
        // Cloud signature verification
        // ----------------------------------------------------------------

        [TestMethod]
        public async Task Verify_200Valid_True()
        {
            _handler.Enqueue(HttpStatusCode.OK, "{\"valid\":true}");
            using var client = NewClient();
            var fodId = SignedAt(T0.AddDays(1));

            Assert.IsTrue(await client.VerifyAsync(fodId));

            var request = _handler.Requests.Single();
            Assert.AreEqual(HttpMethod.Get, request.Method);
            // The identifier travels under the documented name and under
            // the alias the endpoint first went live with, so a service of
            // either age answers.
            var url = fodId.AsBase64Url();
            Assert.AreEqual(
                Endpoint + "id/verify/" + Resource + "?51did=" + url + "&owid=" + url,
                request.Uri.AbsoluteUri);
            Assert.AreEqual(DidClient.UserAgent, request.UserAgent);
        }

        [TestMethod]
        public async Task Verify_String_IsUrlEncoded()
        {
            _handler.Enqueue(HttpStatusCode.OK, "{\"valid\":true}");
            using var client = NewClient();
            var standard = _factory.SignedOwidBase64(CanonicalPayload());

            Assert.IsTrue(await client.VerifyAsync(standard));

            var query = HttpUtility.ParseQueryString(
                _handler.Requests.Single().Uri.Query);
            Assert.AreEqual(standard, query["51did"]);
            Assert.AreEqual(standard, query["owid"]);
        }

        [TestMethod]
        public async Task Verify_LongIdentifierString_IsAccepted()
        {
            // A long creator domain and a long context section are both
            // legitimate, so neither alphabet is refused for its length.
            _handler.Enqueue(HttpStatusCode.OK, "{\"valid\":true}");
            _handler.Enqueue(HttpStatusCode.OK, "{\"valid\":true}");
            using var client = NewClient();
            var standard = _factory.SignedOwid(
                PayloadWithContext(),
                T0,
                OwidVersion.Version3,
                "a-very-long-self-hosted-creator-domain.example.com")
                .AsBase64();

            Assert.IsTrue(await client.VerifyAsync(standard));
            Assert.IsTrue(
                await client.VerifyAsync(FodId.ToBase64Url(standard)));
            Assert.AreEqual(2, _handler.Requests.Count);
        }

        [TestMethod]
        public async Task Verify_StringBeyondInputGuard_IsRejectedLocally()
        {
            using var client = NewClient();

            var error = await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => client.VerifyAsync(new string('A', 4097)));

            Assert.AreEqual("fodId", error.ParamName);
            Assert.AreEqual(0, _handler.Requests.Count);
        }

        [TestMethod]
        public async Task Verify_400Invalid_False()
        {
            _handler.Enqueue(HttpStatusCode.BadRequest, "{\"valid\":false}");
            using var client = NewClient();

            Assert.IsFalse(await client.VerifyAsync(SignedAt(T0)));
        }

        [TestMethod]
        public async Task Verify_400Errors_ThrowsArgumentException()
        {
            // A value that parses here can still be refused by the cloud,
            // whose message is relayed. A value that does not parse never
            // reaches the cloud, see DidClientMalformedInputTests.
            _handler.Enqueue(
                HttpStatusCode.BadRequest,
                "{\"errors\":[\"The resource key does not include the 51Did properties.\"]}");
            using var client = NewClient();

            var error = await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => client.VerifyAsync(_factory.SignedOwidBase64(CanonicalPayload())));

            StringAssert.Contains(error.Message, "does not include the 51Did properties");
            Assert.AreEqual(1, _handler.Requests.Count);
        }

        [TestMethod]
        public async Task Verify_OtherStatus_ThrowsHttpRequestException()
        {
            _handler.Enqueue(HttpStatusCode.Forbidden, "{\"errors\":[\"no\"]}");
            using var client = NewClient();

            var error = await Assert.ThrowsExactlyAsync<HttpRequestException>(
                () => client.VerifyAsync(SignedAt(T0)));

            Assert.AreEqual(HttpStatusCode.Forbidden, error.StatusCode);
        }

        [TestMethod]
        public async Task Verify_EmptyString_Throws()
        {
            using var client = NewClient();

            await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => client.VerifyAsync(" "));
        }

        // ----------------------------------------------------------------
        // Redeem
        // ----------------------------------------------------------------

        private const string RedeemedMismatch =
            "{\"signature\":\"verified\",\"context\":\"mismatch\","
            + "\"factors\":{\"transport\":\"verified\",\"device\":\"mismatch\","
            + "\"browserip\":\"verified\",\"connectionip\":\"mismatch\","
            + "\"asn\":\"verified\",\"browser\":\"verified\"},"
            + "\"verifiedAt\":\"2026-08-07T09:15:32Z\",\"secondsSinceVerified\":2}";

        [TestMethod]
        public async Task Redeem_RedeemedWithFactors()
        {
            _handler.Enqueue(HttpStatusCode.OK, RedeemedMismatch);
            using var client = NewClient();
            var fodId = SignedAt(T0.AddDays(1));

            var result = await client.RedeemAsync(fodId, "sealed", "abc123");

            Assert.AreEqual(ContextOutcome.Mismatch, result.Context);
            Assert.AreEqual("mismatch", result.ContextValue);
            Assert.AreEqual(SignatureOutcome.Verified, result.Signature);
            Assert.IsNotNull(result.Factors);
            Assert.AreEqual(6, result.Factors!.Count);
            Assert.AreEqual(FactorOutcome.Verified, result.Factors["transport"]);
            Assert.AreEqual(FactorOutcome.Mismatch, result.Factors["device"]);
            Assert.AreEqual(FactorOutcome.Mismatch, result.Factors["connectionip"]);
            Assert.AreEqual(
                new DateTime(2026, 8, 7, 9, 15, 32, DateTimeKind.Utc),
                result.VerifiedAt);
            Assert.AreEqual(DateTimeKind.Utc, result.VerifiedAt!.Value.Kind);
            Assert.AreEqual(2, result.SecondsSinceVerified);
            Assert.AreEqual(200, result.StatusCode);
            Assert.AreEqual(RedeemedMismatch, result.Raw);

            // The request shape: a POST to the bare redeem path with every
            // parameter in the form body and nothing in the URL.
            var request = _handler.Requests.Single();
            Assert.AreEqual(HttpMethod.Post, request.Method);
            Assert.AreEqual(Endpoint + "id/redeem", request.Uri.AbsoluteUri);
            Assert.AreEqual("application/x-www-form-urlencoded", request.ContentType);
            Assert.AreEqual(DidClient.UserAgent, request.UserAgent);
            var form = HttpUtility.ParseQueryString(request.Body!);
            Assert.AreEqual(Resource, form["resource"]);
            Assert.AreEqual(fodId.AsBase64Url(), form["51did"]);
            Assert.AreEqual("sealed", form["result"]);
            Assert.AreEqual("abc123", form["challenge"]);
            Assert.AreEqual(Licence, form["license"]);
            Assert.IsFalse(request.Uri.AbsoluteUri.Contains(Licence));
            Assert.IsFalse(request.Uri.AbsoluteUri.Contains(Resource));
        }

        [TestMethod]
        public async Task Redeem_RedeemedWithoutFactors()
        {
            _handler.Enqueue(HttpStatusCode.OK,
                "{\"signature\":\"verified\",\"context\":\"verified\","
                + "\"verifiedAt\":\"2026-08-07T09:15:32Z\",\"secondsSinceVerified\":1}");
            using var client = NewClient();

            var result = await client.RedeemAsync(SignedAt(T0), "sealed", null);

            Assert.AreEqual(ContextOutcome.Verified, result.Context);
            Assert.AreEqual(SignatureOutcome.Verified, result.Signature);
            Assert.IsNull(result.Factors);
            Assert.AreEqual(1, result.SecondsSinceVerified);
            Assert.IsNotNull(result.VerifiedAt);
            var form = HttpUtility.ParseQueryString(_handler.Requests.Single().Body!);
            Assert.AreEqual(string.Empty, form["challenge"]);
        }

        [TestMethod]
        public async Task Redeem_SignatureInvalid()
        {
            _handler.Enqueue(HttpStatusCode.OK,
                "{\"signature\":\"invalid\",\"context\":\"verified\","
                + "\"verifiedAt\":\"2026-08-07T09:15:32Z\",\"secondsSinceVerified\":1}");
            using var client = NewClient();

            var result = await client.RedeemAsync(SignedAt(T0), "sealed");

            Assert.AreEqual(SignatureOutcome.Invalid, result.Signature);
        }

        [TestMethod]
        public async Task Redeem_Expired()
        {
            _handler.Enqueue(HttpStatusCode.OK,
                "{\"context\":\"expired\",\"verifiedAt\":\"2026-08-07T09:15:32Z\","
                + "\"secondsSinceVerified\":14}");
            using var client = NewClient();

            var result = await client.RedeemAsync(SignedAt(T0), "sealed");

            Assert.AreEqual(ContextOutcome.Expired, result.Context);
            Assert.AreEqual(SignatureOutcome.Unknown, result.Signature);
            Assert.AreEqual(14, result.SecondsSinceVerified);
            Assert.IsNotNull(result.VerifiedAt);
            Assert.IsNull(result.Factors);
        }

        [TestMethod]
        public async Task Redeem_Replayed()
        {
            _handler.Enqueue(HttpStatusCode.OK, "{\"context\":\"replayed\"}");
            using var client = NewClient();

            var result = await client.RedeemAsync(SignedAt(T0), "sealed");

            Assert.AreEqual(ContextOutcome.Replayed, result.Context);
            Assert.AreEqual(SignatureOutcome.Unknown, result.Signature);
            Assert.IsNull(result.VerifiedAt);
            Assert.IsNull(result.SecondsSinceVerified);
        }

        [TestMethod]
        public async Task Redeem_Unreadable()
        {
            _handler.Enqueue(HttpStatusCode.OK, "{\"context\":\"unreadable\"}");
            using var client = NewClient();

            var result = await client.RedeemAsync(SignedAt(T0), "not-base64url!!");

            Assert.AreEqual(ContextOutcome.Unreadable, result.Context);
            Assert.AreEqual(200, result.StatusCode);
        }

        [TestMethod]
        public async Task Redeem_503Unconfirmed()
        {
            _handler.Enqueue(HttpStatusCode.ServiceUnavailable, "{\"context\":\"unconfirmed\"}");
            using var client = NewClient();

            var result = await client.RedeemAsync(SignedAt(T0), "sealed");

            Assert.AreEqual(ContextOutcome.Unconfirmed, result.Context);
            Assert.AreEqual(503, result.StatusCode);
        }

        [TestMethod]
        public async Task Redeem_400Errors_ThrowsArgumentException()
        {
            // A value that parses here can still be refused by the cloud,
            // whose message is relayed. A value that does not parse never
            // reaches the cloud, see DidClientMalformedInputTests.
            _handler.Enqueue(HttpStatusCode.BadRequest,
                "{\"errors\":[\"The resource key does not include the 51Did properties.\"]}");
            using var client = NewClient();

            var error = await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => client.RedeemAsync(
                    _factory.SignedOwidBase64(CanonicalPayload()), "sealed"));

            StringAssert.Contains(error.Message, "does not include the 51Did properties");
            Assert.AreEqual(1, _handler.Requests.Count);
        }

        [TestMethod]
        public async Task Redeem_StringBeyondInputGuard_IsRejectedLocally()
        {
            using var client = NewClient();

            var error = await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => client.RedeemAsync(new string('A', 4097), "sealed"));

            Assert.AreEqual("fodId", error.ParamName);
            Assert.AreEqual(0, _handler.Requests.Count);
        }

        [TestMethod]
        public async Task Redeem_404_ThrowsNotSupported()
        {
            _handler.Enqueue(HttpStatusCode.NotFound, "", "text/plain");
            using var client = NewClient();

            var error = await Assert.ThrowsExactlyAsync<NotSupportedException>(
                () => client.RedeemAsync(SignedAt(T0), "sealed"));

            StringAssert.Contains(error.Message, Endpoint);
        }

        [TestMethod]
        public async Task Redeem_UnknownContextString_IsUnreadableWithRawValue()
        {
            _handler.Enqueue(HttpStatusCode.OK, "{\"context\":\"something-new\"}");
            using var client = NewClient();

            var result = await client.RedeemAsync(SignedAt(T0), "sealed");

            Assert.AreEqual(ContextOutcome.Unreadable, result.Context);
            Assert.AreEqual("something-new", result.ContextValue);
        }

        [TestMethod]
        public async Task Redeem_NonJsonBody_IsUnreadable()
        {
            _handler.Enqueue(HttpStatusCode.OK, "<html>", "text/html");
            using var client = NewClient();

            var result = await client.RedeemAsync(SignedAt(T0), "sealed");

            Assert.AreEqual(ContextOutcome.Unreadable, result.Context);
            Assert.IsNull(result.ContextValue);
            Assert.AreEqual("<html>", result.Raw);
        }

        [TestMethod]
        public async Task Redeem_WithoutLicenceKey_OmitsTheField()
        {
            _handler.Enqueue(HttpStatusCode.OK, "{\"context\":\"unreadable\"}");
            using var client = NewClient(null);

            await client.RedeemAsync(SignedAt(T0), "sealed", "c");

            var form = HttpUtility.ParseQueryString(_handler.Requests.Single().Body!);
            Assert.IsNull(form["license"]);
            Assert.AreEqual(Resource, form["resource"]);
            CollectionAssert.AreEquivalent(
                new[] { "resource", "51did", "result", "challenge" },
                form.AllKeys);
        }

        [TestMethod]
        public async Task Redeem_OtherStatus_ThrowsHttpRequestException()
        {
            _handler.Enqueue(HttpStatusCode.Unauthorized, "{\"errors\":[\"no\"]}");
            using var client = NewClient();

            var error = await Assert.ThrowsExactlyAsync<HttpRequestException>(
                () => client.RedeemAsync(SignedAt(T0), "sealed"));

            Assert.AreEqual(HttpStatusCode.Unauthorized, error.StatusCode);
            StringAssert.Contains(error.Message, "401");
        }

        [TestMethod]
        public async Task Redeem_TransportFailure_ThrowsHttpRequestException()
        {
            _handler.EnqueueFailure(new HttpRequestException("no route to host"));
            using var client = NewClient();

            var error = await Assert.ThrowsExactlyAsync<HttpRequestException>(
                () => client.RedeemAsync(SignedAt(T0), "sealed"));

            Assert.IsNull(error.StatusCode);
        }

        [TestMethod]
        public void RedeemResult_MapsEveryContextWord()
        {
            foreach (ContextOutcome outcome in Enum.GetValues(typeof(ContextOutcome)))
            {
                Assert.AreEqual(
                    outcome,
                    RedeemResult.ParseContext(RedeemResult.ToContextValue(outcome)));
            }
            Assert.AreEqual(ContextOutcome.Unreadable, RedeemResult.ParseContext(null));
            Assert.AreEqual(SignatureOutcome.Unknown, RedeemResult.ParseSignature(null));
            Assert.AreEqual(SignatureOutcome.Unknown, RedeemResult.ParseSignature("maybe"));
        }

        /// <summary>
        /// A clock the tests move by hand.
        /// </summary>
        private sealed class FakeTimeProvider : TimeProvider
        {
            public FakeTimeProvider(DateTimeOffset now)
            {
                Now = now;
            }

            public DateTimeOffset Now { get; set; }

            public override DateTimeOffset GetUtcNow() => Now;
        }
    }
}
