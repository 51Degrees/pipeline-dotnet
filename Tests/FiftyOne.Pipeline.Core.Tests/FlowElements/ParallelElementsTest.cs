/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2025 51 Degrees Mobile Experts Limited, Davidson House,
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
        /// Each of the three elements will record the start time, 
        /// wait for 1 second and then record the end time.
        /// If the elements are being run in parallel then all the start times
        /// should be before all the end times and the time between start
        /// and end for each element should be approx 1 second.
        /// </summary>
        [TestMethod]
        public void ParallelElements_ThreeElements_ValidateParallel()
        {
            if (Environment.ProcessorCount < 4)
            {
                Assert.Inconclusive("This test cannot be run on a machine with less that 4 processing cores");
            }

            // Arrange
            var element1 = new Mock<IFlowElement>();
            var element2 = new Mock<IFlowElement>();
            var element3 = new Mock<IFlowElement>();

            element1.Setup(e => e.Process(It.IsAny<IFlowData>())).Callback((IFlowData d) =>
            {
                var tempdata = d.GetOrAdd("element1", (p) => new TestElementData(p));
                tempdata["start"] = DateTime.UtcNow;
                DateTime end = DateTime.UtcNow.AddSeconds(1);
                SpinWait.SpinUntil(() => DateTime.UtcNow >= end);
                tempdata["end"] = DateTime.UtcNow;
            });
            element2.Setup(e => e.Process(It.IsAny<IFlowData>())).Callback((IFlowData d) =>
            {
                var tempdata = d.GetOrAdd("element2", (p) => new TestElementData(p));
                tempdata["start"] = DateTime.UtcNow;
                DateTime end = DateTime.UtcNow.AddSeconds(1);
                SpinWait.SpinUntil(() => DateTime.UtcNow >= end);
                tempdata["end"] = DateTime.UtcNow;
            });
            element3.Setup(e => e.Process(It.IsAny<IFlowData>())).Callback((IFlowData d) =>
            {
                var tempdata = d.GetOrAdd("element3", (p) => new TestElementData(p));
                tempdata["start"] = DateTime.UtcNow;
                DateTime end = DateTime.UtcNow.AddSeconds(1);
                SpinWait.SpinUntil(() => DateTime.UtcNow >= end);
                tempdata["end"] = DateTime.UtcNow;
            });

            _parallelElements = new ParallelElements(_logger.Object,
                element1.Object,
                element2.Object,
                element3.Object);

            IFlowData data = StaticFactories.CreateFlowData(_pipeline.Object);
            data.Process();

            // Act
            _parallelElements.Process(data);

            List<DateTime> startTimes = new List<DateTime>();
            startTimes.Add((DateTime)data.Get("element1")["start"]);
            startTimes.Add((DateTime)data.Get("element2")["start"]);
            startTimes.Add((DateTime)data.Get("element3")["start"]);
            List<DateTime> endTimes = new List<DateTime>();
            endTimes.Add((DateTime)data.Get("element1")["end"]);
            endTimes.Add((DateTime)data.Get("element2")["end"]);
            endTimes.Add((DateTime)data.Get("element3")["end"]);

            // Assert
            Assert.IsTrue(data.Errors == null || data.Errors.Count == 0, "Expected no errors");
            Assert.IsTrue(startTimes.TrueForAll(dtStart => endTimes.TrueForAll(dtEnd => dtEnd > dtStart)),
                $"Start times [{string.Join(",", startTimes.Select(t => t.ToString("HH:mm:ss.ffffff")))}] " +
                $"not before end times [" +
                $"{string.Join(",", endTimes.Select(t => t.ToString("HH:mm:ss.ffffff")))}]");
            for (int i = 0; i < 3; i++)
            {
                double ms = endTimes[i].Subtract(startTimes[i]).TotalMilliseconds;
                Assert.IsTrue(ms < 1100 && ms > 1000, $"Element {i} total time taken was {ms}ms, " +
                    $"which is outside the expected range of 1000-1100");
            }
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

            // Configure element 2 to throw an exception.
            element2.Setup(e => e.Process(It.IsAny<IFlowData>()))
                .Throws(new Exception("TEST"));

            _parallelElements = new ParallelElements(_logger.Object,
                element1.Object,
                element2.Object);

            // Start processing
            _parallelElements.Process(data.Object);

            // Check that add error was called with the expected exception 
            // and flow element.
            data.Verify(d => d.AddError(
                It.Is<Exception>(ex => ex.Message == "TEST"),
                element2.Object),
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
