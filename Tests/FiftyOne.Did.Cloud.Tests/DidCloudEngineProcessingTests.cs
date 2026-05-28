using FiftyOne.Did.Cloud.Data;
using FiftyOne.Did.Cloud.FlowElements;
using FiftyOne.Did.Core.Data;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;

namespace FiftyOne.Did.Cloud.Tests;

/// <summary>
/// Isolated unit tests for <see cref="DidCloudEngine.ProcessCloudEngine"/>:
/// feed the engine a cloud-shaped JSON response directly and verify the
/// resulting <see cref="I51DidData"/>.
/// </summary>
[TestClass]
public class DidCloudEngineProcessingTests
{
    /// <summary>
    /// Subclass that re-exposes the protected
    /// <c>ProcessCloudEngine</c> as a public method so tests can
    /// invoke it without reflection or PrivateObject.
    /// </summary>
    private sealed class ExposedDidCloudEngine(
        Func<
                IPipeline,
                FlowElementBase<I51DidData, IAspectPropertyMetaData>,
                I51DidData> aspectDataFactory) : DidCloudEngine(
            NullLogger<AspectEngineBase<I51DidData, IAspectPropertyMetaData>>.Instance,
            aspectDataFactory)
    {
        public new void ProcessCloudEngine(
            IFlowData data, I51DidData aspectData, string json)
            => base.ProcessCloudEngine(data, aspectData, json);
    }

    private ExposedDidCloudEngine _engine = null!;
    private Mock<IPipeline> _pipeline = null!;

    [TestInitialize]
    public void Init()
    {
        _pipeline = new Mock<IPipeline>(MockBehavior.Loose);

        _engine = new ExposedDidCloudEngine((pipeline, flowElement) =>
            new Cloud51DidData(
                NullLogger<Cloud51DidData>.Instance,
                pipeline,
                flowElement as IAspectEngine));
    }

    private Cloud51DidData NewAspectData() => new(
            NullLogger<Cloud51DidData>.Instance,
            _pipeline.Object,
            _engine);

    private static IFlowData NewFlowData() =>
        new Mock<IFlowData>(MockBehavior.Loose).Object;

    /// <summary>
    /// Cloud JSON response contains a <c>fodid</c> block with both 
    /// <c>idprobglobal</c> and <c>idproblic</c>. The engine populates the 
    /// aspect data with both values verbatim.
    /// </summary>
    [TestMethod]
    public void ValidFodidJson_PopulatesSuccess()
    {
        const string globalDid = "AzUxZC5lcwBzGTMAJQAAAAH-EXAMPLE-GLOBAL";
        const string licDid = "AzUxZC5lcwBzGTMAJQAAAAH-EXAMPLE-LIC";
        var json = $@"{{
            ""fodid"": {{
                ""idprobglobal"": ""{globalDid}"",
                ""idproblic"": ""{licDid}""
            }}
        }}";

        var aspectData = NewAspectData();
        _engine.ProcessCloudEngine(NewFlowData(), aspectData, json);

        Assert.IsTrue(aspectData.IdProbGlobal.HasValue);
        Assert.AreEqual(globalDid, aspectData.IdProbGlobal.Value);
        Assert.IsTrue(aspectData.IdProbLic.HasValue);
        Assert.AreEqual(licDid, aspectData.IdProbLic.Value);
    }

    /// <summary>
    /// When the cloud response omits the <c>fodid</c> block entirely,
    /// the engine silently bypasses population. Pins the
    /// silent-bypass contract: no exception, no partial state.
    /// </summary>
    [TestMethod]
    public void NoFodidBlock_DoesntPopulate()
    {
        var json = @"{ ""device"": { ""ismobile"": ""True"" } }";

        var aspectData = NewAspectData();
        _engine.ProcessCloudEngine(NewFlowData(), aspectData, json);

        // Catch and assert the "no value" outcome via the raw
        // dictionary.
        var raw = aspectData.AsDictionary();
        Assert.IsFalse(raw.ContainsKey(nameof(I51DidData.IdProbGlobal)));
        Assert.IsFalse(raw.ContainsKey(nameof(I51DidData.IdProbLic)));
    }
}
