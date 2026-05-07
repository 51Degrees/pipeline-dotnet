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

using FiftyOne.Pipeline.CloudRequestEngine.Services;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.CloudRequestEngine.Tests.Services
{
    [TestClass]
    public class CloudRequestMissingPropertyServiceTests
    {
        private Mock<IMissingPropertyService> _innerService;
        private CloudRequestMissingPropertyService _service;

        [TestInitialize]
        public void Init()
        {
            _innerService = new Mock<IMissingPropertyService>();
            _innerService
                .Setup(s => s.GetMissingPropertyReason(
                    It.IsAny<string>(), It.IsAny<IAspectEngine>()))
                .Returns(new MissingPropertyResult
                {
                    Reason = MissingPropertyReason.DataFileUpgradeRequired,
                    Description = "INNER",
                });
            _service = new CloudRequestMissingPropertyService(_innerService.Object);
        }

        private static Mock<ICloudAspectEngine> CloudEngine(
            bool hasLoadedProperties,
            params string[] propertyNames)
        {
            var engine = new Mock<ICloudAspectEngine>();
            engine.SetupGet(e => e.HasLoadedProperties).Returns(hasLoadedProperties);
            engine.SetupGet(e => e.ElementDataKey).Returns("device");
            var props = new List<IAspectPropertyMetaData>();
            foreach (var name in propertyNames)
            {
                var p = new Mock<IAspectPropertyMetaData>();
                p.SetupGet(m => m.Name).Returns(name);
                props.Add(p.Object);
            }
            engine.SetupGet(e => e.Properties).Returns(props);
            return engine;
        }

        /// <summary>
        /// Cloud engine, property exists in the engine's metadata but is
        /// missing from this request's data. The resource key allows it,
        /// so the only remaining explanation is a failed cloud request.
        /// </summary>
        [TestMethod]
        public void CloudEngine_PropertyInMetadata_ReturnsCloudRequestFailed()
        {
            var engine = CloudEngine(true, "PlatformName", "BrowserName");

            var result = _service.GetMissingPropertyReason("PlatformName", engine.Object);

            Assert.AreEqual(MissingPropertyReason.CloudRequestFailed, result.Reason);
            StringAssert.Contains(result.Description, "PlatformName");
            // Inner default service must NOT be consulted -- the cloud
            // service owns the answer for cloud engines.
            _innerService.Verify(s => s.GetMissingPropertyReason(
                It.IsAny<string>(), It.IsAny<IAspectEngine>()), Times.Never);
        }

        /// <summary>
        /// Cloud engine with metadata loaded but the requested property is
        /// not in the resource key's allowed list -- the genuine
        /// 'PropertyNotAccessibleWithResourceKey' case.
        /// </summary>
        [TestMethod]
        public void CloudEngine_PropertyNotInMetadata_ReturnsPropertyNotAccessibleWithResourceKey()
        {
            var engine = CloudEngine(true, "BrowserName");

            var result = _service.GetMissingPropertyReason("PlatformName", engine.Object);

            Assert.AreEqual(MissingPropertyReason.PropertyNotAccessibleWithResourceKey, result.Reason);
        }

        /// <summary>
        /// Cloud engine whose resource key grants no properties at all --
        /// the 'ProductNotAccessibleWithResourceKey' case.
        /// </summary>
        [TestMethod]
        public void CloudEngine_NoPropertiesAtAll_ReturnsProductNotAccessibleWithResourceKey()
        {
            var engine = CloudEngine(true);

            var result = _service.GetMissingPropertyReason("PlatformName", engine.Object);

            Assert.AreEqual(MissingPropertyReason.ProductNotAccessibleWithResourceKey, result.Reason);
        }

        /// <summary>
        /// Cloud engine but metadata not yet loaded -- defer to the inner
        /// default service so its existing 'metadata not loaded' handling
        /// applies.
        /// </summary>
        [TestMethod]
        public void CloudEngine_MetadataNotLoaded_DelegatesToInner()
        {
            var engine = CloudEngine(false);

            var result = _service.GetMissingPropertyReason("PlatformName", engine.Object);

            Assert.AreEqual("INNER", result.Description);
            _innerService.Verify(s => s.GetMissingPropertyReason(
                "PlatformName", engine.Object), Times.Once);
        }

        /// <summary>
        /// Non-cloud engine -- delegate to the inner default service so
        /// data-file engines retain their existing 'DataFileUpgradeRequired'
        /// reasoning.
        /// </summary>
        [TestMethod]
        public void NonCloudEngine_DelegatesToInner()
        {
            var engine = new Mock<IAspectEngine>();

            var result = _service.GetMissingPropertyReason("PlatformName", engine.Object);

            Assert.AreEqual(MissingPropertyReason.DataFileUpgradeRequired, result.Reason);
            Assert.AreEqual("INNER", result.Description);
            _innerService.Verify(s => s.GetMissingPropertyReason(
                "PlatformName", engine.Object), Times.Once);
        }

        /// <summary>
        /// Multi-engine overload should iterate engines and return the
        /// first non-Unknown reason. Cloud engine in the list wins over a
        /// generic non-cloud one.
        /// </summary>
        [TestMethod]
        public void MultiEngine_PicksFirstKnownReason()
        {
            var cloud = CloudEngine(true, "PlatformName");
            var nonCloud = new Mock<IAspectEngine>();
            var engines = new List<IAspectEngine> { cloud.Object, nonCloud.Object };

            var result = _service.GetMissingPropertyReason("PlatformName", engines);

            Assert.AreEqual(MissingPropertyReason.CloudRequestFailed, result.Reason);
        }
    }
}
