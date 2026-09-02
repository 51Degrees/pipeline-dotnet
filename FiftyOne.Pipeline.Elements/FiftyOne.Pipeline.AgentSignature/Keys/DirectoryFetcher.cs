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
using FiftyOne.Pipeline.AgentSignature.Verification;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.AgentSignature.Keys
{
    /// <summary>
    /// Fetches an agent's key directory, agent card or card registry over
    /// HTTPS.
    /// </summary>
    internal sealed class DirectoryFetcher
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly Func<DateTimeOffset> _clock;
        private readonly string _userAgent;
        private readonly int _maxResponseBytes;

        /// <summary>
        /// Construct a fetcher.
        /// </summary>
        /// <param name="httpClient">
        /// The client to make requests with.
        /// </param>
        /// <param name="logger">The logger.</param>
        /// <param name="clock">
        /// The source of the current time, which the tests replace.
        /// </param>
        /// <param name="maxResponseBytes">
        /// The number of bytes read from a response before it is abandoned.
        /// </param>
        public DirectoryFetcher(
            HttpClient httpClient,
            ILogger logger,
            Func<DateTimeOffset> clock,
            int maxResponseBytes)
        {
            _httpClient = httpClient;
            _logger = logger;
            _clock = clock;
            _maxResponseBytes = maxResponseBytes;
            var version = typeof(DirectoryFetcher)
                .GetTypeInfo().Assembly.GetName().Version;
            _userAgent = string.Format(
                CultureInfo.InvariantCulture,
                "51Degrees-Pipeline-AgentSignature/{0}",
                version == null ? "0.0.0" : version.ToString());
        }

        /// <summary>
        /// Obtain the keys the given URL leads to.
        /// </summary>
        /// <param name="url">
        /// The key URL that the 'Signature-Agent' member resolved to.
        /// </param>
        /// <param name="type">
        /// The 'Signature-Agent' member type, which decides what the URL
        /// leads to.
        /// </param>
        /// <param name="token">
        /// A token that cancels when the fetch has taken too long.
        /// </param>
        /// <returns>
        /// The keys, or an entry describing why they could not be obtained.
        /// Every failure this method expects, being a network failure, a
        /// timeout or a document it cannot read, comes back as an entry
        /// rather than as an exception. The caller in
        /// <see cref="DirectoryCache"/> catches anything else, so that
        /// nothing an agent sends can throw into the pipeline.
        /// </returns>
        public async Task<DirectoryEntry> FetchAsync(
            string url,
            string type,
            CancellationToken token)
        {
            try
            {
                if (IsSafeUrl(url) == false)
                {
                    return Failed(url, "the address may not be fetched");
                }
                if (string.Equals(
                    type, Constants.AGENT_TYPE_CIMD,
                    StringComparison.Ordinal))
                {
                    return await FetchCardAndKeysAsync(url, token)
                        .ConfigureAwait(false);
                }
                return await FetchDirectoryAsync(url, type, null, token)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (IsNetworkFailure(exception))
            {
                return Failed(url, exception.Message);
            }
        }

        /// <summary>
        /// Read a key directory that was carried inline in a 'data:' URI, so
        /// that no request is made at all.
        /// </summary>
        /// <param name="url">
        /// The 'data:' URI, used only in the failure message.
        /// </param>
        /// <param name="content">The bytes the URI carried.</param>
        /// <returns>The keys, or a failure entry.</returns>
        public DirectoryEntry ReadInline(string url, byte[] content)
        {
            string json;
            try
            {
                json = Encoding.UTF8.GetString(content);
            }
            catch (ArgumentException exception)
            {
                return Failed(url, exception.Message);
            }
            if (KeyDirectory.TryParse(json, out var directory) == false)
            {
                return Failed(url, "the directory could not be read");
            }
            ReportMillisecondTimes(url, directory);
            return DirectoryEntry.Succeeded(directory, null, _clock(), null);
        }

        /// <summary>
        /// Fetch one agent card.
        /// </summary>
        /// <param name="url">The card URL.</param>
        /// <param name="token">
        /// A token that cancels when the fetch has taken too long.
        /// </param>
        /// <returns>The card, or null when it could not be read.</returns>
        public async Task<AgentCard> FetchCardAsync(
            string url,
            CancellationToken token)
        {
            try
            {
                using (var response = await SendAsync(
                    url, Constants.JSON_MEDIA_TYPE, token)
                    .ConfigureAwait(false))
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        LogCardFailure(
                            url,
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "the response status was {0}",
                                (int)response.StatusCode));
                        return null;
                    }
                    // The registry draft says a card is returned directly
                    // with a 200, so a card reached through a redirect is
                    // not the card that was asked for.
                    if (WasRedirected(response, url))
                    {
                        LogCardFailure(url, "the request was redirected");
                        return null;
                    }
                    var bytes = await ReadBodyAsync(response, token)
                        .ConfigureAwait(false);
                    if (bytes == null)
                    {
                        LogCardFailure(
                            url, "the card was too long to read");
                        return null;
                    }
                    var body = Encoding.UTF8.GetString(bytes);
                    if (AgentCard.TryParse(body, url, out var card) == false)
                    {
                        LogCardFailure(url, "the card could not be read");
                        return null;
                    }
                    return card;
                }
            }
            catch (Exception exception) when (IsNetworkFailure(exception))
            {
                LogCardFailure(url, exception.Message);
                return null;
            }
        }

        /// <summary>
        /// Fetch the text of a registry, being a list of agent card URLs
        /// with one URL per line.
        /// </summary>
        /// <param name="url">The registry URL.</param>
        /// <param name="token">
        /// A token that cancels when the fetch has taken too long.
        /// </param>
        /// <returns>
        /// The card URLs the registry lists, which is empty when the
        /// registry could not be read.
        /// </returns>
        public async Task<IList<string>> FetchRegistryAsync(
            string url,
            CancellationToken token)
        {
            try
            {
                using (var response = await SendAsync(url, "text/plain", token)
                    .ConfigureAwait(false))
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        LogRegistryFailure(
                            url,
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "the response status was {0}",
                                (int)response.StatusCode));
                        return new List<string>();
                    }
                    var bytes = await ReadBodyAsync(response, token)
                        .ConfigureAwait(false);
                    if (bytes == null)
                    {
                        LogRegistryFailure(
                            url, "the registry was too long to read");
                        return new List<string>();
                    }
                    return ParseRegistry(Encoding.UTF8.GetString(bytes));
                }
            }
            catch (Exception exception) when (IsNetworkFailure(exception))
            {
                LogRegistryFailure(url, exception.Message);
                return new List<string>();
            }
        }

        /// <summary>
        /// Read the text of a registry into the card URLs it lists. Blank
        /// lines are skipped and everything after a '#' on a line is a
        /// comment. Each URL is checked the same way as an address from a
        /// header, because although the registry's own address is configured
        /// by the operator, the lines are whatever the registry served, so a
        /// registry that has been tampered with must not be able to point
        /// this element at an address inside the network.
        /// </summary>
        /// <param name="text">The registry text.</param>
        /// <returns>The card URLs.</returns>
        public static IList<string> ParseRegistry(string text)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                return result;
            }
            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine;
                var comment = line.IndexOf('#');
                if (comment >= 0)
                {
                    line = line.Substring(0, comment);
                }
                line = line.Trim();
                if (line.Length == 0)
                {
                    continue;
                }
                if (IsSafeUrl(line))
                {
                    result.Add(line);
                }
            }
            return result;
        }

        private async Task<DirectoryEntry> FetchCardAndKeysAsync(
            string url,
            CancellationToken token)
        {
            var card = await FetchCardAsync(url, token)
                .ConfigureAwait(false);
            if (card == null)
            {
                return Failed(url, "the agent card could not be read");
            }
            if (card.Jwks != null)
            {
                return DirectoryEntry.Succeeded(
                    card.Jwks, card, _clock(), null);
            }
            if (string.IsNullOrEmpty(card.JwksUri))
            {
                return Failed(url, "the agent card names no keys");
            }
            // The card was fetched because of a header the sender wrote,
            // so the address it names is checked in the same way as the
            // header's own, rather than trusted because it arrived in a
            // document rather than in a header.
            if (IsSafeUrl(card.JwksUri) == false)
            {
                return Failed(
                    url, "the agent card names an address that may not " +
                    "be fetched");
            }
            return await FetchDirectoryAsync(
                card.JwksUri, Constants.AGENT_TYPE_JWKS_URI, card, token)
                .ConfigureAwait(false);
        }

        private async Task<DirectoryEntry> FetchDirectoryAsync(
            string url,
            string type,
            AgentCard card,
            CancellationToken token)
        {
            using (var response = await SendAsync(
                url, Constants.DIRECTORY_MEDIA_TYPE, token)
                .ConfigureAwait(false))
            {
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    return Failed(
                        url,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "the response status was {0}",
                            (int)response.StatusCode));
                }

                // The well known path is what ties a key set to a domain,
                // so a response that arrived somewhere else is not the
                // document that was asked for. The protocol draft says a
                // verifier must not follow a redirect here.
                if (WasRedirected(response, url))
                {
                    return Failed(url, "the request was redirected");
                }

                var mediaType = response.Content.Headers.ContentType?.MediaType;
                if (IsAcceptableMediaType(mediaType, type) == false)
                {
                    return Failed(
                        url,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "the media type was '{0}'",
                            mediaType ?? "absent"));
                }

                var body = await ReadBodyAsync(response, token)
                    .ConfigureAwait(false);
                if (body == null)
                {
                    return Failed(url, "the document was too long to read");
                }
                var json = Encoding.UTF8.GetString(body);
                if (KeyDirectory.TryParse(json, out var directory) == false)
                {
                    return Failed(url, "the directory could not be read");
                }
                ReportMillisecondTimes(url, directory);

                if (TryVerifyResponseSignature(
                    url, response, body, directory, out var failure) == false)
                {
                    return Failed(url, failure);
                }

                return DirectoryEntry.Succeeded(
                    directory,
                    card,
                    _clock(),
                    response.Headers.CacheControl?.MaxAge);
            }
        }

        private static bool IsAcceptableMediaType(
            string mediaType,
            string type)
        {
            if (string.Equals(
                mediaType,
                Constants.DIRECTORY_MEDIA_TYPE,
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            // A JWKS the agent names directly is an ordinary JSON document,
            // so plain JSON is accepted for it.
            return string.Equals(
                    type,
                    Constants.AGENT_TYPE_JWKS_URI,
                    StringComparison.Ordinal) &&
                string.Equals(
                    mediaType,
                    Constants.JSON_MEDIA_TYPE,
                    StringComparison.OrdinalIgnoreCase);
        }

        private Task<HttpResponseMessage> SendAsync(
            string url,
            string accept,
            CancellationToken token)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("Accept", accept);
            request.Headers.TryAddWithoutValidation("User-Agent", _userAgent);
            return _httpClient.SendAsync(request, token);
        }

        /// <summary>
        /// Whether the response came from somewhere other than the address
        /// that was asked for.
        /// </summary>
        /// <remarks>
        /// The two addresses are compared as URIs rather than as text.
        /// Comparing the text would report a redirect for an address that
        /// merely spells the host in capitals or writes the default port
        /// out in full, because those are the differences the framework
        /// takes out when it makes the request.
        /// </remarks>
        /// <param name="response">The response.</param>
        /// <param name="url">The address that was asked for.</param>
        /// <returns>True when the response came from somewhere else.</returns>
        private static bool WasRedirected(
            HttpResponseMessage response,
            string url)
        {
            var finalUri = response.RequestMessage?.RequestUri;
            if (finalUri == null)
            {
                return false;
            }
            return Uri.TryCreate(url, UriKind.Absolute, out var asked)
                ? finalUri.Equals(asked) == false
                : true;
        }

        /// <summary>
        /// Read a response body, giving up once more bytes have arrived
        /// than the limit allows. The address fetched is chosen by whoever
        /// sent the request, so the body cannot be read into memory whole
        /// on the promise that a well behaved server sends a small one.
        /// </summary>
        /// <param name="response">The response.</param>
        /// <param name="token">A token that cancels the read.</param>
        /// <returns>
        /// The bytes, or null when the body is longer than the limit.
        /// </returns>
        private async Task<byte[]> ReadBodyAsync(
            HttpResponseMessage response,
            CancellationToken token)
        {
            // A declared length over the limit is refused before anything
            // is read, and the read below still counts the bytes, because
            // a response may declare no length at all or declare one it
            // then exceeds.
            if (response.Content.Headers.ContentLength.HasValue &&
                response.Content.Headers.ContentLength.Value >
                    _maxResponseBytes)
            {
                return null;
            }
            using (var stream = await response.Content
                .ReadAsStreamAsync().ConfigureAwait(false))
            using (var held = new MemoryStream())
            {
                var buffer = new byte[8192];
                var total = 0;
                while (true)
                {
                    var read = await stream
                        .ReadAsync(buffer, 0, buffer.Length, token)
                        .ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }
                    total += read;
                    if (total > _maxResponseBytes)
                    {
                        return null;
                    }
                    held.Write(buffer, 0, read);
                }
                return held.ToArray();
            }
        }

        /// <summary>
        /// Whether a URL is one this element is willing to fetch. The
        /// address comes from a header the sender wrote, or from a
        /// document fetched because of one, so it is checked before a
        /// request is made rather than trusted.
        /// </summary>
        /// <remarks>
        /// The check refuses anything that is not HTTPS, and refuses an
        /// address written as an IP address in a range that only appears
        /// inside a network, which is what an attacker reaches for when
        /// trying to make a server fetch its own internal services. It
        /// cannot refuse a name that resolves to such an address, because
        /// the name is resolved later when the connection is made. Where
        /// the element faces the public internet, point it at an outbound
        /// proxy that enforces an allow list as well.
        /// </remarks>
        /// <param name="url">The URL.</param>
        /// <returns>True when the URL may be fetched.</returns>
        public static bool IsSafeUrl(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) == false ||
                string.Equals(
                    uri.Scheme,
                    Uri.UriSchemeHttps,
                    StringComparison.OrdinalIgnoreCase) == false)
            {
                return false;
            }
            // Anything before an '@' in the authority names a user rather
            // than a host, and is a well worn way of writing a URL that
            // reads as one host and connects to another.
            if (string.IsNullOrEmpty(uri.UserInfo) == false)
            {
                return false;
            }
            if (IPAddress.TryParse(uri.Host.Trim('[', ']'), out var address))
            {
                return IsPrivateAddress(address) == false;
            }
            return true;
        }

        /// <summary>
        /// Whether an address is one that only appears inside a network,
        /// covering loopback, link local (which carries the address cloud
        /// providers answer machine credentials on), the private ranges
        /// and the unspecified address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <returns>True when the address is not a public one.</returns>
        private static bool IsPrivateAddress(IPAddress address)
        {
            if (IPAddress.IsLoopback(address) ||
                address.Equals(IPAddress.Any) ||
                address.Equals(IPAddress.IPv6Any))
            {
                return true;
            }
            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                var octets = address.GetAddressBytes();
                return octets[0] == 10 ||
                    octets[0] == 127 ||
                    (octets[0] == 172 &&
                        octets[1] >= 16 && octets[1] <= 31) ||
                    (octets[0] == 192 && octets[1] == 168) ||
                    (octets[0] == 169 && octets[1] == 254) ||
                    (octets[0] == 100 &&
                        octets[1] >= 64 && octets[1] <= 127);
            }
            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
                {
                    return true;
                }
                var bytes = address.GetAddressBytes();
                // fc00::/7 is the range held back for use inside one
                // network, and an IPv4 address mapped into IPv6 is checked
                // as the IPv4 address it carries.
                if ((bytes[0] & 0xFE) == 0xFC)
                {
                    return true;
                }
                if (address.IsIPv4MappedToIPv6)
                {
                    return IsPrivateAddress(address.MapToIPv4());
                }
            }
            return false;
        }

        /// <summary>
        /// Check the signature a key directory response carries over itself,
        /// which the directory draft says a server SHOULD send. A response
        /// that carries no such signature is accepted, because the agents
        /// signing today do not all send one.
        /// </summary>
        internal bool TryVerifyResponseSignature(
            string url,
            HttpResponseMessage response,
            byte[] body,
            KeyDirectory directory,
            out string failure)
        {
            failure = null;
            var signatureHeader = GetHeader(response, "signature");
            var inputHeader = GetHeader(response, "signature-input");
            if (signatureHeader == null && inputHeader == null)
            {
                return true;
            }
            if (signatureHeader == null || inputHeader == null)
            {
                failure = "the response signature headers were incomplete";
                return false;
            }
            if (StructuredFieldParser.TryParseDictionary(
                    inputHeader, out var input) == false ||
                StructuredFieldParser.TryParseDictionary(
                    signatureHeader, out var signature) == false ||
                SignatureCandidate.TryBuild(
                    input, signature, out var candidates) == false)
            {
                failure = "the response signature headers could not be read";
                return false;
            }

            SignatureCandidate candidate = null;
            foreach (var possible in candidates)
            {
                if (string.Equals(
                    possible.Tag,
                    Constants.TAG_DIRECTORY,
                    StringComparison.Ordinal))
                {
                    candidate = possible;
                    break;
                }
            }
            if (candidate == null)
            {
                // The response is signed for some other purpose, which says
                // nothing about this directory either way.
                return true;
            }

            var digestHeader = GetHeader(response, "content-digest");
            if (digestHeader != null &&
                DigestMatches(digestHeader, body) == false)
            {
                failure = "the content digest did not match the body";
                return false;
            }

            var resolver = new ResponseComponentResolver(url, response);
            if (SignatureBase.TryBuild(
                candidate.CoveredComponents,
                candidate.SignatureParams,
                resolver,
                out var signatureBase) == false)
            {
                failure = "a covered component of the response signature " +
                    "could not be rebuilt";
                return false;
            }

            var key = directory.FindKey(candidate.KeyId);
            if (key == null)
            {
                failure = "the response signature named a key the " +
                    "directory does not hold";
                return false;
            }
            var algorithm = SignatureVerifier.ResolveAlgorithm(
                key, candidate.Algorithm);
            if (algorithm.Supported == false ||
                SignatureVerifier.Verify(
                    algorithm.Name,
                    key,
                    Encoding.ASCII.GetBytes(signatureBase),
                    candidate.Signature) == false)
            {
                failure = "the response signature did not check out";
                return false;
            }
            return true;
        }

        private static bool DigestMatches(string header, byte[] body)
        {
            if (StructuredFieldParser.TryParseDictionary(
                header, out var dictionary) == false)
            {
                return false;
            }
            if (dictionary.TryGetValue("sha-256", out var member) == false ||
                member.IsInnerList ||
                (member.Item.Value is byte[] expected) == false)
            {
                // Only SHA-256 is checked, because that is what the
                // directory draft uses. A digest this element cannot check
                // is not evidence against the response.
                return true;
            }
            using (var hash = SHA256.Create())
            {
                var actual = hash.ComputeHash(body);
                if (actual.Length != expected.Length)
                {
                    return false;
                }
                var difference = 0;
                for (var i = 0; i < actual.Length; i++)
                {
                    difference |= actual[i] ^ expected[i];
                }
                return difference == 0;
            }
        }

        /// <summary>
        /// Read one header from a response, looking at both the message
        /// headers and the content headers because which collection a header
        /// lands in depends on whether the framework knows the header.
        /// </summary>
        internal static string GetHeader(
            HttpResponseMessage response,
            string name)
        {
            if (response.Headers.TryGetValues(name, out var values))
            {
                return string.Join(", ", values);
            }
            if (response.Content != null &&
                response.Content.Headers.TryGetValues(name, out var content))
            {
                return string.Join(", ", content);
            }
            return null;
        }

        private DirectoryEntry Failed(string url, string reason)
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(string.Format(
                    CultureInfo.InvariantCulture,
                    Messages.LogDirectoryFetchFailed,
                    url,
                    reason));
            }
            return DirectoryEntry.Failed(_clock(), reason);
        }

        private void LogCardFailure(string url, string reason)
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(string.Format(
                    CultureInfo.InvariantCulture,
                    Messages.LogCardFetchFailed,
                    url,
                    reason));
            }
        }

        private void LogRegistryFailure(string url, string reason)
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(string.Format(
                    CultureInfo.InvariantCulture,
                    Messages.LogRegistryFetchFailed,
                    url,
                    reason));
            }
        }

        private void ReportMillisecondTimes(
            string url,
            KeyDirectory directory)
        {
            if (directory.TimesWereInMilliseconds &&
                _logger != null &&
                _logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(string.Format(
                    CultureInfo.InvariantCulture,
                    Messages.LogMillisecondKeyTimes,
                    url));
            }
        }

        private static bool IsNetworkFailure(Exception exception)
        {
            return exception is HttpRequestException ||
                exception is OperationCanceledException ||
                exception is InvalidOperationException ||
                exception is ObjectDisposedException ||
                exception is System.IO.IOException ||
                exception is UriFormatException;
        }

        /// <summary>
        /// Resolves the covered components of the signature a key directory
        /// response carries over itself. The '@authority;req' component
        /// refers to the request this element sent, and the other components
        /// are response headers.
        /// </summary>
        private sealed class ResponseComponentResolver : IComponentResolver
        {
            private readonly string _authority;
            private readonly HttpResponseMessage _response;

            public ResponseComponentResolver(
                string url,
                HttpResponseMessage response)
            {
                _authority = Uri.TryCreate(
                    url, UriKind.Absolute, out var uri)
                    ? SignatureBase.BuildAuthority(uri.Authority, uri.Scheme)
                    : null;
                _response = response;
            }

            public bool TryResolve(
                string name,
                SfItem component,
                out string value)
            {
                value = null;
                if (string.Equals(
                    name, "@authority", StringComparison.Ordinal))
                {
                    // The directory draft has the response signature cover
                    // the authority of the request, written ';req'.
                    if (component.TryGetParameter("req", out _) == false)
                    {
                        return false;
                    }
                    value = _authority;
                    return value != null;
                }
                if (name.Length > 0 && name[0] == '@')
                {
                    return false;
                }
                if (component.TryGetParameter("req", out _))
                {
                    // A request header, which this element does not carry
                    // back from the request it sent.
                    return false;
                }
                value = GetHeader(_response, name)?.Trim();
                return value != null;
            }
        }
    }
}
