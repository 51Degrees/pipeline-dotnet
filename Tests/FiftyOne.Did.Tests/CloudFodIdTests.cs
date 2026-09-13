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
        /// The versioned Model Terms for Marketing document a marketing
        /// 51Did is created under.
        /// </summary>
        /// <remarks>
        /// Written out here rather than read from the package, because a
        /// test that asked the package what it expects would agree with
        /// itself whatever the package said. The literal is what a
        /// receiver has to be able to fetch, so a table change that moved
        /// it has to fail here and be looked at.
        /// </remarks>
        private const string ModelTermsForMarketing2 =
            "https://m4ow.uk/mtm/2.txt";

        /// <summary>
        /// The cloud <c>id.usage</c> levels checked for the resource key,
        /// with whether a 51Did is required for that usage and the terms
        /// address the identifier must carry. <c>non-marketing</c> is
        /// available on any key that includes <c>fodid.*</c>, so it is
        /// required. <c>standard</c> and <c>personalized</c> are marketing
        /// usages that are validated when returned and reported when not.
        /// </summary>
        /// <remarks>
        /// A non-marketing identifier states no terms, because it may not
        /// reach a demand source at all, so there is nothing for a
        /// receiver to agree to. The two marketing usages both carry the
        /// Model Terms for Marketing. This is the check that proves the
        /// cloud on the other end of the request writes the Terms byte:
        /// an identifier from a cloud that predates it ends at the match
        /// key and reads as no terms, so the two marketing rows fail.
        /// </remarks>
        private static readonly (string Usage, bool Required, string? Terms)[]
            Usages = new[]
        {
            ("non-marketing", true, (string?)null),
            ("standard", false, ModelTermsForMarketing2),
            ("personalized", false, ModelTermsForMarketing2),
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

            // Counts the identifiers whose terms were actually read
            // and matched. A key carrying no marketing usage skips
            // those rows entirely, and a run that skipped them has
            // not proven the Terms byte however green it looks, so
            // the count is checked after the loop rather than left
            // implied.
            var termsChecked = 0;

            foreach (var (usage, required, terms) in Usages)
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
                    AssertValid51Did(
                        $"{usage}/idprobglobal", idProbGlobal!, terms);
                    if (terms != null)
                    {
                        termsChecked++;
                    }
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
                    AssertValid51Did(
                        $"{usage}/idproblic", idProbLic!, terms);
                    if (terms != null)
                    {
                        termsChecked++;
                    }
                }
            }

            // Only a marketing usage carries a terms address, so only
            // a marketing identifier can show that the service wrote
            // the byte. Where this key returned none, say so rather
            // than reporting a pass that proved nothing. The
            // non-marketing identifier states no terms either way,
            // which is the same answer a cloud predating the Terms
            // release would give, so it cannot tell them apart.
            if (termsChecked == 0)
            {
                Assert.Inconclusive(
                    "This resource key returned no marketing 51Did, " +
                    "so nothing carried a terms address and this run " +
                    "did not prove the Terms byte. Use a key entitled " +
                    "to the standard or personalized usage to prove " +
                    "it.");
            }

            Console.WriteLine(
                $"Terms checked on {termsChecked} marketing " +
                $"identifier(s).");
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
        /// Asserts that <paramref name="base64"/> is a real 51Did, being a
        /// signed OWID envelope whose payload carries the 51Did fields,
        /// including the 32-byte probabilistic match key and the terms the
        /// identifier was created under.
        /// </summary>
        /// <param name="label">Names the identifier in a failure.</param>
        /// <param name="base64">The identifier as the cloud returned it.</param>
        /// <param name="expectedTerms">
        /// The terms address the identifier must carry, or <c>null</c> where
        /// it must state none.
        /// </param>
        private static void AssertValid51Did(
            string label,
            string base64,
            string? expectedTerms)
        {
            var fodId = new FodId(base64);

            // A 51Did wraps a payload of at least PayloadLength bytes carrying
            // a MatchKeyLength byte probabilistic match key, inside a domain
            // bearing envelope.
            Assert.AreEqual(FodId.MatchKeyLength, fodId.MatchKey.Length,
                $"{label}: match key length");
            Assert.IsTrue(fodId.Payload.Length >= FodId.MinimumPayloadLength,
                $"{label}: payload length {fodId.Payload.Length} is below " +
                $"the {FodId.MinimumPayloadLength} byte minimum");
            Assert.IsFalse(string.IsNullOrEmpty(fodId.Domain),
                $"{label}: domain should not be empty");

            // The identifier round trips byte for byte and re-parses to the
            // same probabilistic value.
            var reparsed = new FodId(fodId.AsBase64());
            CollectionAssert.AreEqual(fodId.MatchKey, reparsed.MatchKey,
                $"{label}: match key should survive a base64 round trip");

            // The terms travel with the identifier, so a receiver can read
            // what it was created under without asking anyone. A payload
            // that stops at the match key reads as no terms, which is why
            // this is the assertion that fails where the cloud has not
            // been updated to write the byte.
            Assert.AreEqual(expectedTerms, fodId.Terms,
                $"{label}: expected the terms to be " +
                $"'{expectedTerms ?? "none"}' but the identifier carries " +
                $"'{fodId.Terms ?? "none"}'. Where this reads none for a " +
                $"marketing usage the cloud that answered is older than " +
                $"the release that writes the Terms byte.");
            Assert.AreEqual(expectedTerms, reparsed.Terms,
                $"{label}: terms should survive a base64 round trip");

            Console.WriteLine(
                $"{label}: domain={fodId.Domain} " +
                $"flags=0x{fodId.Flags:X2} " +
                $"licenseId=0x{fodId.LicenseId:X8} " +
                $"terms={fodId.Terms ?? "none"} " +
                $"matchKey={Convert.ToHexString(fodId.MatchKey)}");
        }
    }
}
