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

using FiftyOne.Did.Model;
using Owid.Client;
using Owid.Client.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Did.Client
{
    /// <summary>
    /// Everything a server does with a 51Did against the 51Degrees cloud:
    /// fetch and cache the signing public keys, verify a signature offline
    /// against the key in force when the identifier was created, verify a
    /// signature through the cloud, and redeem a sealed creator context
    /// result with the account's licence key.
    /// <para>
    /// Creating a 51Did is not part of this client. Creation is the cloud
    /// <c>json</c> endpoint through the cloud request engine and pipeline,
    /// and a page creates from the browser because the identifier describes
    /// the browser's own connection. The <c>verify-context</c> and
    /// <c>verify-full</c> endpoints are browser calls for the same reason,
    /// so they are not here either. This client is the server side, which
    /// holds the licence key the browser never sees.
    /// </para>
    /// <para>
    /// Credentials never travel in a URL. The resource key is part of the
    /// route, as the endpoints accept, and the licence key travels only in
    /// a POST form body, because a query string is written to access logs.
    /// </para>
    /// <para>
    /// The key cache is per instance and safe to share across threads, so
    /// create one client for the process and reuse it.
    /// </para>
    /// </summary>
    public sealed class DidClient : IDisposable
    {
        /// <summary>
        /// The public cloud API base, used when no endpoint is given and
        /// <see cref="EndpointEnvironmentVariable"/> is not set.
        /// </summary>
        public const string DefaultEndpoint =
            "https://cloud.51degrees.com/api/v4/";

        /// <summary>
        /// The environment variable read for the API base when the
        /// constructor is given none, the same variable the cloud request
        /// engine honours. A host other than the public cloud is used for
        /// a privately hosted copy of the same service.
        /// </summary>
        public const string EndpointEnvironmentVariable = "FOD_CLOUD_API_URL";

        /// <summary>
        /// How old the cached key list may be before a lookup fetches it
        /// again. Keys are published up to three months ahead of their
        /// start, so a day is far inside that margin.
        /// </summary>
        public static readonly TimeSpan KeyCacheLifetime = TimeSpan.FromDays(1);

        /// <summary>
        /// The <c>User-Agent</c> every request carries, naming this package
        /// and its version.
        /// </summary>
        public static string UserAgent { get; } = BuildUserAgent();

        /// <summary>
        /// A key may be used for an identifier created this close to a
        /// boundary in the schedule, at either end, so the key in force a
        /// tolerance either side of the identifier's date is tried too.
        /// </summary>
        private static readonly TimeSpan BoundaryTolerance =
            TimeSpan.FromMinutes(15);

        // A guard against obviously malformed input, so the client does no
        // work and makes no call for a value that cannot be an identifier.
        // The figure is arbitrary and deliberately generous, well beyond
        // anything the cloud issues, because the length of a 51Did is the
        // cloud's business and not this package's.
        private const int MaximumEncodedLength = 4096;

        private readonly HttpClient _http;
        private readonly bool _ownsHttp;
        private readonly TimeProvider _time;
        private readonly SemaphoreSlim _keyLock = new SemaphoreSlim(1, 1);
        private readonly string? _licenceKey;
        private IReadOnlyList<DidPublicKey>? _keys;
        private DateTimeOffset _keysFetchedAt;
        private bool _disposed;

        /// <summary>
        /// Creates a client.
        /// </summary>
        /// <param name="resourceKey">
        /// The resource key, which is public by nature.
        /// </param>
        /// <param name="licenceKey">
        /// A licence key of the same account, server side only. Needed to
        /// redeem where the account holds licence keys, and sent only in
        /// the redeem form body.
        /// </param>
        /// <param name="endpoint">
        /// The API base including <c>/api/v4/</c>. When null the
        /// <see cref="EndpointEnvironmentVariable"/> is read, and when that
        /// is unset too <see cref="DefaultEndpoint"/> is used. A value with
        /// or without a trailing slash is accepted, and is normalised to
        /// end in exactly one.
        /// </param>
        /// <param name="httpClient">
        /// The transport to use, so a test can inject one. When null the
        /// client creates and owns one.
        /// </param>
        /// <param name="timeProvider">
        /// The clock the key cache ages against, so a test can inject one.
        /// When null the system clock is used.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="resourceKey"/> is empty or the
        /// endpoint is not an absolute URL.
        /// </exception>
        public DidClient(
            string resourceKey,
            string? licenceKey = null,
            string? endpoint = null,
            HttpClient? httpClient = null,
            TimeProvider? timeProvider = null)
        {
            if (string.IsNullOrWhiteSpace(resourceKey))
            {
                throw new ArgumentException(
                    "A resource key is required.", nameof(resourceKey));
            }
            ResourceKey = resourceKey;
            _licenceKey = string.IsNullOrEmpty(licenceKey) ? null : licenceKey;
            Endpoint = NormaliseEndpoint(
                endpoint
                ?? Environment.GetEnvironmentVariable(EndpointEnvironmentVariable));
            _http = httpClient ?? new HttpClient();
            _ownsHttp = httpClient is null;
            _time = timeProvider ?? TimeProvider.System;
        }

        /// <summary>The resource key the client sends.</summary>
        public string ResourceKey { get; }

        /// <summary>
        /// The API base every request is built on, ending in one slash.
        /// </summary>
        public string Endpoint { get; }

        /// <summary>
        /// Whether a licence key was given. The key itself is not exposed.
        /// </summary>
        public bool HasLicenceKey => _licenceKey is not null;

        /// <summary>
        /// The signing public keys the cloud publishes, fetched on first use
        /// and then answered from the cache. Use
        /// <see cref="PublicKeyForAsync"/> to pick the key for one
        /// identifier, which also refreshes the cache when it is stale.
        /// </summary>
        /// <param name="cancellationToken">Cancels the fetch.</param>
        /// <returns>The keys in start order.</returns>
        /// <exception cref="HttpRequestException">
        /// Thrown when the cloud cannot be reached or answers with a status
        /// other than 200.
        /// </exception>
        public async Task<IReadOnlyList<DidPublicKey>> PublicKeysAsync(
            CancellationToken cancellationToken = default)
        {
            await _keyLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_keys is null)
                {
                    await RefreshKeysLockedAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
                return _keys!;
            }
            finally
            {
                _keyLock.Release();
            }
        }

        /// <summary>
        /// The key in force when the identifier was created, being the
        /// entry whose start is latest on or before the identifier's date.
        /// The cache is fetched again, once, before answering when it holds
        /// no entry on or before the date, when the date is later than the
        /// newest start held, or when the cache is older than
        /// <see cref="KeyCacheLifetime"/>.
        /// </summary>
        /// <param name="fodId">The identifier.</param>
        /// <param name="cancellationToken">Cancels a fetch.</param>
        /// <returns>
        /// The key, or null when the date precedes the whole schedule.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="fodId"/> is null.
        /// </exception>
        /// <exception cref="HttpRequestException">
        /// Thrown when a fetch was needed and the cloud could not be
        /// reached or answered with a status other than 200.
        /// </exception>
        public async Task<DidPublicKey?> PublicKeyForAsync(
            FodId fodId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(fodId);
            var date = AsUtc(fodId.Date);
            var keys = await KeysCoveringAsync(date, cancellationToken)
                .ConfigureAwait(false);
            return InForceAt(keys, date);
        }

        /// <summary>
        /// Verifies the identifier's signature offline against the
        /// published keys, without a cloud call once the keys are cached.
        /// </summary>
        /// <param name="fodId">The identifier.</param>
        /// <param name="cancellationToken">Cancels a key fetch.</param>
        /// <returns>
        /// True only when the signature verifies under a key in force at
        /// the identifier's date. See
        /// <see cref="VerifySignatureDetailedAsync"/> for why a check did
        /// not pass.
        /// </returns>
        public async Task<bool> VerifySignatureAsync(
            FodId fodId,
            CancellationToken cancellationToken = default)
        {
            var check = await VerifySignatureDetailedAsync(
                fodId, cancellationToken).ConfigureAwait(false);
            return check == SignatureCheck.Verified;
        }

        /// <summary>
        /// Verifies the identifier's signature offline and says why when
        /// the check did not pass. The envelope must be OWID version 3 and
        /// the payload at least the base length for its type, being the
        /// five header bytes plus a 32 byte value, or a 16 byte value for a
        /// Random identifier. A longer payload carries a creator context
        /// section and is accepted, because the signature covers the whole
        /// payload. The keys tried are the one in force at the identifier's
        /// date and, where they differ, the ones in force a short tolerance
        /// before and after it, best first. Older keys are never tried,
        /// because a key belongs to its own period.
        /// </summary>
        /// <param name="fodId">The identifier.</param>
        /// <param name="cancellationToken">Cancels a key fetch.</param>
        /// <returns>The outcome.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="fodId"/> is null.
        /// </exception>
        /// <exception cref="HttpRequestException">
        /// Thrown when a key fetch was needed and the cloud could not be
        /// reached or answered with a status other than 200.
        /// </exception>
        public async Task<SignatureCheck> VerifySignatureDetailedAsync(
            FodId fodId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(fodId);
            if (fodId.Version != OwidVersion.Version3)
            {
                return SignatureCheck.UnsupportedVersion;
            }
            if (fodId.Payload is null
                || fodId.Payload.Length < BaseLength(fodId))
            {
                return SignatureCheck.InvalidLength;
            }
            var date = AsUtc(fodId.Date);
            var keys = await KeysCoveringAsync(date, cancellationToken)
                .ConfigureAwait(false);
            var candidates = CandidatesForDate(keys, date);
            if (candidates.Count == 0)
            {
                return SignatureCheck.NoKeyForDate;
            }
            foreach (var candidate in candidates)
            {
                using (var crypto = ECDsa.Create())
                {
                    crypto.ImportFromPem(candidate.PublicKeyPem);
                    if (fodId.Verify(crypto))
                    {
                        return SignatureCheck.Verified;
                    }
                }
            }
            return SignatureCheck.Invalid;
        }

        /// <summary>
        /// Verifies the identifier's signature through the cloud's verify
        /// endpoint, which needs no licence key and counts as one use.
        /// </summary>
        /// <param name="fodId">The identifier.</param>
        /// <param name="cancellationToken">Cancels the call.</param>
        /// <returns>Whether the cloud found the signature valid.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="fodId"/> is null.
        /// </exception>
        /// <exception cref="HttpRequestException">
        /// Thrown when the cloud cannot be reached or answers with an
        /// unexpected status.
        /// </exception>
        public Task<bool> VerifyAsync(
            FodId fodId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(fodId);
            return VerifyAsync(fodId.AsBase64Url(), cancellationToken);
        }

        /// <summary>
        /// Verifies a 51Did string's signature through the cloud's verify
        /// endpoint, which needs no licence key and counts as one use.
        /// Either base64 alphabet is accepted. The identifier is sent as
        /// <c>51did</c> and again as <c>owid</c>, the name the endpoint
        /// first went live under, so a service of either age answers.
        /// </summary>
        /// <param name="fodId">The identifier as base64.</param>
        /// <param name="cancellationToken">Cancels the call.</param>
        /// <returns>Whether the cloud found the signature valid.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown, with the cloud's message, when the cloud could not parse
        /// the value as a 51Did.
        /// </exception>
        /// <exception cref="HttpRequestException">
        /// Thrown when the cloud cannot be reached or answers with an
        /// unexpected status.
        /// </exception>
        public async Task<bool> VerifyAsync(
            string fodId,
            CancellationToken cancellationToken = default)
        {
            ValidateEncodedValue(fodId, nameof(fodId));
            // The documented parameter is 51did. The same value is sent
            // again as owid, the name the verify endpoint first went live
            // under, which a service that predates the 51did name reads
            // and a current one accepts as an alias, so both answer.
            var encoded = Uri.EscapeDataString(fodId);
            var request = NewRequest(
                HttpMethod.Get,
                "id/verify/" + Uri.EscapeDataString(ResourceKey)
                + "?51did=" + encoded + "&owid=" + encoded);
            var (status, body) = await SendAsync(request, cancellationToken)
                .ConfigureAwait(false);
            if (status == HttpStatusCode.OK || status == HttpStatusCode.BadRequest)
            {
                if (TryReadValid(body, out var valid))
                {
                    return valid;
                }
                if (status == HttpStatusCode.BadRequest)
                {
                    var errors = ReadErrors(body);
                    if (errors is not null)
                    {
                        throw new ArgumentException(errors, nameof(fodId));
                    }
                }
            }
            throw Unexpected("verify", status, body);
        }

        /// <summary>
        /// Redeems a sealed creator context result against the identifier
        /// it was made for, sending the licence key where one was given.
        /// Counts as one use, the second of the two a browser-based context
        /// check costs.
        /// </summary>
        /// <param name="fodId">The identifier the caller knows independently.</param>
        /// <param name="result">The sealed result the browser relayed.</param>
        /// <param name="challenge">
        /// The single-use challenge given to the verify call, or null where
        /// none was.
        /// </param>
        /// <param name="cancellationToken">Cancels the call.</param>
        /// <returns>The typed verdict.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="fodId"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown, with the cloud's message, when the cloud answered 400
        /// because the identifier was malformed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Thrown when the host answered 404, meaning it does not offer the
        /// creator context.
        /// </exception>
        /// <exception cref="HttpRequestException">
        /// Thrown when the cloud cannot be reached or answers with any
        /// other unexpected status.
        /// </exception>
        public Task<RedeemResult> RedeemAsync(
            FodId fodId,
            string result,
            string? challenge = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(fodId);
            return RedeemAsync(
                fodId.AsBase64Url(), result, challenge, cancellationToken);
        }

        /// <summary>
        /// Redeems a sealed creator context result against a 51Did string,
        /// in either base64 alphabet. See
        /// <see cref="RedeemAsync(FodId, string, string, CancellationToken)"/>.
        /// </summary>
        /// <param name="fodId">The identifier as base64.</param>
        /// <param name="result">The sealed result the browser relayed.</param>
        /// <param name="challenge">
        /// The single-use challenge given to the verify call, or null where
        /// none was.
        /// </param>
        /// <param name="cancellationToken">Cancels the call.</param>
        /// <returns>The typed verdict.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="fodId"/> is empty, or with the
        /// cloud's message when the cloud answered 400.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Thrown when the host answered 404, meaning it does not offer the
        /// creator context.
        /// </exception>
        /// <exception cref="HttpRequestException">
        /// Thrown when the cloud cannot be reached or answers with any
        /// other unexpected status.
        /// </exception>
        public async Task<RedeemResult> RedeemAsync(
            string fodId,
            string result,
            string? challenge = null,
            CancellationToken cancellationToken = default)
        {
            ValidateEncodedValue(fodId, nameof(fodId));
            // Everything travels in the form body, the resource key
            // included, because the redeem endpoint's POST route is the bare
            // path and reads its parameters from the form. Nothing here is
            // written to an access log.
            var form = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("resource", ResourceKey),
                new KeyValuePair<string, string>("51did", fodId),
                new KeyValuePair<string, string>("result", result ?? string.Empty),
                new KeyValuePair<string, string>("challenge", challenge ?? string.Empty),
            };
            if (_licenceKey is not null)
            {
                form.Add(new KeyValuePair<string, string>("license", _licenceKey));
            }
            var request = NewRequest(HttpMethod.Post, "id/redeem");
            request.Content = new FormUrlEncodedContent(form);
            var (status, body) = await SendAsync(request, cancellationToken)
                .ConfigureAwait(false);
            switch (status)
            {
                case HttpStatusCode.OK:
                case HttpStatusCode.ServiceUnavailable:
                    return RedeemResult.FromResponse((int)status, body);
                case HttpStatusCode.BadRequest:
                    throw new ArgumentException(
                        ReadErrors(body) ?? body, nameof(fodId));
                case HttpStatusCode.NotFound:
                    throw new NotSupportedException(
                        $"The service at {Endpoint} does not support the "
                        + "51Did creator context.");
                default:
                    throw Unexpected("redeem", status, body);
            }
        }

        /// <summary>
        /// Releases the transport when this client created it.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            if (_ownsHttp)
            {
                _http.Dispose();
            }
            _keyLock.Dispose();
        }

        /// <summary>
        /// The entry in force at the moment, being the one whose start is
        /// latest on or before it, or null when the moment precedes every
        /// entry.
        /// </summary>
        /// <param name="keys">The schedule, in any order.</param>
        /// <param name="at">The moment.</param>
        /// <returns>The entry in force, or null.</returns>
        public static DidPublicKey? InForceAt(
            IReadOnlyList<DidPublicKey> keys,
            DateTime at)
        {
            ArgumentNullException.ThrowIfNull(keys);
            DidPublicKey? best = null;
            foreach (var key in keys)
            {
                if (key is null || key.StartsAt > at)
                {
                    continue;
                }
                if (best is null || key.StartsAt > best.StartsAt)
                {
                    best = key;
                }
            }
            return best;
        }

        /// <summary>
        /// The keys that may have signed something created at the moment,
        /// best first: the entry in force, then the entries in force a
        /// short tolerance earlier and later where those differ. Empty when
        /// the moment precedes the whole schedule.
        /// </summary>
        /// <param name="keys">The schedule, in any order.</param>
        /// <param name="at">The creation moment.</param>
        /// <returns>The keys to try.</returns>
        public static IReadOnlyList<DidPublicKey> CandidatesForDate(
            IReadOnlyList<DidPublicKey> keys,
            DateTime at)
        {
            ArgumentNullException.ThrowIfNull(keys);
            var candidates = new List<DidPublicKey>(3);
            AddIfNew(candidates, InForceAt(keys, at));
            AddIfNew(candidates, InForceAt(keys, Shift(at, -BoundaryTolerance)));
            AddIfNew(candidates, InForceAt(keys, Shift(at, BoundaryTolerance)));
            return candidates;
        }

        /// <summary>
        /// Reads the key endpoint's answer, a JSON array of objects each
        /// carrying <c>startsAt</c> (or <c>created</c> on a service that
        /// predates <c>startsAt</c>) and <c>publicKey</c>. Other fields are
        /// ignored.
        /// </summary>
        /// <param name="json">The response body.</param>
        /// <returns>The keys in start order.</returns>
        /// <exception cref="FormatException">
        /// Thrown when the body is not an array of such objects.
        /// </exception>
        /// <exception cref="JsonException">
        /// Thrown when the body is not JSON.
        /// </exception>
        public static IReadOnlyList<DidPublicKey> ParseKeys(string json)
        {
            using (var document = JsonDocument.Parse(json ?? string.Empty))
            {
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    throw new FormatException(
                        "The 51Did key endpoint did not answer with a JSON "
                        + "array: " + Truncate(json));
                }
                var keys = new List<DidPublicKey>();
                foreach (var element in document.RootElement.EnumerateArray())
                {
                    var start = ReadString(element, "startsAt")
                        ?? ReadString(element, "created");
                    var pem = ReadString(element, "publicKey");
                    if (start is null || pem is null)
                    {
                        throw new FormatException(
                            "A 51Did key entry lacks its start or public "
                            + "key: " + Truncate(element.GetRawText()));
                    }
                    keys.Add(new DidPublicKey(ParseUtc(start), pem));
                }
                keys.Sort((a, b) => a.StartsAt.CompareTo(b.StartsAt));
                return keys.AsReadOnly();
            }
        }

        private async Task<IReadOnlyList<DidPublicKey>> KeysCoveringAsync(
            DateTime date,
            CancellationToken cancellationToken)
        {
            await _keyLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_keys is null || NeedsRefreshLocked(_keys, date))
                {
                    await RefreshKeysLockedAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
                return _keys!;
            }
            finally
            {
                _keyLock.Release();
            }
        }

        private bool NeedsRefreshLocked(
            IReadOnlyList<DidPublicKey> keys,
            DateTime date)
        {
            if (_time.GetUtcNow() - _keysFetchedAt > KeyCacheLifetime)
            {
                return true;
            }
            var inForce = InForceAt(keys, date);
            if (inForce is null)
            {
                return true;
            }
            var newest = DateTime.MinValue;
            foreach (var key in keys)
            {
                if (key.StartsAt > newest)
                {
                    newest = key.StartsAt;
                }
            }
            return date > newest;
        }

        private async Task RefreshKeysLockedAsync(
            CancellationToken cancellationToken)
        {
            var request = NewRequest(
                HttpMethod.Get,
                "id/key/" + Uri.EscapeDataString(ResourceKey));
            var (status, body) = await SendAsync(request, cancellationToken)
                .ConfigureAwait(false);
            if (status != HttpStatusCode.OK)
            {
                throw Unexpected("key", status, body);
            }
            _keys = ParseKeys(body);
            _keysFetchedAt = _time.GetUtcNow();
        }

        private HttpRequestMessage NewRequest(HttpMethod method, string path)
        {
            var request = new HttpRequestMessage(method, new Uri(Endpoint + path));
            request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
            return request;
        }

        private async Task<(HttpStatusCode Status, string Body)> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            using (request)
            using (var response = await _http
                .SendAsync(request, cancellationToken).ConfigureAwait(false))
            {
                var body = await response.Content
                    .ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return (response.StatusCode, body);
            }
        }

        private static HttpRequestException Unexpected(
            string endpoint,
            HttpStatusCode status,
            string body)
            => new HttpRequestException(
                $"The 51Did {endpoint} endpoint answered {(int)status} "
                + $"({status}): {Truncate(body)}",
                null,
                status);

        private static int BaseLength(FodId fodId)
            => FodId.HeaderLength + (fodId.Type == IdType.Random
                ? FodId.GuidLength
                : FodId.HashLength);

        private static void ValidateEncodedValue(string fodId, string paramName)
        {
            if (string.IsNullOrWhiteSpace(fodId))
            {
                throw new ArgumentException("A 51Did is required.", paramName);
            }
            if (fodId.Length > MaximumEncodedLength)
            {
                throw new ArgumentException(
                    "The value is too long to be a 51Did.", paramName);
            }
        }

        private static DateTime AsUtc(DateTime date)
            => date.Kind == DateTimeKind.Local
                ? date.ToUniversalTime()
                : DateTime.SpecifyKind(date, DateTimeKind.Utc);

        // Saturating, because the date comes off the wire as a raw count of
        // minutes and could sit within the tolerance of either extreme.
        private static DateTime Shift(DateTime at, TimeSpan by)
        {
            var remaining = by > TimeSpan.Zero
                ? DateTime.MaxValue - at
                : at - DateTime.MinValue;
            return by.Duration() > remaining
                ? (by > TimeSpan.Zero ? DateTime.MaxValue : DateTime.MinValue)
                : at + by;
        }

        private static void AddIfNew(List<DidPublicKey> candidates, DidPublicKey? key)
        {
            if (key is null || candidates.Contains(key))
            {
                return;
            }
            candidates.Add(key);
        }

        private static string NormaliseEndpoint(string? endpoint)
        {
            var value = string.IsNullOrWhiteSpace(endpoint)
                ? DefaultEndpoint
                : endpoint!.Trim();
            value = value.TrimEnd('/') + "/";
            if (Uri.TryCreate(value, UriKind.Absolute, out _) == false)
            {
                throw new ArgumentException(
                    $"The endpoint '{value}' is not an absolute URL.",
                    nameof(endpoint));
            }
            return value;
        }

        private static DateTime ParseUtc(string value)
            => DateTime.Parse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

        private static bool TryReadValid(string body, out bool valid)
        {
            valid = false;
            try
            {
                using (var document = JsonDocument.Parse(body ?? string.Empty))
                {
                    var root = document.RootElement;
                    if (root.ValueKind == JsonValueKind.Object
                        && root.TryGetProperty("valid", out var element)
                        && (element.ValueKind == JsonValueKind.True
                            || element.ValueKind == JsonValueKind.False))
                    {
                        valid = element.GetBoolean();
                        return true;
                    }
                }
            }
            catch (JsonException)
            {
            }
            return false;
        }

        /// <summary>
        /// The cloud's <c>errors</c> array joined into one message, or null
        /// when the body carries none.
        /// </summary>
        private static string? ReadErrors(string body)
        {
            try
            {
                using (var document = JsonDocument.Parse(body ?? string.Empty))
                {
                    var root = document.RootElement;
                    if (root.ValueKind != JsonValueKind.Object
                        || root.TryGetProperty("errors", out var errors) == false
                        || errors.ValueKind != JsonValueKind.Array)
                    {
                        return null;
                    }
                    var messages = new List<string>();
                    foreach (var error in errors.EnumerateArray())
                    {
                        messages.Add(error.ValueKind == JsonValueKind.String
                            ? error.GetString() ?? string.Empty
                            : error.GetRawText());
                    }
                    return messages.Count == 0 ? null : string.Join(" ", messages);
                }
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string? ReadString(JsonElement element, string name)
            => element.TryGetProperty(name, out var value)
                && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        private static string Truncate(string? value)
        {
            const int Limit = 500;
            if (value is null)
            {
                return string.Empty;
            }
            return value.Length <= Limit ? value : value.Substring(0, Limit) + "...";
        }

        private static string BuildUserAgent()
        {
            var assembly = typeof(DidClient).Assembly;
            var version = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
            if (string.IsNullOrWhiteSpace(version))
            {
                version = assembly.GetName().Version?.ToString() ?? "0.0";
            }
            // The informational version may carry a build label after a
            // plus sign, which is not part of the version.
            var plus = version!.IndexOf('+');
            if (plus >= 0)
            {
                version = version.Substring(0, plus);
            }
            return "FiftyOne.Did/" + version;
        }
    }
}
