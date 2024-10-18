/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2023 51 Degrees Mobile Experts Limited, Davidson House,
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
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Core.Tests.HelperClasses;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Core.Tests.FlowElements
{
    /// <summary>
    /// Testing the <see cref="Core.FlowElements.Pipeline"/> class.
    /// </summary>
    [TestClass]
    public class PipelineTest
    {
        private Mock<ILogger<Core.FlowElements.Pipeline>> _logger =
            new Mock<ILogger<Core.FlowElements.Pipeline>>();

        private static Mock<IFlowElement> GetMockFlowElement()
        {
            var element = new Mock<IFlowElement>();
            element.SetupGet(e => e.ElementDataKey).Returns("test");
            element.Setup(e => e.Properties).Returns(new List<IElementPropertyMetaData>());
            return element;
        }

        /// <summary>
        /// Test that the pipeline functions correctly when it contains 
        /// 2 sequential flow elements.
        /// </summary>
        [TestMethod]
        public void Pipeline_Process_SequenceOfTwo()
        {
            // Arrange
            var element1 = GetMockFlowElement();
            var element2 = GetMockFlowElement();

            // Configure the elements
            element1.Setup(e => e.Process(It.IsAny<IFlowData>())).Callback((IFlowData d) =>
            {
                var tempdata = d.GetOrAdd("element1", (p) => new TestElementData(p));
                tempdata["key"] = "done";
            });
            element2.Setup(e => e.Process(It.IsAny<IFlowData>())).Callback((IFlowData d) =>
            {
                var tempdata = d.GetOrAdd("element2", (p) => new TestElementData(p));
                tempdata["key"] = "done";
            });

            // Create the pipeline
            var pipeline = CreatePipeline(
                false,
                false,
                element1.Object,
                element2.Object);
            // Don't create the flow data via the pipeline as we just want
            // to test Process.
            using IFlowData data = StaticFactories.CreateFlowData(pipeline);
            data.Process();

            // Act            
            pipeline.Process(data);

            // Assert
            Assert.IsTrue(data.Errors == null || data.Errors.Count == 0, "Expected no errors");
            // Check that the resulting data has the expected values
            Assert.IsTrue(data.GetDataKeys().Contains("element1"), "data from element 1 is missing in the result");
            Assert.IsTrue(data.GetDataKeys().Contains("element2"), "data from element 2 is missing in the result");
            Assert.AreEqual("done", data.Get("element1")["key"].ToString());
            Assert.AreEqual("done", data.Get("element2")["key"].ToString());
            // Check that element 1 was called before element 2.
            element2.Verify(e => e.Process(It.Is<IFlowData>(d => data.GetDataKeys().Contains("element1"))),
                "element 1 should have been called before element 2.");
        }

        [TestMethod]
        public void Pipeline_Process_SequenceOfTwo_WithCancellation()
        {
            // Arrange
            var element1 = GetMockFlowElement();
            var element2 = GetMockFlowElement();
            var tokenSource = new CancellationTokenSource();

            // Configure the elements
            element1.Setup(e => e.Process(It.IsAny<IFlowData>())).Callback((IFlowData d) =>
            {
                var tempdata = d.GetOrAdd("element1", (p) => new TestElementData(p));
                tempdata["key"] = "done";
                tokenSource.Cancel();
            });
            element2.Setup(e => e.Process(It.IsAny<IFlowData>())).Callback((IFlowData d) =>
            {
                var tempdata = d.GetOrAdd("element2", (p) => new TestElementData(p));
                tempdata["key"] = "done";
            });

            // Create the pipeline
            var pipeline = CreatePipeline(
                false,
                false,
                element1.Object,
                element2.Object);
            // Don't create the flow data via the pipeline as we just want
            // to test Process.
            using IFlowData data = StaticFactories.CreateFlowData(pipeline);
            
            // Act            
            try
            {
                data.Process(tokenSource.Token);
                Assert.Fail($"{nameof(pipeline.Process)} didn't throw.");
            }
            catch (OperationCanceledException)
            {
                // nop
            }

            // Assert
            Assert.IsTrue(data.Errors == null || data.Errors.Count == 0, "Expected no errors");
            // Check that the resulting data has the expected values
            Assert.IsTrue(data.GetDataKeys().Contains("element1"),
                "data from element 1 is missing in the result");
            Assert.IsFalse(data.GetDataKeys().Contains("element2"), 
                "data from element 2 is present in the result");
            Assert.AreEqual("done", data.Get("element1")["key"].ToString(),
                "element 1 result mismatch.");
            // Check that element 1 was called before element 2.
            element2.Verify(e => e.Process(It.Is<IFlowData>(d => data.GetDataKeys().Contains("element1"))),
                Times.Never(), "element 2. should never be called.");
        }

        /// <summary>
        /// Check that pipeline disposes of it's elements correctly if 
        /// configured to do so.
        /// </summary>
        [TestMethod]
        public void Pipeline_Dispose()
        {
            // Arrange
            var element1 = GetMockFlowElement();
            var element2 = GetMockFlowElement();

            // Create the pipeline
            var pipeline = CreatePipeline(
                true,
                false,
                element1.Object,
                element2.Object);

            // Act
            pipeline.Dispose();

            // Assert
            element1.Verify(e => e.Dispose(), Times.Once());
            element2.Verify(e => e.Dispose(), Times.Once());
        }

        [TestMethod]
        /// <summary>
        /// Check that the 'EvidenceKeys' property on the Pipeline
        /// works as expected.
        /// </summary>
        public void Pipeline_GetKeys()
        {
            var element1 = GetMockFlowElement();
            var element2 = GetMockFlowElement();
            // Configure the elements to return one key each
            element1.Setup(e => e.EvidenceKeyFilter).Returns(
                new EvidenceKeyFilterWhitelist(new List<string>() { "key1" }));
            element2.Setup(e => e.EvidenceKeyFilter).Returns(
                new EvidenceKeyFilterWhitelist(new List<string>() { "key2" }));

            // Create the pipeline
            var pipeline = CreatePipeline(
                false,
                false,
                element1.Object,
                element2.Object);

            // Get the keys from the pipeline
            var result = pipeline.EvidenceKeyFilter;

            // Check that the result is as expected.
            Assert.IsTrue(result.Include("key1"));
            Assert.IsTrue(result.Include("key2"));
            Assert.IsFalse(result.Include("key3"));
        }

        [TestMethod]
        /// <summary>
        /// Test pipeline handling <see cref="PropertiesNotYetLoadedException"/>
        /// </summary>
        public void Pipeline_PropertiesTest()
        {
            int[] propertyCalls = new int[] { 0 };

            var element1 = new Mock<IFlowElement>();
            element1.SetupGet(e => e.ElementDataKey).Returns("unstable");
            element1.Setup(e => e.Properties).Returns(() =>
            {
                propertyCalls[0] += 1;
                if (propertyCalls[0] <= 2)
                {
                    throw new PropertiesNotYetLoadedException("Properties not loaded yet, try again later!");
                }
                return new List<IElementPropertyMetaData>()
                {
                    new ElementPropertyMetaData(element1.Object, "unstable_property", typeof(string), true),
                };
            });

            var element2 = new Mock<IFlowElement>();
            element2.SetupGet(e => e.ElementDataKey).Returns("stable");
            element2.Setup(e => e.Properties).Returns(new List<IElementPropertyMetaData>()
            {
                new ElementPropertyMetaData(element2.Object, "stable_property", typeof(string), true),
            });

            // Create the pipeline, properties will be read once
            var pipeline = CreatePipeline(
                false,
                true,
                element1.Object,
                element2.Object);
            Assert.AreEqual(1, propertyCalls[0]);

            // Read properties manually, but still throw `PropertiesNotYetLoadedException`
            var pipelineProps1 = pipeline.ElementAvailableProperties;
            Assert.AreEqual(2, propertyCalls[0]);
            Assert.AreEqual(1, pipelineProps1.Count);
            Assert.IsTrue(pipelineProps1["stable"]["stable_property"].Available);

            // Read without throwing `PropertiesNotYetLoadedException`
            var pipelineProps2 = pipeline.ElementAvailableProperties;
            Assert.AreEqual(3, propertyCalls[0]);
            Assert.AreEqual(2, pipelineProps2.Count);
            Assert.IsTrue(pipelineProps2["stable"]["stable_property"].Available);
            Assert.IsTrue(pipelineProps2["unstable"]["unstable_property"].Available);

            // Read one last time, the result should be already cached
            var pipelineProps3 = pipeline.ElementAvailableProperties;
            Assert.AreEqual(3, propertyCalls[0]);
            Assert.AreEqual(2, pipelineProps3.Count);
            Assert.IsTrue(pipelineProps3["stable"]["stable_property"].Available);
            Assert.IsTrue(pipelineProps3["unstable"]["unstable_property"].Available);
        }

        [TestMethod]
        /// <summary>
        /// Check that an exception being thrown by a flow element will 
        /// bubble up to be thrown by the Process method.
        /// </summary>
        public void Pipeline_ExceptionDuringProcessingDontSuppress()
        {
            var element1 = GetMockFlowElement();
            var errors = new List<IFlowError>() {
                new FlowError(new Exception("Test"), element1.Object)
            };
            var data = new Mock<IFlowData>();

            // Configure the flow data to return errors.
            data.SetupGet(d => d.Errors).Returns(errors);

            // Create the pipeline
            var pipeline = CreatePipeline(
                false,
                true,
                element1.Object);

            // Start processing
            pipeline.Process(data.Object);
        }

        [TestMethod]
        /// <summary>
        /// Check that an exception being thrown by a flow element will 
        /// bubble up to be thrown by the Process method.
        /// </summary>
        public void Pipeline_ExceptionDuringProcessingSuppress()
        {
            var element1 = GetMockFlowElement();
            var errors = new List<IFlowError>() {
                new FlowError(new Exception("TEST"), element1.Object)
            };
            var data = new Mock<IFlowData>();

            // Configure the flow data to return errors.
            data.SetupGet(d => d.Errors).Returns(errors);

            // Create the pipeline
            var pipeline = CreatePipeline(
                false,
                false,
                element1.Object);

            // Start processing
            Exception exception = null;
            try {
                pipeline.Process(data.Object);
            }
            catch (Exception e)
            {
                exception = e;
            }

            // Check that the correct exception was thrown.
            Assert.IsNotNull(exception,
                "The exception did not bubble up to be thrown by the process" +
                "method.");
            Assert.IsInstanceOfType(
                exception,
                typeof(AggregateException),
                $"An exception of type '{exception.GetType()}' was thrown, " +
                $"the type should have been '{typeof(AggregateException)}'.");
            Assert.AreEqual(
                1,
                ((AggregateException)exception).InnerExceptions.Count,
                "The incorrect number of inner exceptions were added.");
            Assert.AreEqual(
                "TEST",
                ((AggregateException)exception).InnerExceptions[0].Message,
                "The correct exception message was not thrown.");
        }

        [TestMethod]
        /// <summary>
        /// Check that an exception being thrown by a flow element will 
        /// result in the AddError method being called on FlowData and that
        /// the exception is suppressed.
        /// </summary>
        public void Pipeline_ExceptionDuringProcessingAdd()
        {
            var element1 = GetMockFlowElement();
            var element2 = GetMockFlowElement();
            var data = new Mock<IFlowData>();

            // Configure element 2 to throw an exception.
            element2.Setup(e => e.Process(It.IsAny<IFlowData>()))
                .Throws(new Exception("TEST"));

            // Create the pipeline
            var pipeline = CreatePipeline(
                false,
                true,
                element1.Object,
                element2.Object);

            // Start processing
            pipeline.Process(data.Object);

            // Check that add error was called with the expected exception 
            // and flow element.
            data.Verify(d => d.AddError(
                It.Is<Exception>(ex => ex.Message == "TEST"),
                element2.Object),
                Times.Once());
        }

        /// <summary>
        /// Check that the GetPropertyMetaData method works as expected
        /// for a simple case.
        /// i.e. returning the requested meta data.
        /// </summary>
        [TestMethod]
        public void Pipeline_GetPropertyMetaData()
        {
            var element1 = GetMockFlowElement();
            var element2 = GetMockFlowElement();
            element1.Setup(e => e.Properties).Returns(new List<IElementPropertyMetaData>()
            {
                new ElementPropertyMetaData(element1.Object, "testproperty", typeof(string), true),
                new ElementPropertyMetaData(element1.Object, "anotherproperty", typeof(string), true)
            });
            element2.Setup(e => e.Properties).Returns(new List<IElementPropertyMetaData>()
            {
                new ElementPropertyMetaData(element2.Object, "testproperty2", typeof(string), true),
                new ElementPropertyMetaData(element2.Object, "anotherproperty2", typeof(string), true)
            });
            var data = new Mock<IFlowData>();

            // Create the pipeline
            var pipeline = CreatePipeline(
                false,
                true,
                element1.Object,
                element2.Object);

            // Get the requested property meta data
            var metadata = pipeline.GetMetaDataForProperty("testproperty");

            Assert.IsNotNull(metadata);
            Assert.AreEqual("testproperty", metadata.Name);
        }


        /// <summary>
        /// Check that the GetPropertyMetaData method throws an exception
        /// when no matching property exists
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(PipelineDataException))]
        public void Pipeline_GetPropertyMetaData_None()
        {
            var element1 = GetMockFlowElement();
            var element2 = GetMockFlowElement();
            var data = new Mock<IFlowData>();

            // Create the pipeline
            var pipeline = CreatePipeline(
                false,
                true,
                element1.Object,
                element2.Object);

            // Get the requested property meta data
            var metadata = pipeline.GetMetaDataForProperty("noproperty");
        }

        /// <summary>
        /// Check that the GetPropertyMetaData method throws an exception
        /// when multiple properties match
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(PipelineDataException))]
        public void Pipeline_GetPropertyMetaData_Multiple()
        {
            var element1 = GetMockFlowElement();
            element1.Setup(e => e.Properties).Returns(new List<IElementPropertyMetaData>()
            {
                new ElementPropertyMetaData(element1.Object, "testproperty", typeof(string), true)
            });
            var element2 = GetMockFlowElement();
            element2.Setup(e => e.Properties).Returns(new List<IElementPropertyMetaData>()
            {
                new ElementPropertyMetaData(element2.Object, "testproperty", typeof(string), true)
            });
            var data = new Mock<IFlowData>();

            // Create the pipeline
            var pipeline = CreatePipeline(
                false,
                true,
                element1.Object,
                element2.Object);

            // Get the requested property meta data
            var metadata = pipeline.GetMetaDataForProperty("testproperty");
        }

        /// <summary>
        /// Check that the GetPropertyMetaData method is thread-safe.
        /// </summary>
        [TestMethod]
        public void Pipeline_GetPropertyMetaData_Concurrent()
        {
            if (Environment.ProcessorCount < 2)
            {
                Assert.Inconclusive("This test cannot be run on a machine with less that 2 processing cores");
            }

            var element1 = GetMockFlowElement();
            element1.Setup(e => e.Properties).Returns(new List<IElementPropertyMetaData>()
            {
                new ElementPropertyMetaData(element1.Object, "testproperty", typeof(string), true)
            });
            var data = new Mock<IFlowData>();

            int repeatLimit = 100;
            // This test can just happen to work correctly by chance so
            // we repeat it 100 times in order to try and make sure
            // we eliminate the element of chance.
            // Testing has indicated a failure rate of around 50%, so
            // this should be sufficient to catch problems the vast
            // majority of the time.
            for (int repeatCount = 0; repeatCount < repeatLimit; repeatCount++)
            {
                // Create the pipeline
                var pipeline = CreatePipeline(
                    false,
                    true,
                    element1.Object);

                // Get the requested property meta data on two
                // threads simultaneously.
                var threads = 2;
                Task[] tasks = new Task[threads];
                for (int i = 0; i < threads; i++)
                {
                    tasks[i] = Task.Run(() =>
                    {
                        var metadata = pipeline.GetMetaDataForProperty("testproperty");
                    });
                }
                Task.WaitAll(tasks);
            }
        }

        [TestMethod]
        public void Pipeline_HasExpectedElementAfter()
        {
            var element1 = new TestElement();
            var element2 = new ListSplitterElement(new List<string>(), 1);

            var pipeline = CreatePipeline(false, false, element1, element2);

            // Verify the 'HasExpectedElementAfter' method returns the expected results
            Assert.IsTrue(pipeline.HasExpectedElementAfter<TestElement, ListSplitterElement>(),
                "ListSplitterElement should be after TestElement");
            Assert.IsFalse(pipeline.HasExpectedElementAfter<ListSplitterElement, TestElement>(),
                "TestElement should not be after ListSplitterElement");
            // Check that the method still works when using interfaces.
            Assert.IsTrue(pipeline.HasExpectedElementAfter<ITestElement, ListSplitterElement>(),
                "ListSplitterElement should be after ITestElement");
            Assert.IsFalse(pipeline.HasExpectedElementAfter<ListSplitterElement, ITestElement>(),
                "ITestElement should not be after ListSplitterElement");
            // If the element is not in the pipeline, return false.
            Assert.IsFalse(pipeline.HasExpectedElementAfter<StopElement, TestElement>());
            // If the 'expected element' is not in the pipeline, return false.
            Assert.IsFalse(pipeline.HasExpectedElementAfter<TestElement, StopElement>());
        }

        /// <summary>
        /// Helper method to create a new pipeline instance.
        /// </summary>
        /// <param name="autoDispose">
        /// Set to true to enable the auto dispose option.
        /// </param>
        /// <param name="suppressExceptions">
        /// Set to true to suppress exceptions in the process
        /// method.
        /// </param>
        /// <param name="flowElements">
        /// The flow elements to add to the pipeline.
        /// </param>
        /// <returns>
        /// A new <see cref="Core.FlowElements.Pipeline"/>.
        /// </returns>
        private Core.FlowElements.Pipeline CreatePipeline(
            bool autoDispose,
            bool suppressExceptions,
            params IFlowElement[] flowElements)
        {
            return new Core.FlowElements.Pipeline(
                _logger.Object,
                StaticFactories.CreateFlowData,
                new List<IFlowElement>(flowElements),
                autoDispose,
                suppressExceptions);
        }
    }

}
