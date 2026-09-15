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

using FiftyOne.Did.Cloud.Data;
using FiftyOne.Did.Cloud.FlowElements;
using FiftyOne.Did.Core.Data;
using FiftyOne.Did.Core.FlowElements;
using FiftyOne.Pipeline.CloudRequestEngine;
using FiftyOne.Pipeline.CloudRequestEngine.Data;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.Exceptions;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.Did.Cloud.Tests;

/// <summary>
/// The 51Did cloud engine takes its property availability from the cloud,
/// like every other cloud engine, rather than from a local list in which
/// every property is marked available whatever the resource key carries.
/// This is test 21 of pipeline-dotnet#414 and it is what makes the
/// JavaScript builder's user prompt decision true in a customer pipeline.
/// </summary>
[TestClass]
public class DidCloudEngineEntitlementTests
{
    /// <summary>
    /// The names the engine populates, which is what the cloud lists for
    /// a key carrying the whole product.
    /// </summary>
    private static readonly string[] AllSixNames =
    {
        nameof(I51DidData.IdProbGlobal),
        nameof(I51DidData.IdProbLic),
        nameof(I51DidData.IdRandGlobal),
        nameof(I51DidData.IdRandLic),
        nameof(I51DidData.IdHemGlobal),
        nameof(I51DidData.IdHemLic),
    };

    /// <summary>
    /// Duplicated here because the message resource is internal to
    /// FiftyOne.Pipeline.CloudRequestEngine. The base test
    /// CloudAspectEngineBaseTests carries the same copy.
    /// </summary>
    private const string PropertiesErrorFormat =
        "Failed to load properties for '{0}'. This usually means your " +
        "resource key does not include access to any properties under " +
        "'{0}'.";

    /// <summary>
    /// Stands in for the cloud request engine, so a test can say exactly
    /// what the resource key is entitled to without making a request.
    /// </summary>
    private class StubRequestEngine
        : AspectEngineBase<CloudRequestData, IAspectPropertyMetaData>,
            ICloudRequestEngine
    {
        public StubRequestEngine()
            : base(NullLogger<StubRequestEngine>.Instance, CreateData)
        {
        }

        private static CloudRequestData CreateData(
            IPipeline pipeline,
            FlowElementBase<CloudRequestData, IAspectPropertyMetaData> element)
            => new CloudRequestData(
                NullLogger<CloudRequestData>.Instance,
                pipeline,
                element as IAspectEngine);

        /// <summary>
        /// Answers the property request. A test that wants a failure
        /// throws from here, which is what the cloud request engine does
        /// when it cannot reach the service.
        /// </summary>
        public Func<IReadOnlyDictionary<string, ProductMetaData>?> Answer
        { get; set; } = () => null;

        public IReadOnlyDictionary<string, ProductMetaData>? PublicProperties
            => Answer();

        public override string DataSourceTier => string.Empty;

        public override string ElementDataKey => "cloud";

        public override IEvidenceKeyFilter EvidenceKeyFilter { get; } =
            new EvidenceKeyFilterWhitelist(new List<string>());

        public override IList<IAspectPropertyMetaData> Properties { get; } =
            new List<IAspectPropertyMetaData>();

        protected override void ProcessEngine(
            IFlowData data, CloudRequestData aspectData)
        {
        }

        protected override void UnmanagedResourcesCleanup()
        {
        }
    }

    private static DidCloudEngine NewEngine()
        => new DidCloudEngine(
            NullLogger<AspectEngineBase<I51DidData, IAspectPropertyMetaData>>
                .Instance,
            (pipeline, flowElement) => new Cloud51DidData(
                NullLogger<Cloud51DidData>.Instance,
                pipeline,
                flowElement as IAspectEngine));

    private static ProductMetaData ProductWith(params string[] names)
        => new ProductMetaData()
        {
            DataTier = "CloudV5FODiD",
            Properties = names
                .Select(n => new PropertyMetaData() { Name = n, Type = "String" })
                .ToList(),
        };

    /// <summary>
    /// A resource key that carries the product. The cloud lists the
    /// properties under the element data key the loader looks up, which
    /// was confirmed against the live accessibleproperties endpoint on
    /// 15 September 2026 and is why the key is spelled "FODid" here.
    /// </summary>
    [TestMethod]
    public void EntitledKeyReportsTheCloudsProperties()
    {
        var engine = NewEngine();
        var requestEngine = new StubRequestEngine
        {
            Answer = () => new Dictionary<string, ProductMetaData>()
            {
                [DidBaseEnginePropertiesBuilder.ComponentName] =
                    ProductWith(AllSixNames),
            },
        };

        using var pipeline = new PipelineBuilder(new LoggerFactory())
            .AddFlowElement(requestEngine)
            .AddFlowElement(engine)
            .Build();

        Assert.AreEqual(6, engine.Properties.Count,
            "the engine must report what the cloud listed");
        foreach (var name in AllSixNames)
        {
            Assert.IsTrue(engine.Properties.Any(p => p.Name == name),
                $"the cloud listed {name} and the engine did not report it");
        }
        Assert.IsTrue(engine.Properties.All(p => p.Available),
            "everything the cloud lists is available");
        Assert.IsTrue(engine.HasLoadedProperties,
            "the properties have been loaded by this point");
    }

