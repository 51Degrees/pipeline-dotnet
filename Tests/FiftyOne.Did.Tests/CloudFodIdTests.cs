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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Owid.Client;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FiftyOne.Did.Tests
{
    /// <summary>
    /// Live integration test that obtains a real 51Did from the 51Degrees cloud
    /// and checks that it parses into a <see cref="FodId"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The test uses a single resource key from the environment. Set
    /// <c>_51DEGREES_RESOURCE_KEY</c> (or the legacy <c>SUPER_RESOURCE_KEY</c>)
    /// to a key whose properties include <c>fodid.*</c>. With no key set the
    /// test is inconclusive.
    /// </para>
    /// <para>
    /// To exercise more than one key (for example a free key and a paid key),
    /// the CI workflow runs this test once per <c>_51DEGREES_RESOURCE_KEY*</c>
    /// secret, setting <c>_51DEGREES_RESOURCE_KEY</c> to each in turn. The test
    /// itself only ever reads the single variable.
    /// </para>
    /// <para>
    /// For each key the test checks the cloud <c>id.usage</c> levels.
    /// <c>non-marketing</c> is available on any key that includes
    /// <c>fodid.*</c>, so it is required. <c>standard</c> and
    /// <c>personalized</c> are marketing usages that paid keys are expected to
    /// grant in due course; they are validated when the key returns them and
    /// reported when it does not, so the test starts covering them
    /// automatically once a paid key is expanded for marketing.
    /// </para>
    /// <para>
    /// This is an integration test that uses the live cloud service, so any
    /// problems with that service could affect the result of this test.
    /// </para>
    /// </remarks>
    [TestClass]
    public class CloudFodIdTests
    {
        /// <summary>
        /// The aligned environment variable name used to supply the resource
        /// key. Checked before the legacy name.
        /// </summary>
        private const string ResourceKeyEnvVar = "_51DEGREES_RESOURCE_KEY";

        /// <summary>
        /// The legacy environment variable name, checked when
        /// <see cref="ResourceKeyEnvVar"/> is not set.
        /// </summary>
        private const string LegacyResourceKeyEnvVar = "SUPER_RESOURCE_KEY";

        /// <summary>
        /// The 51Degrees cloud V4 JSON endpoint.
        /// </summary>
        private const string CloudJsonUrl =
            "https://cloud.51degrees.com/api/v4/json";

        /// <summary>
        /// A representative mobile User-Agent. The cloud needs Device
        /// Detection evidence plus a client IP to derive a 51Did.
        /// </summary>
        private const string UserAgent =
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) " +
            "AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 " +
            "Mobile/15E148 Safari/604.1";

        /// <summary>
        /// A client IP for the request. 203.0.113.0/24 is the TEST-NET-3 range
        /// reserved for documentation (RFC 5737).
        /// </summary>
        private const string ClientIp = "203.0.113.42";

        /// <summary>
        /// The cloud <c>id.usage</c> levels checked for the resource key, with
        /// whether a 51Did is required for that usage. <c>non-marketing</c> is
        /// available on any key that includes <c>fodid.*</c>, so it is
        /// required. <c>standard</c> and <c>personalized</c> are marketing
        /// usages that are validated when returned and reported when not.
        /// </summary>
        private static readonly (string Usage, bool Required)[] Usages = new[]
        {
            ("non-marketing", true),
            ("standard", false),
            ("personalized", false),
        };

        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        };

        /// <summary>
        /// The resource key from the environment, preferring the aligned name
        /// and falling back to the legacy name. <c>null</c> when neither is
        /// set.
        /// </summary>
        private static string? ResourceKey()
        {
            var key = Environment.GetEnvironmentVariable(ResourceKeyEnvVar);
            if (string.IsNullOrWhiteSpace(key))
            {
                key = Environment.GetEnvironmentVariable(LegacyResourceKeyEnvVar);
            }
            return string.IsNullOrWhiteSpace(key) ? null : key;
        }

        [TestMethod]
        public async Task ResourceKeyReturns51DidForSupportedUsages()
        {
            var resourceKey = ResourceKey();
            if (resourceKey == null)
            {
                Assert.Inconclusive(
                    $"No resource key supplied for the live cloud 51Did test.\n" +
                    $"Set a 51Degrees resource key whose properties include " +
                    $"fodid.* in one of these ways, then re-run:\n" +
                    $"  - PowerShell env var:  $env:{ResourceKeyEnvVar} = '<your-key>'\n" +
                    $"  - bash env var:        export {ResourceKeyEnvVar}=<your-key>\n" +
                    $"  - inline, single run:  dotnet test --filter " +
                    $"FullyQualifiedName~CloudFodIdTests -e {ResourceKeyEnvVar}=<your-key>\n" +
                    $"The legacy variable {LegacyResourceKeyEnvVar} is also " +
                    $"accepted. Get a free key that includes 51Did from " +
                    $"https://configure.51degrees.com/N57Wygby");
                return;
            }

            foreach (var (usage, required) in Usages)
            {
                var body = await RequestUsageAsync(resourceKey, usage);
                using var document = JsonDocument.Parse(body);

                // The cloud groups 51Did properties under a 'fodid' element. It
                // is absent when the resource key does not include the fodid.*
                // properties.
                if (document.RootElement.TryGetProperty("fodid", out var fodidElement) == false)
                {
                    if (required)
                    {
                        Assert.Fail(
                            $"id.usage={usage}: response has no 'fodid' element; a " +
                            $"resource key for the 51Did tests must include the " +
                            $"fodid.* properties. Response: {body}");
                    }
                    Console.WriteLine(
                        $"id.usage={usage}: no 'fodid' element returned (this " +
                        $"marketing usage becomes available once the resource key " +
                        $"is expanded for it).");
                    continue;
                }

                // idprobglobal is the global 51Did for this usage. It is
                // required for non-marketing and validated when a marketing
                // usage returns it.
                var idProbGlobal = StringProperty(fodidElement, "idprobglobal");
                if (string.IsNullOrEmpty(idProbGlobal) == false)
                {
                    AssertValid51Did($"{usage}/idprobglobal", idProbGlobal!);
                }
                else if (required)
                {
                    Assert.Fail(
                        $"id.usage={usage}: no idprobglobal returned. fodid " +
                        $"element: " + fodidElement.GetRawText());
                }
                else
                {
                    Console.WriteLine(
                        $"id.usage={usage}: no idprobglobal returned (becomes " +
                        $"available once the resource key is expanded for this " +
                        $"marketing usage).");
                }

                // idproblic is scoped to the caller's licence and is validated
                // whenever it is returned.
                var idProbLic = StringProperty(fodidElement, "idproblic");
                if (string.IsNullOrEmpty(idProbLic) == false)
                {
                    AssertValid51Did($"{usage}/idproblic", idProbLic!);
                }
            }
        }

        /// <summary>
        /// Calls the cloud JSON endpoint for the given <c>id.usage</c> and
        /// returns the response body, asserting the request succeeded.
        /// </summary>
        private static async Task<string> RequestUsageAsync(string resourceKey, string usage)
        {
            var url =
                $"{CloudJsonUrl}?resource={Uri.EscapeDataString(resourceKey)}" +
                $"&user-agent={Uri.EscapeDataString(UserAgent)}" +
                $"&client-ip={Uri.EscapeDataString(ClientIp)}" +
                $"&id.usage={Uri.EscapeDataString(usage)}";

            using var response = await Http.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();
            Assert.IsTrue(response.IsSuccessStatusCode,
                $"Cloud request for id.usage={usage} failed " +
                $"({(int)response.StatusCode} {response.StatusCode}): {body}");
            return body;
        }

        /// <summary>
        /// Reads a string property from a JSON object, returning <c>null</c>
        /// when it is absent or not a non-empty string.
        /// </summary>
        private static string? StringProperty(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out var value) &&
                   value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        /// <summary>
        /// Asserts that <paramref name="base64"/> is a real 51Did: a signed
        /// OWID envelope whose payload carries the three 51Did fields,
        /// including the 32-byte probabilistic hash.
        /// </summary>
        private static void AssertValid51Did(string label, string base64)
        {
            var fodId = new FodId(base64);

            // A 51Did wraps a payload of at least PayloadLength bytes carrying
            // a HashLength byte probabilistic value, inside a domain bearing
            // envelope.
            Assert.AreEqual(FodId.HashLength, fodId.Hash.Length,
                $"{label}: hash length");
            Assert.IsTrue(fodId.Payload.Length >= FodId.PayloadLength,
                $"{label}: payload length {fodId.Payload.Length} is below " +
                $"the {FodId.PayloadLength} byte minimum");
            Assert.IsFalse(string.IsNullOrEmpty(fodId.Domain),
                $"{label}: domain should not be empty");

            // The identifier round trips byte for byte and re-parses to the
            // same probabilistic value.
            var reparsed = new FodId(fodId.AsBase64());
            CollectionAssert.AreEqual(fodId.Hash, reparsed.Hash,
                $"{label}: hash should survive a base64 round trip");

            Console.WriteLine(
                $"{label}: domain={fodId.Domain} " +
                $"flags=0x{fodId.Flags:X2} " +
                $"licenseId=0x{fodId.LicenseId:X8} " +
                $"hash={Convert.ToHexString(fodId.Hash)}");
        }
    }
}
