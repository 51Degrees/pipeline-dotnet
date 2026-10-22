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
using System.Linq;
using System.Threading;

namespace FiftyOne.Pipeline.Core.Tests.FlowElements
{
    [TestClass]
    public class ParallelElementsTest
    {
        private ParallelElements _parallelElements;

        private Mock<IPipelineInternal> _pipeline;
        private Mock<ILogger<ParallelElements>> _logger;

        [TestInitialize]
        public void Initialize()
        {
            _pipeline = new Mock<IPipelineInternal>();
            // We need to set the IsConcurrent property to return true so 
            // that the FlowData instance will be created with a 
            // concurrent dictionary instead of a regular one.
            _pipeline.Setup(p => p.IsConcurrent).Returns(true);
            _logger = new Mock<ILogger<ParallelElements>>();
        }

        /// <summary>
        /// Test ParallelElements does actually execute it's children
        /// in parallel.
        /// Each of the three elements signals a barrier and waits for the
        /// other two to reach it. The barrier can only be passed if all three
        /// elements are running at the same time, so this proves parallel
        /// execution without depending on timings. On a busy machine the
        /// thread pool may need to add threads before all three can start,
        /// which only delays the test. If the elements were run one after
        /// another, the first would never be joined by the others and the
        /// wait would time out.
        /// </summary>
        [TestMethod]
        public void ParallelElements_ThreeElements_ValidateParallel()
        {
            // Arrange
            var timeout = TimeSpan.FromSeconds(10);
            using var allStarted = new Barrier(3);
            var passedBarrier = new ConcurrentBag<bool>();
            Action<IFlowData> process = d =>
                passedBarrier.Add(allStarted.SignalAndWait(timeout));

            var element1 = new Mock<IFlowElement>();
            var element2 = new Mock<IFlowElement>();
            var element3 = new Mock<IFlowElement>();
            element1.Setup(e => e.Process(It.IsAny<IFlowData>())).Callback(process);
            element2.Setup(e => e.Process(It.IsAny<IFlowData>())).Callback(process);
            element3.Setup(e => e.Process(It.IsAny<IFlowData>())).Callback(process);

            _parallelElements = new ParallelElements(_logger.Object,
                element1.Object,
                element2.Object,
                element3.Object);

            IFlowData data = StaticFactories.CreateFlowData(_pipeline.Object);
            data.Process();

            // Act
            _parallelElements.Process(data);

            // Assert
            Assert.IsTrue(data.Errors == null || data.Errors.Count == 0, "Expected no errors");
            Assert.AreEqual(3, passedBarrier.Count, "Expected all three elements to run");
            Assert.IsTrue(passedBarrier.All(p => p),
                $"Expected all three elements to be running at the same " +
                $"time, but they did not all start within {timeout}");
        }

        [TestMethod]
        /// <summary>
        /// Check that the 'EvidenceKeys' property on ParallelElements
        /// works as expected.
        /// </summary>
        public void ParallelElements_GetKeys()
        {
            var element1 = new Mock<IFlowElement>();
            var element2 = new Mock<IFlowElement>();
            // Configure the elements to return one key each
            element1.Setup(e => e.EvidenceKeyFilter).Returns(
                new EvidenceKeyFilterWhitelist(new List<string>() { "key1" }));
            element2.Setup(e => e.EvidenceKeyFilter).Returns(
                new EvidenceKeyFilterWhitelist(new List<string>() { "key2" }));

            _parallelElements = new ParallelElements(_logger.Object,
                element1.Object,
                element2.Object);
            // Get the keys.
            var result = _parallelElements.EvidenceKeyFilter;
            
            // Check that the result is as expected.
            Assert.IsTrue(result.Include("key1"));
            Assert.IsTrue(result.Include("key2"));
            Assert.IsFalse(result.Include("key3"));
        }
        
        [TestMethod]
        /// <summary>
        /// Check that an exception being thrown by a flow element will 
        /// result in the AddError method being called on FlowData.
        /// </summary>
        public void ParallelElements_ExceptionDuringProcessing()
        {
            var element1 = new Mock<IFlowElement>();
            var element2 = new Mock<IFlowElement>();
            var data = new Mock<IFlowData>();
            _pipeline.Setup(p => p.SuppressProcessExceptions).Returns(false);
            data.Setup(d => d.Pipeline).Returns(_pipeline.Object);

            // Configure element 2 to throw an exception.
            element2.Setup(e => e.Process(It.IsAny<IFlowData>()))
                .Throws(new Exception("TEST"));

            _parallelElements = new ParallelElements(_logger.Object,
                element1.Object,
                element2.Object);

            // Start processing
            _parallelElements.Process(data.Object);

            // Check that add error was called with the expected exception
            // and flow element. Exceptions are not suppressed, so the error
            // is flagged for logging.
            data.Verify(d => d.AddError(
                It.Is<Exception>(ex => ex.Message == "TEST"),
                element2.Object,
                true,
                true),
                Times.Once());
        }

        [TestMethod]
        /// <summary>
        /// Check that an exception being thrown by a flow element is not
        /// flagged for error level logging when the pipeline is suppressing
        /// process exceptions (issue #280).
        /// </summary>
        public void ParallelElements_ExceptionDuringProcessingSuppressed()
        {
            var element1 = new Mock<IFlowElement>();
            var element2 = new Mock<IFlowElement>();
            var data = new Mock<IFlowData>();
            _pipeline.Setup(p => p.SuppressProcessExceptions).Returns(true);
            data.Setup(d => d.Pipeline).Returns(_pipeline.Object);

            // Configure element 2 to throw an exception.
            element2.Setup(e => e.Process(It.IsAny<IFlowData>()))
                .Throws(new Exception("TEST"));

            _parallelElements = new ParallelElements(_logger.Object,
                element1.Object,
                element2.Object);

            // Start processing
            _parallelElements.Process(data.Object);

            // Check that add error was called asking for the error not to
            // be logged.
            data.Verify(d => d.AddError(
                It.Is<Exception>(ex => ex.Message == "TEST"),
                element2.Object,
                true,
                false),
                Times.Once());
        }

        /// <summary>
        /// Check that ParallelElements disposes of it's elements correctly.
        /// </summary>
        [TestMethod]
        public void ParallelElements_Dispose()
        {
            // Arrange
            var element1 = new Mock<IFlowElement>();
            var element2 = new Mock<IFlowElement>();

            // Create the instance
            _parallelElements = new ParallelElements(_logger.Object,
                element1.Object,
                element2.Object);

            // Act
            _parallelElements.Dispose();

            // Assert
            element1.Verify(e => e.Dispose(), Times.Once());
            element2.Verify(e => e.Dispose(), Times.Once());
        }
    }
}
