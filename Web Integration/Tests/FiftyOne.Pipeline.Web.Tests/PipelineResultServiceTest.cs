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
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Threading;

namespace FiftyOne.Pipeline.Web.Tests
{
    [TestClass]
    public class PipelineResultServiceTest
    {
        private static Mock<IFlowData> _flowData;

        private Mock<IPipeline> _pipeline;

        private Mock<IWebRequestEvidenceService> _evidenceService;

        private PipelineResultService _resultsService;

        /// <summary>
        /// Set up the test by creating a new results service and its
        /// dependencies.
        /// </summary>
        [TestInitialize]
        public void SetUp()
        {
            _pipeline = new Mock<IPipeline>();
            _flowData = new Mock<IFlowData>();
            _pipeline.Setup(p => p.CreateFlowData()).Returns(_flowData.Object);
            _evidenceService = new Mock<IWebRequestEvidenceService>();

            _resultsService = new PipelineResultService(
                _evidenceService.Object,
                _pipeline.Object);
        }

        /// <summary>
        /// Test that the process method calls the process method on the flow
        /// data.
        /// </summary>
        [TestMethod]
        public void PipelineResultsService_Process()
        {
            _resultsService.Process(new DefaultHttpContext());

            _flowData.Verify(
                f => f.Process(It.IsAny<CancellationToken>()),
                Times.Once,
                "The process method on flow data should have been called " +
                "once by the process method.");
        }

        /// <summary>
        /// Test that the process method adds the evidence to the flow data
        /// through the evidence service. This checks that the method was only
        /// called once, and that the parameters were correct.
        /// </summary>
        [TestMethod]
        public void PipelineResultsService_AddEvidence()
        {
            HttpContext context = new DefaultHttpContext();
            _resultsService.Process(context);

            _evidenceService.Verify(e => e.AddEvidenceFromRequest(
                It.IsAny<IFlowData>(),
                It.IsAny<HttpRequest>()),
                Times.Once,
                "Add evidence should have been called once by the process method.");
            _evidenceService.Verify(e => e.AddEvidenceFromRequest(
                _flowData.Object,
                context.Request),
                Times.Once,
                "Add evidence was not called with the correct parameters.");
        }

        /// <summary>
        /// Test that the processed flow data was added to the HTTP context
        /// making it available.
        /// </summary>
        [TestMethod]
        public void PipelineResultsService_AddFlowData()
        {
            HttpContext context = new DefaultHttpContext();
            _resultsService.Process(context);

            Assert.AreEqual(
                _flowData.Object,
                context.Items[Constants.HTTPCONTEXT_FLOWDATA],
                "The flow data was not added to the HTTP context.");
        }
    }
}