    /// <summary>
    /// The live cloud lists one 51Did property for an entitled key today,
    /// which is still enough for the JavaScript builder to render the
    /// block, so one property is a case in its own right rather than an
    /// odd corner.
    /// </summary>
    [TestMethod]
    public void OnePropertyIsStillAnEntitledKey()
    {
        var engine = NewEngine();
        var requestEngine = new StubRequestEngine
        {
            Answer = () => new Dictionary<string, ProductMetaData>()
            {
                [DidBaseEnginePropertiesBuilder.ComponentName] =
                    ProductWith(nameof(I51DidData.IdProbGlobal)),
            },
        };

        using var pipeline = new PipelineBuilder(new LoggerFactory())
            .AddFlowElement(requestEngine)
            .AddFlowElement(engine)
            .Build();

        Assert.AreEqual(1, engine.Properties.Count);
        Assert.AreEqual(nameof(I51DidData.IdProbGlobal),
            engine.Properties[0].Name);

        var available = pipeline.ElementAvailableProperties;
        Assert.IsTrue(
            available.TryGetValue(
                DidBaseEnginePropertiesBuilder.ComponentName,
                out var forElement) && forElement.Count > 0,
            "the pipeline must offer the 51Did property, which is what the " +
            "JavaScript builder reads to decide whether to render the block");
    }

    /// <summary>
    /// A key with no entitlement to the product. The cloud lists no entry
    /// for the element, and the pipeline build fails naming it, which is
    /// what every other cloud engine already does. Before this change the
    /// engine advertised all six properties for such a key and the caller
    /// was told the identifier was available and then never got one.
    /// </summary>
    [TestMethod]
    public void UnentitledKeyFailsTheBuildNamingTheElement()
    {
        var engine = NewEngine();
        var requestEngine = new StubRequestEngine
        {
            Answer = () => new Dictionary<string, ProductMetaData>()
            {
                ["device"] = ProductWith("IsMobile"),
            },
        };

        try
        {
            using var pipeline = new PipelineBuilder(new LoggerFactory())
                .AddFlowElement(requestEngine)
                .AddFlowElement(engine)
                .Build();
            Assert.Fail("the build must fail for a key that does not carry " +
                "the product");
        }
        catch (PipelineException ex)
        {
            var expected = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                PropertiesErrorFormat,
                DidBaseEnginePropertiesBuilder.ComponentName);
            StringAssert.StartsWith(ex.Message, expected,
                "the message must name the element whose properties are " +
                $"missing, was: {ex.Message}");
        }
    }

    /// <summary>
    /// A failure to reach the cloud must surface as
    /// PropertiesNotYetLoadedException and never as a quiet "no properties
    /// available", because Pipeline.ElementAvailableProperties remembers
    /// its answer only when no element threw that exception, so a start up
    /// blip reported as unavailable would switch the 51Did off for the
    /// life of the process.
    /// </summary>
    [TestMethod]
    public void FetchFailureSurfacesAsPropertiesNotYetLoaded()
    {
        var engine = NewEngine();
        var thrown = 0;
        var requestEngine = new StubRequestEngine
        {
            Answer = () =>
            {
                thrown++;
                throw new CloudRequestException("Stubbed properties failure");
            },
        };

        using var pipeline = new PipelineBuilder(new LoggerFactory())
            .AddFlowElement(requestEngine)
            .AddFlowElement(engine)
            .Build();

        Assert.IsTrue(thrown > 0,
            "the pipeline build must have asked for the properties");
        Assert.IsFalse(engine.HasLoadedProperties,
            "nothing was loaded, so the engine must say so");

        Assert.Throws<PropertiesNotYetLoadedException>(
            () => _ = engine.Properties,
            "a fetch failure must be a retryable exception rather than an " +
            "empty list");

        // The pipeline swallows the exception, does not remember the
        // answer, and so asks again on the next read. That is what lets a
        // transient failure recover.
        var before = thrown;
        _ = pipeline.ElementAvailableProperties;
        _ = pipeline.ElementAvailableProperties;
        Assert.IsTrue(thrown > before,
            "the pipeline must keep asking whilst the fetch is failing, " +
            "rather than remembering that nothing is available");
    }

    /// <summary>
    /// Reading the response still populates every value the cloud sent,
    /// whatever the resource key was entitled to advertise, because the
    /// local list is used only to give those values their types.
    /// </summary>
    [TestMethod]
    public void ValuesAreStillTypedFromTheLocalList()
    {
        var engine = NewEngine();
        var requestEngine = new StubRequestEngine
        {
            Answer = () => new Dictionary<string, ProductMetaData>()
            {
                [DidBaseEnginePropertiesBuilder.ComponentName] =
                    ProductWith(nameof(I51DidData.IdProbGlobal)),
            },
        };

        using var pipeline = new PipelineBuilder(new LoggerFactory())
            .AddFlowElement(requestEngine)
            .AddFlowElement(engine)
            .Build();

        var aspectData = new Cloud51DidData(
            NullLogger<Cloud51DidData>.Instance, pipeline, engine);
        var json = @"{
            ""fodid"": {
                ""idprobglobal"": ""pg"",
                ""idhemlic"": ""hl""
            }
        }";

        new ExposedEngine(engine).Process(
            new Mock<IFlowData>(MockBehavior.Loose).Object, aspectData, json);

        Assert.AreEqual("pg", aspectData.IdProbGlobal.Value);
        Assert.AreEqual("hl", aspectData.IdHemLic.Value,
            "a value the cloud sent must still be read even where the key " +
            "did not advertise the property");
    }

    /// <summary>
    /// Reaches the protected read method without reflection.
    /// </summary>
    private sealed class ExposedEngine
    {
        private readonly DidCloudEngine _engine;

        public ExposedEngine(DidCloudEngine engine)
        {
            _engine = engine;
        }

        public void Process(IFlowData data, I51DidData aspectData, string json)
            => typeof(DidCloudEngine)
                .GetMethod(
                    "ProcessCloudEngine",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)!
                .Invoke(_engine, new object[] { data, aspectData, json });
    }
}
