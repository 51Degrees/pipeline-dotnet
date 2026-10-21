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
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FiftyOne.Did.Tests
{
    /// <summary>
    /// Live tests for <see cref="DidClient"/> against the cloud. They
    /// create a 51Did through the cloud <c>json</c> endpoint with the
    /// resource key from the environment, exactly as
    /// <see cref="CloudFodIdTests"/> does, then verify it offline and
    /// through the cloud, and redeem both a garbage result and one the
    /// cloud sealed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Set <c>_51DEGREES_RESOURCE_KEY</c> (or the legacy
    /// <c>SUPER_RESOURCE_KEY</c>) to a key whose properties include
    /// <c>fodid.*</c>. With no key set the tests are inconclusive.
    /// <c>_51DEGREES_LICENSE_KEY_51DID</c>, then
    /// <c>_51DEGREES_LICENSE_KEY</c> (or <c>LICENSE_KEY</c>), is read for
    /// the license key and is optional. When one is set it must belong to
    /// the resource key's account and carry the 51Did product, because a
    /// sealed result must then read back as a verdict.
    /// <c>FOD_CLOUD_API_URL</c> points every call at another host.
    /// </para>
    /// <para>
    /// These are integration tests that use the live cloud service, so
    /// any problem with that service could affect the result.
    /// </para>
    /// </remarks>
    [TestClass]
    public class CloudDidClientTests
    {
        private const string UserAgent =
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) " +
            "AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 " +
            "Mobile/15E148 Safari/604.1";

        /// <summary>
        /// A client IP for the request. 203.0.113.0/24 is the TEST-NET-3
        /// range reserved for documentation (RFC 5737).
        /// </summary>
        private const string ClientIp = "203.0.113.42";

        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        };

        private static string? ResourceKey() =>
            FirstSet("_51DEGREES_RESOURCE_KEY", "SUPER_RESOURCE_KEY");

        private static string? LicenceKey() =>
            FirstSet(
                "_51DEGREES_LICENSE_KEY_51DID",
                "_51DEGREES_LICENSE_KEY",
                "LICENSE_KEY");

        private static string? FirstSet(params string[] names)
        {
            foreach (var name in names)
            {
                var value = Environment.GetEnvironmentVariable(name);
                if (string.IsNullOrWhiteSpace(value) == false)
                {
                    return value;
                }
            }
            return null;
        }

        /// <summary>
        /// A client for the key in the environment, or null with the test
        /// marked inconclusive when there is none.
        /// </summary>
        private static DidClient? ClientOrInconclusive()
        {
            var resourceKey = ResourceKey();
            if (resourceKey == null)
            {
                Assert.Inconclusive(
                    "No resource key supplied for the live cloud 51Did client " +
                    "tests. Set _51DEGREES_RESOURCE_KEY (or the legacy " +
                    "SUPER_RESOURCE_KEY) to a key whose properties include " +
                    "fodid.* and re-run. Get a free key that includes 51Did " +
                    "from https://configure.51degrees.com/N57Wygby");
                return null;
            }
            return new DidClient(resourceKey, LicenceKey());
        }

        /// <summary>
        /// Creates a 51Did through the cloud json endpoint and parses it,
        /// or marks the test inconclusive when the key returns none.
        /// </summary>
        private static async Task<FodId?> CreateAsync(DidClient client)
        {
            var encoded = await CreateEncodedAsync(client);
            return encoded == null ? null : FodId.FromBase64(encoded);
        }

        /// <summary>
        /// The 51Did exactly as the cloud json endpoint returned it, or null
        /// with the test marked inconclusive when the key returns none.
        /// </summary>
        private static async Task<string?> CreateEncodedAsync(DidClient client)
        {
            var url =
                $"{client.Endpoint}json?resource={Uri.EscapeDataString(client.ResourceKey)}" +
                $"&user-agent={Uri.EscapeDataString(UserAgent)}" +
                $"&client-ip={Uri.EscapeDataString(ClientIp)}" +
                "&id.usage=non-marketing";
            using var response = await Http.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();
            Assert.IsTrue(response.IsSuccessStatusCode,
                $"Cloud creation request failed " +
                $"({(int)response.StatusCode} {response.StatusCode}): {body}");
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("fodid", out var fodid) == false
                || fodid.TryGetProperty("idprobglobal", out var id) == false
                || id.ValueKind != JsonValueKind.String
                || string.IsNullOrEmpty(id.GetString()))
            {
                Assert.Inconclusive(
                    "The resource key returned no fodid.idprobglobal, so " +
                    "there is no 51Did to verify. Use a key whose properties " +
                    "include fodid.*. Response: " + body);
                return null;
            }
            return id.GetString()!;
        }

        /// <summary>
        /// Seals a creator context result for the 51Did, standing in for
        /// the browser call the client does not carry, or null when the
        /// host returns none.
        /// </summary>
        private static async Task<string?> SealAsync(
            DidClient client, string encoded, string challenge)
        {
            var url =
                $"{client.Endpoint}id/verify-context/{Uri.EscapeDataString(client.ResourceKey)}" +
                $"?51did={Uri.EscapeDataString(encoded)}" +
                $"&challenge={Uri.EscapeDataString(challenge)}";
            using var response = await Http.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();
            Assert.IsTrue(response.IsSuccessStatusCode,
                $"verify-context request failed " +
                $"({(int)response.StatusCode} {response.StatusCode}): {body}");
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("result", out var result)
                && result.ValueKind == JsonValueKind.String
                ? result.GetString()
                : null;
        }

        [TestMethod]
        public async Task Create_VerifyOffline_VerifyThroughCloud()
        {
            using var client = ClientOrInconclusive();
            if (client == null) { return; }
            var fodId = await CreateAsync(client);
            if (fodId == null) { return; }

            var key = await client.PublicKeyForAsync(fodId);
            Assert.IsNotNull(key,
                $"No published key covers the identifier's date {fodId.Date:o}.");
            Assert.AreEqual(
                SignatureCheck.Verified,
                await client.VerifySignatureDetailedAsync(fodId),
                "offline signature check");
            Assert.IsTrue(await client.VerifySignatureAsync(fodId));
            Assert.IsTrue(await client.VerifyAsync(fodId), "cloud signature check");

            Console.WriteLine(
                $"domain={fodId.Domain} date={fodId.Date:o} " +
                $"key starts {key!.StartsAt:o} " +
                $"payload={fodId.Payload.Length} bytes");
        }

        [TestMethod]
        public async Task Redeem_GarbageResult_IsUnreadable()
        {
            using var client = ClientOrInconclusive();
            if (client == null) { return; }
            var fodId = await CreateAsync(client);
            if (fodId == null) { return; }

            RedeemResult result;
            try
            {
                result = await client.RedeemAsync(
                    fodId, "not-base64url!!", "0123456789abcdef");
            }
            catch (NotSupportedException e)
            {
                Assert.Inconclusive(
                    "The host does not offer the creator context: " + e.Message);
                return;
            }

            // Every cryptographic failure is the one word, with status 200,
            // by design.
            Assert.AreEqual(200, result.StatusCode, result.Raw);
            Assert.AreEqual(ContextOutcome.Unreadable, result.Context, result.Raw);
            Assert.AreEqual(SignatureOutcome.Unknown, result.Signature);
        }

        [TestMethod]
        public async Task Redeem_SealedResult_ReadsVerdictWithLicenseKey()
        {
            using var client = ClientOrInconclusive();
            if (client == null) { return; }
            var encoded = await CreateEncodedAsync(client);
            if (encoded == null) { return; }

            var challenge = Guid.NewGuid().ToString("N");
            var sealedResult = await SealAsync(client, encoded, challenge);
            if (sealedResult == null)
            {
                Assert.Inconclusive(
                    "verify-context returned no sealed result, so the host " +
                    "holds no creator context secret.");
                return;
            }

            RedeemResult result;
            try
            {
                result = await client.RedeemAsync(encoded, sealedResult, challenge);
            }
            catch (NotSupportedException e)
            {
                Assert.Inconclusive(
                    "The host does not offer the creator context: " + e.Message);
                return;
            }

            if (LicenceKey() == null)
            {
                Assert.AreEqual(ContextOutcome.Unreadable, result.Context,
                    "without a license key the sealed result cannot be read: " +
                    result.Raw);
                return;
            }

            Assert.AreNotEqual(ContextOutcome.Unreadable, result.Context,
                "the license key reads the sealed result, so it belongs to " +
                "the resource key's account and carries 51Did: " + result.Raw);
            Assert.AreEqual(SignatureOutcome.Verified, result.Signature, result.Raw);
            // The 51Did was created with a User-Agent and client IP that are
            // not this machine's, so factors are expected to differ. What
            // matters is that each one is reported.
            Assert.IsNotNull(result.Factors, result.Raw);
            CollectionAssert.AreEquivalent(
                FactorName.All.ToList(), result.Factors!.Keys.ToList(), result.Raw);

            Console.WriteLine(
                $"redeemed: context={result.ContextValue} " +
                $"signature={result.Signature} factors={result.Factors.Count}");
        }
    }
}
