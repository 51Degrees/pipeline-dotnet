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

using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using FiftyOne.Common.TestHelpers;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;

namespace FiftyOne.Pipeline.Engines.Tests.Data
{
    [TestClass]
    public class AspectDataBaseTests
    {
        private class TestData : AspectDataBase
        {
            public TestData(
                ILogger<AspectDataBase> logger,
                IPipeline pipeline,
                IAspectEngine engine,
                IMissingPropertyService missingPropertyService)
                : base(logger, pipeline, engine, missingPropertyService) { }
        }

        private TestData _data;
        private TestLogger<TestData> _logger;
        private Mock<IAspectEngine> _engine;
        private Mock<IPipeline> _pipeline;
        private Mock<IMissingPropertyService> _missingPropertyService;

        [TestInitialize]
        public void Initisalise()
        {
            _logger = new TestLogger<TestData>();
            _engine = new Mock<IAspectEngine>();
            _pipeline = new Mock<IPipeline>();
            _missingPropertyService = new Mock<IMissingPropertyService>();
            _missingPropertyService.Setup(m => m.GetMissingPropertyReason(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<IAspectEngine>>()))
                .Returns(new MissingPropertyResult()
                {
                    Description = "TEST",
                    Reason = MissingPropertyReason.Unknown
                });
            _data = new TestData(
                _logger,
                _pipeline.Object,
                _engine.Object,
                _missingPropertyService.Object);

        }

        /// <summary>
        /// Check that the base class will throw an
        /// <see cref="ArgumentNullException"/> if the indexer is passed
        /// a null property name
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void AspectData_Indexer_NullKey()
        {
            var result = _data[null];
        }

        /// <summary>
        /// Check that the indexers can be used to set and get a
        /// property value.
        /// </summary>
        [TestMethod]
        public void AspectData_Indexer_SetAndGet()
        {
            _data["testproperty"] = "TestValue";
            var result = _data["testproperty"];
            Assert.AreEqual("TestValue", result);
        }

        /// <summary>
        /// Check that the base class will throw a
        /// <see cref="PropertyMissingException"/> if the property
        /// is not present.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(PropertyMissingException))]
        public void AspectData_Indexer_GetMissing()
        {
            var result = _data["testproperty"];
        }

        /// <summary>
        /// When a property is missing AND the flow data reports an upstream
        /// error AND this aspect's data dictionary is empty, the heuristic
        /// in <see cref="AspectDataBase"/> should bypass the missing-property
        /// service and throw with reason
        /// <see cref="MissingPropertyReason.CloudRequestFailed"/>.
        /// </summary>
        [TestMethod]
        public void AspectData_Indexer_GetMissing_CloudRequestFailed_WhenErrorAndEmpty()
        {
            var failingElement = new Mock<IFlowElement>();
            var flowData = new Mock<IFlowData>();
            flowData.SetupGet(d => d.Errors).Returns(new List<IFlowError>
            {
                new FlowError(new Exception("upstream timeout"), failingElement.Object)
            });
            _data.SetFlowData(flowData.Object);

            var propertyMissingException = Assert.ThrowsException<PropertyMissingException>(
                () => { var _ = _data["testproperty"]; });

            Assert.AreEqual(MissingPropertyReason.CloudRequestFailed, propertyMissingException.Reason);
            Assert.AreEqual("testproperty", propertyMissingException.PropertyName);
            StringAssert.Contains(propertyMissingException.Message, "upstream timeout");
            // The missing-property service must NOT be consulted on this path
            // -- the heuristic owns the answer.
            _missingPropertyService.Verify(m => m.GetMissingPropertyReason(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<IAspectEngine>>()),
                Times.Never);
        }

        /// <summary>
        /// If the flow data has an upstream error but this aspect's data
        /// dictionary is non-empty (the engine populated something), the
        /// heuristic should NOT fire and the missing-property service is
        /// consulted as before.
        /// </summary>
        [TestMethod]
        public void AspectData_Indexer_GetMissing_DelegatesToService_WhenDataNotEmpty()
        {
            var failingElement = new Mock<IFlowElement>();
            var flowData = new Mock<IFlowData>();
            flowData.SetupGet(d => d.Errors).Returns(new List<IFlowError>
            {
                new FlowError(new Exception("anything"), failingElement.Object)
            });
            _data.SetFlowData(flowData.Object);
            _data["someOtherProp"] = "populated";

            var propertyMissingException = Assert.ThrowsException<PropertyMissingException>(
                () => { var _ = _data["testproperty"]; });

            Assert.AreEqual(MissingPropertyReason.Unknown, propertyMissingException.Reason);
            _missingPropertyService.Verify(m => m.GetMissingPropertyReason(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<IAspectEngine>>()),
                Times.Once);
        }

        /// <summary>
        /// If the flow data dictionary is empty but FlowData reports no
        /// errors (genuine 'not in resource key' / 'data tier' case), the
        /// heuristic must NOT fire -- delegate to the missing-property
        /// service so the user gets the correct, license-related reason.
        /// </summary>
        [TestMethod]
        public void AspectData_Indexer_GetMissing_DelegatesToService_WhenNoErrors()
        {
            var flowData = new Mock<IFlowData>();
            flowData.SetupGet(d => d.Errors).Returns(new List<IFlowError>());
            _data.SetFlowData(flowData.Object);

            var propertyMissingException = Assert.ThrowsException<PropertyMissingException>(
                () => { var _ = _data["testproperty"]; });

            Assert.AreEqual(MissingPropertyReason.Unknown, propertyMissingException.Reason);
            _missingPropertyService.Verify(m => m.GetMissingPropertyReason(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<IAspectEngine>>()),
                Times.Once);
        }

        /// <summary>
        /// If <see cref="AspectDataBase.SetFlowData"/> was never called
        /// (older callers, or aspect data created outside the standard
        /// engine pipeline), the heuristic must short-circuit cleanly
        /// rather than NRE -- delegating to the existing service path.
        /// </summary>
        [TestMethod]
        public void AspectData_Indexer_GetMissing_DelegatesToService_WhenFlowDataUnset()
        {
            var propertyMissingException = Assert.ThrowsException<PropertyMissingException>(
                () => { var _ = _data["testproperty"]; });

            Assert.AreEqual(MissingPropertyReason.Unknown, propertyMissingException.Reason);
            _missingPropertyService.Verify(m => m.GetMissingPropertyReason(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<IAspectEngine>>()),
                Times.Once);
        }

        /// <summary>
        /// The exception description should name the upstream flow element
        /// that errored, so a user reading the log knows which component
        /// to investigate -- not just that 'something went wrong'.
        /// </summary>
        [TestMethod]
        public void AspectData_CloudRequestFailed_DescriptionIdentifiesUpstreamElement()
        {
            var failingElement = new Mock<IFlowElement>();
            var flowData = new Mock<IFlowData>();
            flowData.SetupGet(d => d.Errors).Returns(new List<IFlowError>
            {
                new FlowError(new Exception("network timeout"), failingElement.Object)
            });
            _data.SetFlowData(flowData.Object);

            var propertyMissingException = Assert.ThrowsException<PropertyMissingException>(
                () => { var _ = _data["testproperty"]; });

            // Description must reference both the failing element type
            // and the underlying exception message.
            StringAssert.Contains(propertyMissingException.Message, failingElement.Object.GetType().Name);
            StringAssert.Contains(propertyMissingException.Message, "network timeout");
        }
    }
}
