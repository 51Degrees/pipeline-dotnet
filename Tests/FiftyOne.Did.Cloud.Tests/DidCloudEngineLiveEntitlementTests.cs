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

using FiftyOne.Did.Cloud.FlowElements;
using FiftyOne.Did.Core.FlowElements;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FiftyOne.Did.Cloud.Tests;

/// <summary>
/// The live half of the entitlement change. A customer style pipeline is
/// built against the real cloud with the resource key the environment
/// supplies, and the outcome has to match what that key is actually
/// entitled to.
/// </summary>
/// <remarks>
/// <para>
/// The test reads one resource key from <c>51DEGREES_RESOURCE_KEY</c>, which
/// the workflow that runs the live 51Did tests sets once per key secret, or
/// where that is unset from <c>_51DEGREES_RESOURCE_KEY_51DID</c>, the name
/// continuous integration gives a resource key carrying the 51Did product.
/// Running it for both an entitled key and an unentitled one is therefore a
/// matter of the workflow looping rather than of this test naming two keys.
/// With neither name set it is inconclusive.
/// </para>
/// <para>
/// Whether the key carries the product is not taken from the engine under
/// test. It is read from the cloud's own accessibleproperties answer, so
/// the two sides of the assertion come from different places and the test
/// cannot agree with itself.
/// </para>
/// <para>
/// The key is never printed, in a message, a log or a failure.
/// </para>
/// </remarks>
[TestClass]
public class DidCloudEngineLiveEntitlementTests
{
    /// <summary>
    /// Set by the test framework. Used to say which of the two outcomes
    /// the run took, because the key decides that and a run that says
    /// nothing looks the same either way.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Read first. The runtime name a developer sets.
    /// </summary>
    private const string ResourceKeyEnvVar = "51DEGREES_RESOURCE_KEY";

    /// <summary>
    /// Read where <see cref="ResourceKeyEnvVar"/> is unset. The name
    /// continuous integration sets for a resource key carrying the 51Did
    /// product. It starts with an underscore because a shell cannot export
    /// a name starting with a digit.
    /// </summary>
    private const string CiResourceKeyEnvVar = "_51DEGREES_RESOURCE_KEY_51DID";

    private const string AccessiblePropertiesUrl =
        "https://cloud.51degrees.com/api/v4/accessibleproperties";

    private static string? ResourceKey()
    {
        var key = Environment.GetEnvironmentVariable(ResourceKeyEnvVar);
        if (string.IsNullOrWhiteSpace(key) == false)
        {
            return key;
        }
        key = Environment.GetEnvironmentVariable(CiResourceKeyEnvVar);
        if (string.IsNullOrWhiteSpace(key) == false)
        {
            return key;
        }
        return null;
    }

    /// <summary>
    /// Ask the cloud what the key is entitled to, and return the property
    /// names it lists for the 51Did element, or null where it lists no
    /// entry for the element at all.
    /// </summary>
    private static async Task<List<string>?> ListedPropertiesAsync(string key)
    {
        using var client = new HttpClient();
        var json = await client.GetStringAsync(
            $"{AccessiblePropertiesUrl}?resource={key}");
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("Products", out var products)
            == false)
        {
            return null;
        }
        foreach (var product in products.EnumerateObject())
        {
            if (product.Name.Equals(
                DidBaseEnginePropertiesBuilder.ComponentName,
                StringComparison.OrdinalIgnoreCase) == false)
            {
                continue;
            }
            // The exact spelling matters, because the loader looks the
            // element data key up in a dictionary the cloud's own answer
            // fills, and that dictionary compares keys exactly.
            Assert.AreEqual(
                DidBaseEnginePropertiesBuilder.ComponentName, product.Name,
                "the cloud must list the 51Did product under the element " +
                "data key the engine returns, spelled the same way");
            var names = new List<string>();
            if (product.Value.TryGetProperty("Properties", out var properties))
            {
                names.AddRange(properties.EnumerateArray()
                    .Select(p => p.GetProperty("Name").GetString())
                    .Where(n => n != null)
                    .Select(n => n!));
            }
            return names;
        }
        return null;
    }

    /// <summary>
    /// The customer style pipeline of the work package's three
    /// configurations. An entitled key builds and reports what the cloud
    /// listed, and an unentitled one fails the build naming the element
    /// rather than running and never creating an identifier.
    /// </summary>
    [TestMethod]
    [Timeout(180_000)]
    public async Task ACustomerPipelineMatchesWhatTheKeyIsEntitledTo()
    {
        var key = ResourceKey();
        if (key == null)
        {
            Assert.Inconclusive(
                "No resource key in the environment. Set " +
                $"{ResourceKeyEnvVar}, or {CiResourceKeyEnvVar} in CI, to " +
                "run this against the live cloud.");
        }

        var listed = await ListedPropertiesAsync(key);
        var loggerFactory = new LoggerFactory();

        IPipeline? pipeline = null;
        try
        {
            // The cloud request engine's builder warms up at build, so the
            // whole question is settled here.
            pipeline = new PipelineBuilder(loggerFactory)
                .AddFlowElement(new CloudRequestEngineBuilder(
                        loggerFactory, new HttpClient())
                    .SetResourceKey(key)
                    .Build())
                .AddFlowElement(new DidCloudEngineBuilder(loggerFactory)
                    .Build())
                .Build();
        }
        catch (PipelineException ex)
        {
            Assert.IsNull(listed,
                "the cloud lists the 51Did product for this key, so the " +
                "pipeline had no reason to refuse to build. The message " +
                $"was: {ex.Message}");
            StringAssert.Contains(ex.Message,
                DidBaseEnginePropertiesBuilder.ComponentName,
                "a key that does not carry the product must fail the build " +
                "with a message naming the element, which is what every " +
                "other cloud engine already does");
            TestContext.WriteLine(
                "The key carries no 51Did entitlement, and the pipeline " +
                "build failed naming the element: " + ex.Message);
            return;
        }

        using (pipeline)
        {
            Assert.IsNotNull(listed,
                "the cloud lists no 51Did entry for this key, so the " +
                "pipeline must have refused to build rather than running " +
                "and never creating an identifier");

            var engine = pipeline.GetElement<DidCloudEngine>();
            var reported = engine.Properties.Select(p => p.Name).ToList();
            CollectionAssert.AreEquivalent(listed, reported,
                "the engine must report exactly what the cloud listed, " +
                $"cloud: [{string.Join(", ", listed)}], engine: " +
                $"[{string.Join(", ", reported)}]");
            Assert.IsTrue(engine.Properties.All(p => p.Available),
                "everything the cloud lists is available");

            var available = pipeline.ElementAvailableProperties;
            Assert.IsTrue(
                available.TryGetValue(
                    DidBaseEnginePropertiesBuilder.ComponentName,
                    out var forElement) && forElement.Count > 0,
                "and the pipeline must offer them under the element data " +
                "key, because that is what the JavaScript builder reads to " +
                "decide whether to render the user prompt block");
            TestContext.WriteLine(
                "The key is entitled and the pipeline offers " +
                $"[{string.Join(", ", reported)}] under " +
                DidBaseEnginePropertiesBuilder.ComponentName + ".");
        }
    }
}
