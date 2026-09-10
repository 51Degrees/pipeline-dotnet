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

using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Core.Tests.HelperClasses;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Core.Tests.FlowElements
{
    /// <summary>
    /// Tests for the tracing spans emitted around flow element processing.
    /// </summary>
    [TestClass]
    // The activity listener is process-wide, so tests running in parallel
    // would record each other's spans.
    [DoNotParallelize]
    public class ElementTracingTests
    {
        private Mock<ILogger<Core.FlowElements.Pipeline>> _logger =
            new Mock<ILogger<Core.FlowElements.Pipeline>>();

        /// <summary>
        /// Records every span of the pipeline's activity source that stops
        /// while the collector is alive.
        /// </summary>
        private sealed class ActivityCollector : IDisposable
        {
            public ConcurrentBag<Activity> Stopped { get; } =
                new ConcurrentBag<Activity>();

            private readonly ActivityListener _listener;

            public ActivityCollector(
                ActivitySamplingResult sampling =
                    ActivitySamplingResult.AllDataAndRecorded)
            {
                _listener = new ActivityListener
                {
                    ShouldListenTo = source =>
                        source.Name == Constants.TRACING_SOURCE_NAME,
                    Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                        sampling,
                    ActivityStopped = activity => Stopped.Add(activity),
                };
                ActivitySource.AddActivityListener(_listener);
            }

            public void Dispose()
            {
                _listener.Dispose();
            }
        }

        private static Mock<IFlowElement> MockElement(string dataKey)
        {
            var element = new Mock<IFlowElement>();
            element.SetupGet(e => e.ElementDataKey).Returns(dataKey);
            element.Setup(e => e.Properties)
                .Returns(new List<IElementPropertyMetaData>());
            return element;
        }

        private Core.FlowElements.Pipeline CreatePipeline(
            bool suppressExceptions,
            params IFlowElement[] flowElements)
        {
            return new Core.FlowElements.Pipeline(
                _logger.Object,
                StaticFactories.CreateFlowData,
                new List<IFlowElement>(flowElements),
                false,
                suppressExceptions);
        }

        /// <summary>
        /// Sequential processing produces one span per element, parented
        /// to the caller's current activity and carrying the element tags.
        /// </summary>
        [TestMethod]
        public void Process_SequentialElements_SpanPerElementUnderRoot()
        {
            using var collector = new ActivityCollector();
            var element1 = MockElement("test1");
            var element2 = MockElement("test2");
            using var pipeline = CreatePipeline(
                true, element1.Object, element2.Object);
            using var root = new Activity("root").Start();
            using var data = pipeline.CreateFlowData();

            data.Process();
            root.Stop();

            var spans = collector.Stopped
                .Where(a => a.TraceId == root.TraceId)
                .ToList();
            Assert.HasCount(2, spans);
            var first = spans.Single(
                a => a.OperationName == "element.test1");
            var second = spans.Single(
                a => a.OperationName == "element.test2");
            Assert.AreEqual(root.Id, first.ParentId);
            Assert.AreEqual(root.Id, second.ParentId);
            Assert.AreEqual("test1", first.GetTagItem("element.data_key"));
            Assert.IsNotNull(first.GetTagItem("element.type"));
            Assert.IsTrue(first.StartTimeUtc <= second.StartTimeUtc,
                "element.test1 should start before element.test2");
        }

        /// <summary>
        /// With nothing listening to the source, processing works.
        /// </summary>
        [TestMethod]
        public void Process_NoListener_ElementRuns()
        {
            var element = MockElement("test");
            using var pipeline = CreatePipeline(true, element.Object);
            using var data = pipeline.CreateFlowData();

            data.Process();

            element.Verify(
                e => e.Process(It.IsAny<IFlowData>()), Times.Once());
        }

        /// <summary>
        /// A throwing element marks its span as error; the error is still
        /// recorded in flow data and, with suppression on, not rethrown.
        /// </summary>
        [TestMethod]
        public void Process_ElementThrows_SuppressOn_SpanErrorErrorsRecorded()
        {
            using var collector = new ActivityCollector();
            var element = MockElement("crash");
            element.Setup(e => e.Process(It.IsAny<IFlowData>()))
                .Throws(new Exception("TEST"));
            using var pipeline = CreatePipeline(true, element.Object);
            using var root = new Activity("root").Start();
            using var data = pipeline.CreateFlowData();

            data.Process();
            root.Stop();

            var span = collector.Stopped
                .Single(a => a.TraceId == root.TraceId);
            Assert.AreEqual(ActivityStatusCode.Error, span.Status);
            Assert.AreEqual("TEST", span.StatusDescription);
            Assert.HasCount(1, data.Errors);
        }

        /// <summary>
        /// With suppression off the aggregate exception still propagates
        /// and the span is marked as error.
        /// </summary>
        [TestMethod]
        public void Process_ElementThrows_SuppressOff_SpanErrorAndThrows()
        {
            using var collector = new ActivityCollector();
            var element = MockElement("crash");
            element.Setup(e => e.Process(It.IsAny<IFlowData>()))
                .Throws(new Exception("TEST"));
            using var pipeline = CreatePipeline(false, element.Object);
            using var root = new Activity("root").Start();
            using var data = pipeline.CreateFlowData();

            Assert.ThrowsExactly<AggregateException>(() => data.Process());
            root.Stop();

            var span = collector.Stopped
                .Single(a => a.TraceId == root.TraceId);
            Assert.AreEqual(ActivityStatusCode.Error, span.Status);
        }

        /// <summary>
        /// When the sampler drops the trace, the activity exists for
        /// context propagation but asks for no data.
        /// </summary>
        [TestMethod]
        public void Process_SamplerDropsTrace_NotAllDataRequested()
        {
            using var collector = new ActivityCollector(
                ActivitySamplingResult.PropagationData);
            var element = MockElement("test");
            using var pipeline = CreatePipeline(true, element.Object);
            using var root = new Activity("root").Start();
            using var data = pipeline.CreateFlowData();

            data.Process();
            root.Stop();

            var span = collector.Stopped
                .Single(a => a.TraceId == root.TraceId);
            Assert.IsFalse(span.IsAllDataRequested,
                "the sampler decision must be respected");
        }

        /// <summary>
        /// A stop token that is already cancelled skips every element, so
        /// no spans are produced either.
        /// </summary>
        [TestMethod]
        public void Process_StopTokenAlreadyCancelled_NoSpans()
        {
            using var collector = new ActivityCollector();
            var element = MockElement("test");
            using var pipeline = CreatePipeline(true, element.Object);
            using var root = new Activity("root").Start();
            using var data = pipeline.CreateFlowData(
                new CancellationToken(true));

            data.Process();
            root.Stop();

            Assert.HasCount(0, collector.Stopped
                .Where(a => a.TraceId == root.TraceId).ToList());
            element.Verify(
                e => e.Process(It.IsAny<IFlowData>()), Times.Never());
        }

        /// <summary>
        /// Children of a parallel block each get their own span, siblings
        /// under the caller's activity; the wrapper itself has none.
        /// </summary>
        [TestMethod]
        public void Process_ParallelElements_SpanPerChildNoWrapperSpan()
        {
            using var collector = new ActivityCollector();
            var element1 = MockElement("par1");
            var element2 = MockElement("par2");
            var parallel = new ParallelElements(
                new Mock<ILogger<ParallelElements>>().Object,
                element1.Object,
                element2.Object);
            using var pipeline = CreatePipeline(true, parallel);
            using var root = new Activity("root").Start();
            using var data = pipeline.CreateFlowData();

            data.Process();
            root.Stop();

            var spans = collector.Stopped
                .Where(a => a.TraceId == root.TraceId)
                .ToList();
            Assert.HasCount(2, spans);
            Assert.IsNotNull(spans.Single(
                a => a.OperationName == "element.par1"));
            Assert.IsNotNull(spans.Single(
                a => a.OperationName == "element.par2"));
            Assert.IsTrue(spans.All(a => a.ParentId == root.Id),
                "children must be siblings under the caller's activity");
        }

        /// <summary>
        /// A throwing child in a parallel block marks its span as error
        /// and the error is collected exactly as before.
        /// </summary>
        [TestMethod]
        public void Process_ParallelChildThrows_SpanErrorErrorsRecorded()
        {
            using var collector = new ActivityCollector();
            var element1 = MockElement("par1");
            var element2 = MockElement("crash");
            element2.Setup(e => e.Process(It.IsAny<IFlowData>()))
                .Throws(new Exception("TEST"));
            var parallel = new ParallelElements(
                new Mock<ILogger<ParallelElements>>().Object,
                element1.Object,
                element2.Object);
            using var pipeline = CreatePipeline(true, parallel);
            using var root = new Activity("root").Start();
            using var data = pipeline.CreateFlowData();

            data.Process();
            root.Stop();

            var crashed = collector.Stopped.Single(
                a => a.TraceId == root.TraceId
                    && a.OperationName == "element.crash");
            Assert.AreEqual(ActivityStatusCode.Error, crashed.Status);
            Assert.AreEqual("TEST", crashed.StatusDescription);
            Assert.HasCount(1, data.Errors);
        }

        /// <summary>
        /// A failing element does not disturb the parentage of the spans
        /// that follow it.
        /// </summary>
        [TestMethod]
        public void Process_FirstElementThrows_NextSpanStillUnderRoot()
        {
            using var collector = new ActivityCollector();
            var element1 = MockElement("crash");
            element1.Setup(e => e.Process(It.IsAny<IFlowData>()))
                .Throws(new Exception("TEST"));
            var element2 = MockElement("after");
            using var pipeline = CreatePipeline(
                true, element1.Object, element2.Object);
            using var root = new Activity("root").Start();
            using var data = pipeline.CreateFlowData();

            data.Process();
            root.Stop();

            var after = collector.Stopped.Single(
                a => a.TraceId == root.TraceId
                    && a.OperationName == "element.after");
            Assert.AreEqual(root.Id, after.ParentId);
            var crashed = collector.Stopped.Single(
                a => a.TraceId == root.TraceId
                    && a.OperationName == "element.crash");
            Assert.AreEqual(ActivityStatusCode.Error, crashed.Status);
        }

        /// <summary>
        /// A throwing child in a parallel block with suppression off still
        /// propagates the aggregate exception and marks its span.
        /// </summary>
        [TestMethod]
        public void Process_ParallelChildThrows_SuppressOff_SpanErrorAndThrows()
        {
            using var collector = new ActivityCollector();
            var element1 = MockElement("par1");
            var element2 = MockElement("crash");
            element2.Setup(e => e.Process(It.IsAny<IFlowData>()))
                .Throws(new Exception("TEST"));
            var parallel = new ParallelElements(
                new Mock<ILogger<ParallelElements>>().Object,
                element1.Object,
                element2.Object);
            using var pipeline = CreatePipeline(false, parallel);
            using var root = new Activity("root").Start();
            using var data = pipeline.CreateFlowData();

            Assert.ThrowsExactly<AggregateException>(() => data.Process());
            root.Stop();

            var crashed = collector.Stopped.Single(
                a => a.TraceId == root.TraceId
                    && a.OperationName == "element.crash");
            Assert.AreEqual(ActivityStatusCode.Error, crashed.Status);
            Assert.IsNotNull(collector.Stopped.SingleOrDefault(
                a => a.TraceId == root.TraceId
                    && a.OperationName == "element.par1"));
        }

        /// <summary>
        /// Attaching and disposing listeners while requests process must
        /// neither throw nor leave a span running.
        /// </summary>
        [TestMethod]
        public void Process_ListenerChurn_NoThrowAndAllSpansStopped()
        {
            var started = new ConcurrentBag<Activity>();
            using var watcher = new ActivityListener
            {
                ShouldListenTo = source =>
                    source.Name == Constants.TRACING_SOURCE_NAME,
                Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                    ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => started.Add(activity),
            };
            ActivitySource.AddActivityListener(watcher);
            var element = MockElement("test");
            using var pipeline = CreatePipeline(true, element.Object);

            var stop = false;
            var churn = Task.Run(() =>
            {
                while (Volatile.Read(ref stop) == false)
                {
                    using var extra = new ActivityListener
                    {
                        ShouldListenTo = source =>
                            source.Name == Constants.TRACING_SOURCE_NAME,
                        Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                            ActivitySamplingResult.AllDataAndRecorded,
                    };
                    ActivitySource.AddActivityListener(extra);
                }
            });
            try
            {
                for (int i = 0; i < 200; i++)
                {
                    using var data = pipeline.CreateFlowData();
                    data.Process();
                }
            }
            finally
            {
                Volatile.Write(ref stop, true);
                churn.Wait();
            }

            Assert.IsTrue(started.All(a => a.IsStopped),
                "every span must be stopped by the end of its request");
        }
    }
}
