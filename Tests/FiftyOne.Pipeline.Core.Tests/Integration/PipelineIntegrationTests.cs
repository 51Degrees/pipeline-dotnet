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
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Core.Tests.Integration
{
    /// <summary>
    /// Integration tests that check the whole pipeline and associated 
    /// classes work as expected.
    /// </summary>
    [TestClass]
    public class PipelineIntegrationTests
    {
        /// <summary>
        /// Check the results are as expected when the pipeline contains a
        /// single element that multiplies by 5.
        /// </summary>
        [TestMethod]
        public void PipelineIntegration_SingleElement()
        {
            var fiveElement = new MultiplyByFiveElement();
            var pipeline = new PipelineBuilder(new LoggerFactory())
                .AddFlowElement(fiveElement)
                .Build();

            using (var flowData = pipeline.CreateFlowData())
            {
                flowData.AddEvidence(fiveElement.EvidenceKeys[0], 2);

                flowData.Process();

                Assert.AreEqual(10, flowData.GetFromElement(fiveElement).Result);
            }
        }

        /// <summary>
        /// Check the results are as expected when the pipeline contains a
        /// multiple elements
        /// </summary>
        [TestMethod]
        public void PipelineIntegration_MultipleElements()
        {
            var fiveElement = new MultiplyByFiveElement();
            var tenElement = new MultiplyByTenElement();
            var pipeline = new PipelineBuilder(new LoggerFactory())
                .AddFlowElement(fiveElement)
                .AddFlowElement(tenElement)
                .Build();

            using (var flowData = pipeline.CreateFlowData())
            {
                flowData.AddEvidence(fiveElement.EvidenceKeys[0], 2);

                flowData.Process();

                Assert.AreEqual(10, flowData.GetFromElement(fiveElement).Result);
                Assert.AreEqual(20, flowData.GetFromElement(tenElement).Result);
            }
        }

        /// <summary>
        /// Check the results are as expected when the pipeline contains a
        /// multiple elements executing in parallel
        /// </summary>
        [TestMethod]
        public void PipelineIntegration_MultipleElementsParallel()
        {
            var fiveElement = new MultiplyByFiveElement();
            var tenElement = new MultiplyByTenElement();
            var pipeline = new PipelineBuilder(new LoggerFactory())
                .AddFlowElementsParallel(fiveElement, tenElement)
                .Build();

            using (var flowData = pipeline.CreateFlowData())
            {
                flowData.AddEvidence(fiveElement.EvidenceKeys[0], 2);

                flowData.Process();

                Assert.AreEqual(10, flowData.GetFromElement(fiveElement).Result);
                Assert.AreEqual(20, flowData.GetFromElement(tenElement).Result);
            }
        }

        /// <summary>
        /// Check that the stop flag prevents execution of any subsequent
        /// flow elements
        /// </summary>
        [TestMethod]
        public void PipelineIntegration_StopFlag()
        {
            // Configure the pipeline
            var stopElement = new StopElement();
            var testElement = new Mock<IFlowElement>();
            testElement.SetupGet(e => e.ElementDataKey).Returns("test");
            testElement.SetupGet(e => e.Properties).Returns(new List<IElementPropertyMetaData>());
            var pipeline = new PipelineBuilder(new LoggerFactory())
                .AddFlowElement(stopElement)
                .AddFlowElement(testElement.Object)
                .Build();

            // Create and process flow data
            using (var flowData = pipeline.CreateFlowData())
            {
                flowData.Process();

                // Check that the stop flag is set
#pragma warning disable CS0618 // Type or member is obsolete
                // This usage will be replaced once the Cancellation Token
                // mechanism is available.
                Assert.IsTrue(flowData.Stop);
#pragma warning restore CS0618 // Type or member is obsolete
            }
            // Check that the second element was never processed
            testElement.Verify(e => e.Process(It.IsAny<IFlowData>()), Times.Never());
        }
    }
}
