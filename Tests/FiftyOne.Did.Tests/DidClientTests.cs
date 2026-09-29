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

        // The shape of an answer that gives each key's scheduled end.
        private static string EndedKeysJson(
            params (DateTime Start, DateTime End, string Pem)[] keys)
            => JsonSerializer.Serialize(keys.Select(key =>
                new Dictionary<string, string>
                {
                    ["startsAt"] = key.Start.ToString("o"),
                    ["endsAt"] = key.End.ToString("o"),
                    ["publicKey"] = key.Pem,
                }).ToArray());

        // The start a key request asked from, or null where it gave none.
        private static string? AskedFrom(FakeHttpHandler.Recorded request)
            => HttpUtility.ParseQueryString(request.Uri.Query)["datetime"];

        private FodId SignedAt(
            DateTime date,
            byte[]? payload = null,
            string domain = TestDomain) =>
            new FodId(_factory.SignedOwid(
                payload ?? CanonicalPayload(),
                date,
                OwidVersion.Version3,
                domain));

        private static FodId SignedBy(FodIdTestFactory signer, DateTime date)
            => new FodId(signer.SignedOwid(CanonicalPayload(), date));

        // A payload with a creator context section after the value. The
        // section's length belongs to the cloud, so this is simply longer
        // than the base and nothing here depends on how much longer.
        private static byte[] PayloadWithContext()
        {
            var payload = new byte[FodId.MinimumPayloadLength + 128];
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
        public void ParseKeys_ReadsEndsAtWhereGiven()
        {
            var keys = DidClient.ParseKeys(
                "[{\"startsAt\":\"2026-08-10T00:00:00Z\","
                + "\"endsAt\":\"2026-08-17T00:00:00Z\",\"publicKey\":\"pem1\"},"
                + "{\"startsAt\":\"2026-08-03T00:00:00Z\",\"endsAt\":null,"
                + "\"publicKey\":\"pem0\"},"
                + "{\"startsAt\":\"2026-07-27T00:00:00Z\",\"publicKey\":\"pem9\"}]");

            Assert.IsNull(keys[0].EndsAt);
            Assert.AreEqual(T0, keys[1].StartsAt);
            Assert.IsNull(keys[1].EndsAt);
            Assert.AreEqual(T1, keys[2].StartsAt);
            Assert.AreEqual(T1.AddDays(7), keys[2].EndsAt);
            Assert.AreEqual(DateTimeKind.Utc, keys[2].EndsAt!.Value.Kind);
        }

        [TestMethod]
        public void ParseKeys_EndNotAfterStart_IsUnreadable()
        {
            Assert.ThrowsExactly<FormatException>(() => DidClient.ParseKeys(
                "[{\"startsAt\":\"2026-08-03T00:00:00Z\","
                + "\"endsAt\":\"2026-08-03T00:00:00Z\",\"publicKey\":\"pem0\"}]"));
            Assert.ThrowsExactly<FormatException>(() => DidClient.ParseKeys(
                "[{\"startsAt\":\"2026-08-10T00:00:00Z\","
                + "\"endsAt\":\"2026-08-03T00:00:00Z\",\"publicKey\":\"pem0\"}]"));
            Assert.ThrowsExactly<ArgumentException>(
                () => new DidPublicKey(T1, "pem0", T0));
        }

        [TestMethod]
        public async Task PublicKeyFor_AnswerWithAnEndNotAfterItsStart_MergesNothing()
        {
            // One such entry makes the whole answer unreadable, so none of
            // it is merged, the good entry included.
            _handler.Enqueue(HttpStatusCode.OK, EndedKeysJson((T0, T1, "pem0")));
            _handler.Enqueue(HttpStatusCode.OK, EndedKeysJson(
                (T1, T1.AddDays(7), "pem1"),
                (T1.AddDays(7), T1.AddDays(7), "pem2")));
            using var client = NewClient();
            await client.PublicKeysAsync();

            await Assert.ThrowsExactlyAsync<FormatException>(
                () => client.PublicKeyForAsync(SignedAt(T1.AddHours(1))));

            var keys = await client.PublicKeysAsync();
            Assert.AreEqual(1, keys.Count);
            Assert.AreEqual(T1, keys[0].EndsAt);
        }

        [TestMethod]
        public async Task PublicKeyFor_KeysWithoutEnds_AnswersFromCache()
        {
            // An answer that gives no ends and includes keys not started
            // yet covers up to its newest start, so an identifier from the
            // current period prompts no fetch.
            _handler.Enqueue(HttpStatusCode.OK, KeysJson(
                (T0, "pem0"), (T1, "pem1"), (T1.AddDays(7), "pem2")));
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
            // Without ends the keys held cover up to the newest start, and
            // the fetch asks only from that start.
            _handler.Enqueue(HttpStatusCode.OK, KeysJson((T0, "pem0")));
            _handler.Enqueue(HttpStatusCode.OK, KeysJson((T0, "pem0"), (T1, "pem1")));
            using var client = NewClient();
            await client.PublicKeysAsync();

            var key = await client.PublicKeyForAsync(SignedAt(T1.AddDays(1)));

            Assert.AreEqual("pem1", key!.PublicKeyPem);
            Assert.AreEqual(2, _handler.Requests.Count);
            Assert.IsNull(AskedFrom(_handler.Requests[0]));
            Assert.AreEqual(T0.ToString("o"), AskedFrom(_handler.Requests[1]));
        }

        [TestMethod]
        public async Task PublicKeyFor_DateBeforeEveryKey_MakesNoFetch()
        {
            // The first answer held every key the service publishes, and a
            // later fetch asks only from the newest start held, so a date
            // before every key held prompts no fetch.
            _handler.Enqueue(HttpStatusCode.OK, KeysJson((T0, "pem0")));
            using var client = NewClient();
            await client.PublicKeysAsync();

            var key = await client.PublicKeyForAsync(SignedAt(T0.AddDays(-1)));

            Assert.IsNull(key);
            Assert.AreEqual(1, _handler.Requests.Count);
        }

        [TestMethod]
        public async Task PublicKeyFor_EmptyAnswer_FetchesAgainAtMostOnceAMinute()
        {
            // An answer with no keys covers nothing, so the next lookup asks
            // again for every key, and the one after within the minute makes
            // no request.
            _handler.Enqueue(HttpStatusCode.OK, "[]");
            _handler.Enqueue(HttpStatusCode.OK, "[]");
            _handler.Enqueue(HttpStatusCode.OK, KeysJson((T0, "pem0")));
            using var client = NewClient();
            var fodId = SignedAt(T0.AddDays(1));

            Assert.IsNull(await client.PublicKeyForAsync(fodId));
            Assert.IsNull(await client.PublicKeyForAsync(fodId));
            Assert.IsNull(await client.PublicKeyForAsync(fodId));
            Assert.AreEqual(2, _handler.Requests.Count);

            _time.Now = _time.Now.AddMinutes(1);
            var key = await client.PublicKeyForAsync(fodId);

            Assert.AreEqual("pem0", key!.PublicKeyPem);
            Assert.AreEqual(3, _handler.Requests.Count);
            Assert.IsNull(AskedFrom(_handler.Requests[1]));
            Assert.IsNull(AskedFrom(_handler.Requests[2]));
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

        [TestMethod]
        public async Task PublicKeyFor_DayOldList_FetchesTheWholeList()
        {
            // Only a fetch of the whole list resets its age, because only
            // that fetch sees a change to a key older than the newest held,
            // and the one minute limit neither counts it nor holds it back.
            var keys = KeysJson((T0, "pem0"), (T1, "pem1"));
            _handler.Enqueue(HttpStatusCode.OK, KeysJson((T0, "pem0")));
            _handler.Enqueue(HttpStatusCode.OK, keys);
            _handler.Enqueue(HttpStatusCode.OK, keys);
            using var client = NewClient();
            await client.PublicKeysAsync();

            // A date past the newest start fetches from it, just before the
            // list is a day old.
            _time.Now = _time.Now.AddDays(1).AddSeconds(-30);
            await client.PublicKeyForAsync(SignedAt(T1.AddDays(1)));
            // Moments later the list is over a day old all the same.
            _time.Now = _time.Now.AddSeconds(40);
            await client.PublicKeyForAsync(SignedAt(T0.AddDays(1)));

            Assert.AreEqual(3, _handler.Requests.Count);
            Assert.IsNull(AskedFrom(_handler.Requests[0]));
            Assert.AreEqual(T0.ToString("o"), AskedFrom(_handler.Requests[1]));
            Assert.IsNull(AskedFrom(_handler.Requests[2]));
        }

        [TestMethod]
        public async Task PublicKeys_LaterAnswerWithEnd_ReplacesTheEntryHeld()
        {
            // The first answer gives no ends, so the keys held cover up to
            // the newest start. A date past it fetches from that start, and
            // the answer's copy of the entry replaces the one held, bringing
            // its end, whilst the older entry the answer leaves out is kept.
            _handler.Enqueue(
                HttpStatusCode.OK, KeysJson((T0, "pem0"), (T1, "pem1")));
            _handler.Enqueue(HttpStatusCode.OK, EndedKeysJson(
                (T1, T1.AddDays(7), "pem1")));
            using var client = NewClient();
            Assert.IsNull((await client.PublicKeysAsync())[1].EndsAt);

            var key = await client.PublicKeyForAsync(SignedAt(T1.AddDays(1)));
            var keys = await client.PublicKeysAsync();

            Assert.AreEqual("pem1", key!.PublicKeyPem);
            Assert.AreEqual(2, _handler.Requests.Count);
            Assert.AreEqual(T1.ToString("o"), AskedFrom(_handler.Requests[1]));
            Assert.AreEqual(2, keys.Count);
            Assert.AreEqual(T0, keys[0].StartsAt);
            Assert.IsNull(keys[0].EndsAt);
            Assert.AreEqual(T1, keys[1].StartsAt);
            Assert.AreEqual(T1.AddDays(7), keys[1].EndsAt);

            // The keys held now end later, so a later date in the same
            // period needs no request, even once the minute has passed.
            _time.Now = _time.Now.AddMinutes(2);
            await client.PublicKeyForAsync(SignedAt(T1.AddDays(3)));
            Assert.AreEqual(2, _handler.Requests.Count);
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
        public void InForceAt_NotAtOrAfterItsEnd()
        {
            var keys = new[] { new DidPublicKey(T0, "pem0", T1) };

            Assert.AreEqual(
                "pem0",
                DidClient.InForceAt(keys, T1.AddTicks(-1))!.PublicKeyPem);
            Assert.IsNull(DidClient.InForceAt(keys, T1));
            // Just after its end the key is still tried as the neighbour,
            // and beyond the boundary tolerance nothing is.
            CollectionAssert.AreEqual(
                new[] { "pem0" },
                DidClient.CandidatesForDate(keys, T1.AddMinutes(1))
                    .Select(c => c.PublicKeyPem).ToArray());
            Assert.AreEqual(
                0, DidClient.CandidatesForDate(keys, T1.AddHours(1)).Count);
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
            var keys = KeysJson((T0, other.PublicPem), (T1, "pem1"));
            _handler.Enqueue(HttpStatusCode.OK, keys);
            // The second check fails with the keys held, so it fetches once
            // more before answering, in case the key was replaced.
            _handler.Enqueue(HttpStatusCode.OK, keys);
            using var client = NewClient();
            var fodId = SignedAt(T0.AddDays(1));

            // The first check fetched the keys it failed with, which cannot
            // get any better, so it makes no second request.
            Assert.IsFalse(await client.VerifySignatureAsync(fodId));
            Assert.AreEqual(1, _handler.Requests.Count);
            Assert.AreEqual(
                SignatureCheck.Invalid,
                await client.VerifySignatureDetailedAsync(fodId));
            Assert.AreEqual(2, _handler.Requests.Count);
        }

        [TestMethod]
        public async Task VerifySignature_TriesTheNeighbourAtABoundary()
        {
            // Signed under the first key a moment into the second period,
            // as a creator stamping its date just after rollover.
            var keys = KeysJson(
                (T0, _factory.PublicPem),
                (T1, new FodIdTestFactory().PublicPem),
                (T1.AddDays(7), "pem2"));
            _handler.Enqueue(HttpStatusCode.OK, keys);
            // Answers the fetch made when the second fails with the keys
            // held.
            _handler.Enqueue(HttpStatusCode.OK, keys);
            using var client = NewClient();

            Assert.IsTrue(await client.VerifySignatureAsync(SignedAt(T1.AddMinutes(1))));
            Assert.IsFalse(await client.VerifySignatureAsync(SignedAt(T1.AddHours(1))));
            Assert.AreEqual(2, _handler.Requests.Count);
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
            payload[FodId.FlagsOffset] = 0b1100_0001;
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
            payload[FodId.MinimumPayloadLength] = 0;
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
            // The date precedes every key held, which no fetch could
            // change, so no key is tried and no further request is made.
            _handler.Enqueue(HttpStatusCode.OK, KeysJson((T0, _factory.PublicPem)));
            using var client = NewClient();
            var fodId = SignedAt(T0.AddDays(-2));

            Assert.IsFalse(await client.VerifySignatureAsync(fodId));
            Assert.AreEqual(
                SignatureCheck.NoKeyForDate,
                await client.VerifySignatureDetailedAsync(fodId));
            Assert.AreEqual(1, _handler.Requests.Count);
        }

        [TestMethod]
        public async Task VerifySignature_KeysWithEnds_VerifyOfflineUntilTheNewestEnd()
        {
            // Only started keys are published, each with its scheduled end,
            // so the newest key's end is the start of one not published
            // yet. Identifiers dated before that end, by more than the
            // boundary tolerance, verify with no request.
            var earlier = new FodIdTestFactory();
            _time.Now = T1.AddMinutes(-16);
            _handler.Enqueue(HttpStatusCode.OK, EndedKeysJson(
                (T0.AddDays(-7), T0, earlier.PublicPem),
                (T0, T1, _factory.PublicPem)));
            using var client = NewClient();

            foreach (var date in new[]
            {
                T0,
                T0.AddDays(1),
                T0.AddDays(3),
                T0.AddDays(6),
                T1.AddMinutes(-16),
            })
            {
                Assert.AreEqual(
                    SignatureCheck.Verified,
                    await client.VerifySignatureDetailedAsync(SignedAt(date)),
                    date.ToString("o"));
            }

            Assert.AreEqual(1, _handler.Requests.Count);
        }

        [TestMethod]
        public async Task VerifySignature_AtTheNewestEndLessTolerance_FetchesOnce()
        {
            // From the newest end less the boundary tolerance, the key in
            // force or its neighbour may be one not held yet, so the list is
            // fetched once more, asking from the newest start held, and the
            // answer is merged in.
            var earlier = new FodIdTestFactory();
            var next = new FodIdTestFactory();
            _time.Now = T1.AddMinutes(-20);
            _handler.Enqueue(HttpStatusCode.OK, EndedKeysJson(
                (T0.AddDays(-7), T0, earlier.PublicPem),
                (T0, T1, _factory.PublicPem)));
            using var client = NewClient();
            await client.PublicKeysAsync();
            _time.Now = T1.AddMinutes(-10);
            _handler.Enqueue(HttpStatusCode.OK, EndedKeysJson(
                (T0, T1, _factory.PublicPem),
                (T1, T1.AddDays(7), next.PublicPem)));

            // Signed under the key held, which would verify it, but dated
            // exactly the tolerance before the newest end, so the list is
            // fetched first.
            Assert.AreEqual(
                SignatureCheck.Verified,
                await client.VerifySignatureDetailedAsync(
                    SignedAt(T1.AddMinutes(-15))));
            Assert.AreEqual(2, _handler.Requests.Count);
            // Signed under the next key with the same date, so only the
            // newly published neighbour verifies it, and one from the next
            // period verifies from the merged list, neither with a request.
            Assert.AreEqual(
                SignatureCheck.Verified,
                await client.VerifySignatureDetailedAsync(
                    SignedBy(next, T1.AddMinutes(-15))));
            _time.Now = T1.AddHours(2);
            Assert.AreEqual(
                SignatureCheck.Verified,
                await client.VerifySignatureDetailedAsync(
                    SignedBy(next, T1.AddHours(1))));

            Assert.AreEqual(2, _handler.Requests.Count);
            var request = _handler.Requests[1];
            Assert.AreEqual(
                Endpoint + "id/key/" + Resource,
                request.Uri.GetLeftPart(UriPartial.Path));
            Assert.AreEqual(T0.ToString("o"), AskedFrom(request));
            // The older key the answer left out is kept.
            CollectionAssert.AreEqual(
                new[] { T0.AddDays(-7), T0, T1 },
                (await client.PublicKeysAsync())
                    .Select(key => key.StartsAt).ToArray());
        }

        [TestMethod]
        public async Task VerifySignature_AfterTheNewestEnd_FetchesAtMostOnceAMinute()
        {
            // A date in a period whose key is not published, as a forged
            // date may be, fetches at most once a minute, the first fetch of
            // the whole list not counting, and is answered as a date no key
            // held covers, never as a bad signature.
            var keys = EndedKeysJson((T0, T1, _factory.PublicPem));
            _handler.Enqueue(HttpStatusCode.OK, keys);
            _handler.Enqueue(HttpStatusCode.OK, keys);
            using var client = NewClient();
            await client.PublicKeysAsync();

            Assert.AreEqual(
                SignatureCheck.NoKeyForDate,
                await client.VerifySignatureDetailedAsync(
                    SignedAt(T1.AddHours(1))));
            Assert.AreEqual(
                SignatureCheck.NoKeyForDate,
                await client.VerifySignatureDetailedAsync(
                    SignedAt(T1.AddHours(2))));
            Assert.AreEqual(2, _handler.Requests.Count, "one in the minute");

            _time.Now = _time.Now.AddMinutes(1);
            _handler.Enqueue(HttpStatusCode.OK, keys);
            Assert.AreEqual(
                SignatureCheck.NoKeyForDate,
                await client.VerifySignatureDetailedAsync(
                    SignedAt(T1.AddHours(3))));
            Assert.AreEqual(3, _handler.Requests.Count, "one a minute later");
        }

        [TestMethod]
        public async Task VerifySignature_FetchThatFails_CountsTowardTheMinute()
        {
            // A request for keys that fails is counted too, so a cloud that
            // cannot be reached is not asked on every lookup, and lookups
            // within the minute are answered from the keys held.
            _handler.Enqueue(HttpStatusCode.OK, EndedKeysJson(
                (T0, T1, _factory.PublicPem)));
            using var client = NewClient();
            await client.PublicKeysAsync();
            _handler.EnqueueFailure(
                new HttpRequestException("no route to host"));

            await Assert.ThrowsExactlyAsync<HttpRequestException>(
                () => client.VerifySignatureDetailedAsync(
                    SignedAt(T1.AddHours(1))));
            Assert.AreEqual(
                SignatureCheck.NoKeyForDate,
                await client.VerifySignatureDetailedAsync(
                    SignedAt(T1.AddHours(2))));
            Assert.AreEqual(
                SignatureCheck.Verified,
                await client.VerifySignatureDetailedAsync(
                    SignedAt(T0.AddDays(1))));

            Assert.AreEqual(2, _handler.Requests.Count);
        }

        [TestMethod]
        public async Task VerifySignature_KeyReplacedMidPeriod_FetchesOnceThenVerifies()
        {
            // A key may be replaced before its scheduled end. The service
            // then ends the old entry at the replacement's start and
            // publishes the replacement from there.
            var replacement = new FodIdTestFactory();
            var replacedAt = T0.AddDays(1).AddHours(2);
            _handler.Enqueue(HttpStatusCode.OK, EndedKeysJson(
                (T0, T1, _factory.PublicPem)));
            using var client = NewClient();
            await client.PublicKeysAsync();
            _time.Now = replacedAt.AddHours(1);
            _handler.Enqueue(HttpStatusCode.OK, EndedKeysJson(
                (T0, replacedAt, _factory.PublicPem),
                (replacedAt, T1, replacement.PublicPem)));

            // Genuine and signed under the replacement, so it fails with the
            // key held and verifies after one fetch.
            Assert.AreEqual(
                SignatureCheck.Verified,
                await client.VerifySignatureDetailedAsync(
                    SignedBy(replacement, replacedAt.AddMinutes(30))));
            Assert.AreEqual(2, _handler.Requests.Count);
            Assert.AreEqual(T0.ToString("o"), AskedFrom(_handler.Requests[1]));

            // Signed under the old key after the replacement, so refused.
            Assert.AreEqual(
                SignatureCheck.Invalid,
                await client.VerifySignatureDetailedAsync(
                    SignedAt(replacedAt.AddMinutes(45))));
            // Made under the old key before the replacement, so still good.
            Assert.AreEqual(
                SignatureCheck.Verified,
                await client.VerifySignatureDetailedAsync(
                    SignedAt(replacedAt.AddHours(-1))));
            Assert.AreEqual(2, _handler.Requests.Count);
        }

        [TestMethod]
        public async Task VerifySignature_OlderKeyReplaced_FetchesFromTheKeyInForce()
        {
            // The next key is already held when the key in force is replaced
            // in the last minutes of its period. The fetch after the failure
            // asks from the start of the key in force at the identifier's
            // date, which brings the replacement, and not from the newest
            // start held, which would not.
            var replacement = new FodIdTestFactory();
            var next = new FodIdTestFactory();
            var replacedAt = T1.AddMinutes(-5);
            _time.Now = T1.AddMinutes(-10);
            _handler.Enqueue(HttpStatusCode.OK, EndedKeysJson(
                (T0, T1, _factory.PublicPem),
                (T1, T1.AddDays(7), next.PublicPem)));
            _handler.Enqueue(HttpStatusCode.OK, EndedKeysJson(
                (T0, replacedAt, _factory.PublicPem),
                (replacedAt, T1, replacement.PublicPem),
                (T1, T1.AddDays(7), next.PublicPem)));
            using var client = NewClient();
            await client.PublicKeysAsync();
            _time.Now = T1.AddMinutes(-2);

            Assert.AreEqual(
                SignatureCheck.Verified,
                await client.VerifySignatureDetailedAsync(
                    SignedBy(replacement, T1.AddMinutes(-3))));

            Assert.AreEqual(2, _handler.Requests.Count);
            Assert.AreEqual(T0.ToString("o"), AskedFrom(_handler.Requests[1]));
        }

        [TestMethod]
        public async Task VerifySignature_CallersAtTheEnd_ShareOneFetch()
        {
            // A second caller waits for the fetch the first started, rather
            // than making its own or answering from the list that lacks the
            // key.
            var next = new FodIdTestFactory();
            _handler.Enqueue(HttpStatusCode.OK, EndedKeysJson(
                (T0, T1, _factory.PublicPem)));
            using var client = NewClient();
            await client.PublicKeysAsync();
            var held = _handler.EnqueueHeld();
            var fodId = SignedBy(next, T1.AddHours(1));

            var first = client.VerifySignatureDetailedAsync(fodId);
            var second = client.VerifySignatureDetailedAsync(fodId);

            Assert.AreEqual(2, _handler.Requests.Count);
            Assert.IsFalse(first.IsCompleted);
            Assert.IsFalse(second.IsCompleted);
            held.SetResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(EndedKeysJson(
                    (T0, T1, _factory.PublicPem),
                    (T1, T1.AddDays(7), next.PublicPem))),
            });
            Assert.AreEqual(SignatureCheck.Verified, await first);
            Assert.AreEqual(SignatureCheck.Verified, await second);
            Assert.AreEqual(2, _handler.Requests.Count);
        }

        [TestMethod]
        public async Task PublicKeyFor_ClockSetBack_DoesNotHoldAFetchBack()
        {
            // The minute is measured forward, so a clock set back behind the
            // last fetch does not stop the next one.
            var keys = EndedKeysJson((T0, T1, _factory.PublicPem));
            _handler.Enqueue(HttpStatusCode.OK, keys);
            _handler.Enqueue(HttpStatusCode.OK, keys);
            _handler.Enqueue(HttpStatusCode.OK, keys);
            using var client = NewClient();
            await client.PublicKeysAsync();

            await client.PublicKeyForAsync(SignedAt(T1.AddHours(1)));
            _time.Now = _time.Now.AddHours(-1);
            await client.PublicKeyForAsync(SignedAt(T1.AddHours(2)));

            Assert.AreEqual(3, _handler.Requests.Count);
        }

        [TestMethod]
        public async Task VerifySignature_FailsWithEveryKeyHeld_FetchesAtMostOnceAMinute()
        {
            // A forged signature fails with every key held and prompts one
            // more fetch in case the key was replaced, but no other within
            // the minute, so forgeries cannot make a request each lookup.
            var forger = new FodIdTestFactory();
            var keys = EndedKeysJson((T0, T1, _factory.PublicPem));
            _handler.Enqueue(HttpStatusCode.OK, keys);
            _handler.Enqueue(HttpStatusCode.OK, keys);
            using var client = NewClient();
            await client.PublicKeysAsync();

            Assert.AreEqual(
                SignatureCheck.Invalid,
                await client.VerifySignatureDetailedAsync(
                    SignedBy(forger, T0.AddDays(1))));
            Assert.AreEqual(
                SignatureCheck.Invalid,
                await client.VerifySignatureDetailedAsync(
                    SignedBy(forger, T0.AddDays(1).AddMinutes(1))));

            Assert.AreEqual(2, _handler.Requests.Count);
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

        private const string RedeemedMisconfigured =
            "{\"signature\":\"verified\",\"context\":\"misconfigured\","
            + "\"factors\":{\"transport\":\"misconfigured\","
            + "\"device\":\"verified\",\"browserip\":\"verified\","
            + "\"connectionip\":\"verified\",\"asn\":\"misconfigured\","
            + "\"platformname\":\"verified\","
            + "\"platformversion\":\"verified\","
            + "\"browsername\":\"verified\","
            + "\"browserversion\":\"verified\"},"
            + "\"verifiedAt\":\"2026-09-03T09:15:32Z\",\"secondsSinceVerified\":1}";

        private const string RedeemedFourBrowserFactors =
            "{\"signature\":\"verified\",\"context\":\"mismatch\","
            + "\"factors\":{\"transport\":\"verified\","
            + "\"device\":\"verified\",\"browserip\":\"verified\","
            + "\"connectionip\":\"verified\",\"asn\":\"verified\","
            + "\"platformname\":\"verified\","
            + "\"platformversion\":\"mismatch\","
            + "\"browsername\":\"mismatch\","
            + "\"browserversion\":\"misconfigured\"},"
            + "\"verifiedAt\":\"2026-09-16T09:15:32Z\","
            + "\"secondsSinceVerified\":2}";

        private const string RedeemedOldBrowserFactorOnly =
            "{\"signature\":\"verified\",\"context\":\"mismatch\","
            + "\"factors\":{\"transport\":\"verified\","
            + "\"browser\":\"mismatch\"},"
            + "\"verifiedAt\":\"2026-09-16T09:15:32Z\","
            + "\"secondsSinceVerified\":2}";

        private const string RedeemedInvalidDate =
            "{\"signature\":\"invalid\",\"context\":\"invaliddate\","
            + "\"verifiedAt\":\"2026-09-03T09:15:32Z\",\"secondsSinceVerified\":1}";

        /// <summary>
        /// A misconfigured factor says the checking service could not
        /// determine it, so it must NOT read as a mismatch. Reading it as one
        /// would report a replay indicator for something the identifier says
        /// nothing about, which is the failure this value exists to prevent.
        /// </summary>
        [TestMethod]
        public async Task Redeem_MisconfiguredFactors_AreNotMismatches()
        {
            _handler.Enqueue(HttpStatusCode.OK, RedeemedMisconfigured);
            using var client = NewClient();

            var result = await client.RedeemAsync(
                SignedAt(T0.AddDays(1)), "sealed", "abc123");

            Assert.AreEqual(ContextOutcome.Misconfigured, result.Context);
            Assert.AreEqual("misconfigured", result.ContextValue);
            Assert.IsNotNull(result.Factors);
            Assert.AreEqual(9, result.Factors!.Count);
            Assert.AreEqual(
                FactorOutcome.Misconfigured, result.Factors["transport"]);
            Assert.AreEqual(FactorOutcome.Misconfigured, result.Factors["asn"]);
            Assert.AreEqual(FactorOutcome.Verified, result.Factors["device"]);
            Assert.AreEqual(FactorOutcome.Verified, result.Factors["browserip"]);
            Assert.AreEqual(
                FactorOutcome.Verified, result.Factors["connectionip"]);
            Assert.AreEqual(
                FactorOutcome.Verified, result.Factors["platformname"]);
            Assert.AreEqual(
                FactorOutcome.Verified, result.Factors["platformversion"]);
            Assert.AreEqual(
                FactorOutcome.Verified, result.Factors["browsername"]);
            Assert.AreEqual(
                FactorOutcome.Verified, result.Factors["browserversion"]);
            Assert.AreEqual(
                0,
                System.Linq.Enumerable.Count(
                    result.Factors,
                    f => f.Value == FactorOutcome.Mismatch),
                "no factor should read as a mismatch");
        }

        /// <summary>
        /// The four factors that replaced the single browser factor in
        /// cloud release 4.4.38 are read by the names in
        /// <see cref="FactorName"/>, each with its own outcome, and a
        /// misconfigured one is not read as a mismatch.
        /// </summary>
        [TestMethod]
        public async Task Redeem_FourBrowserFactors_AreReadByName()
        {
            _handler.Enqueue(HttpStatusCode.OK, RedeemedFourBrowserFactors);
            using var client = NewClient();

            var result = await client.RedeemAsync(
                SignedAt(T0.AddDays(1)), "sealed", "abc123");

            Assert.AreEqual(ContextOutcome.Mismatch, result.Context);
            Assert.IsNotNull(result.Factors);
            CollectionAssert.AreEquivalent(
                FactorName.All.ToArray(),
                result.Factors!.Keys.ToArray(),
                "every factor the cloud sent is read");
            Assert.AreEqual(
                FactorOutcome.Verified,
                result.Factors[FactorName.PlatformName]);
            Assert.AreEqual(
                FactorOutcome.Mismatch,
                result.Factors[FactorName.PlatformVersion]);
            Assert.AreEqual(
                FactorOutcome.Mismatch,
                result.Factors[FactorName.BrowserName]);
            Assert.AreEqual(
                FactorOutcome.Misconfigured,
                result.Factors[FactorName.BrowserVersion]);
            Assert.IsFalse(result.Factors.ContainsKey("browser"));
        }

        /// <summary>
        /// An answer carrying only the old browser factor, as a cloud older
        /// than release 4.4.38 sends, populates none of the four new
        /// factors, so an old verdict is never read as though the operating
        /// system and browser had each been checked.
        /// </summary>
        [TestMethod]
        public async Task Redeem_OldBrowserFactorOnly_PopulatesNoNewFactor()
        {
            _handler.Enqueue(HttpStatusCode.OK, RedeemedOldBrowserFactorOnly);
            using var client = NewClient();

            var result = await client.RedeemAsync(
                SignedAt(T0.AddDays(1)), "sealed", "abc123");

            Assert.IsNotNull(result.Factors);
            foreach (var name in new[]
            {
                FactorName.PlatformName,
                FactorName.PlatformVersion,
                FactorName.BrowserName,
                FactorName.BrowserVersion,
            })
            {
                Assert.IsFalse(
                    result.Factors!.ContainsKey(name),
                    $"{name} should not be read from the old browser key");
            }
        }

        /// <summary>
        /// The factor names are the words the cloud writes, in the order
        /// the cloud lists them, and the old browser factor is not one.
        /// </summary>
        [TestMethod]
        public void FactorName_All_IsTheNineCloudNamesInOrder()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    "transport", "device", "browserip", "connectionip",
                    "asn", "platformname", "platformversion",
                    "browsername", "browserversion",
                },
                FactorName.All.ToArray());
        }

        /// <summary>
        /// An impossible creation date is a statement about the identifier
        /// rather than about the service, so it reports on its own with no
        /// factor block, and it is independent of the signature outcome.
        /// </summary>
        [TestMethod]
        public async Task Redeem_InvalidDate_IsReadAndCarriesNoFactors()
        {
            _handler.Enqueue(HttpStatusCode.OK, RedeemedInvalidDate);
            using var client = NewClient();

            var result = await client.RedeemAsync(
                SignedAt(T0.AddDays(1)), "sealed", "abc123");

            Assert.AreEqual(ContextOutcome.InvalidDate, result.Context);
            Assert.AreEqual("invaliddate", result.ContextValue);
            Assert.AreEqual(SignatureOutcome.Invalid, result.Signature);
            Assert.IsNull(result.Factors);
        }

        private const string RedeemedMismatch =
            "{\"signature\":\"verified\",\"context\":\"mismatch\","
            + "\"factors\":{\"transport\":\"verified\",\"device\":\"mismatch\","
            + "\"browserip\":\"verified\",\"connectionip\":\"mismatch\","
            + "\"asn\":\"verified\",\"platformname\":\"verified\","
            + "\"platformversion\":\"verified\","
            + "\"browsername\":\"verified\","
            + "\"browserversion\":\"verified\"},"
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
            Assert.AreEqual(9, result.Factors!.Count);
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
