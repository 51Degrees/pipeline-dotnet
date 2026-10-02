using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace FiftyOne.Pipeline.Core.Tests.FlowElements;

[TestClass]
public class InlineExecutionParityTests
{
    private static Mock<IFlowElement> Element(string name)
    {
        var mock = new Mock<IFlowElement>();
        mock.SetupGet(e => e.ElementDataKey).Returns(name);
        mock.SetupGet(e => e.Properties).Returns(new List<IElementPropertyMetaData>());
        mock.SetupGet(e => e.EvidenceKeyFilter).Returns(new EvidenceKeyFilterWhitelist(new List<string>()));
        return mock;
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void BothEnginesRunAndErrorsRemainVisible(bool parallel, bool cancellationException)
    {
        var first = Element("first");
        var second = Element("second");
        Exception expected = cancellationException
            ? new OperationCanceledException("engine cancellation")
            : new InvalidOperationException("engine failure");
        first.Setup(e => e.Process(It.IsAny<IFlowData>())).Throws(expected);
        var builder = new PipelineBuilder(NullLoggerFactory.Instance).SetSuppressProcessExceptions(false);
        if (parallel) builder.AddFlowElementsParallel(first.Object, second.Object);
        else builder.AddFlowElement(first.Object).AddFlowElement(second.Object);
        using var pipeline = builder.Build();
        using var data = pipeline.CreateFlowData();
        var error = Assert.ThrowsExactly<AggregateException>(() => data.Process());
        Assert.HasCount(1, error.InnerExceptions);
        Assert.AreSame(expected, error.InnerExceptions.Single());
        second.Verify(e => e.Process(data), Times.Once());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PreCancelledDataSkipsBothEngines(bool parallel)
    {
        var first = Element("first");
        var second = Element("second");
        var builder = new PipelineBuilder(NullLoggerFactory.Instance);
        if (parallel) builder.AddFlowElementsParallel(first.Object, second.Object);
        else builder.AddFlowElement(first.Object).AddFlowElement(second.Object);
        using var pipeline = builder.Build();
        using var data = pipeline.CreateFlowData();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        data.SetStopToken(cancellation.Token);
        data.Process();
        first.Verify(e => e.Process(It.IsAny<IFlowData>()), Times.Never());
        second.Verify(e => e.Process(It.IsAny<IFlowData>()), Times.Never());
    }
}
