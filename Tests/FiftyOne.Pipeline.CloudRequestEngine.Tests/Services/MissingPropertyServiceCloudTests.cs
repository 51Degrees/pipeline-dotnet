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

using FiftyOne.Common.TestHelpers;
using FiftyOne.Pipeline.CloudRequestEngine.Services;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.CloudRequestEngine.Tests.Services
{
    [TestClass]
    public class MissingPropertyServiceCloudTests
    {
        private IMissingPropertyService _service;

        [TestInitialize]
        public void Initialise()
        {
            _service = MissingPropertyServiceCloud.Instance;
        }

        /// <summary>
        /// When a cloud engine reports zero properties for the requested
        /// element, the resource key gives no access to any property
        /// under this engine — report
        /// <see cref="MissingPropertyReason.ProductNotAccessibleWithResourceKey"/>.
        /// </summary>
        [TestMethod]
        public void MissingPropertyServiceCloud_GetReason_ProductNotInResource()
        {
            Mock<ICloudAspectEngine> engine = new Mock<ICloudAspectEngine>();
            engine.SetupGet(e => e.ElementDataKey).Returns("testElement");
            engine.SetupGet(e => e.HasLoadedProperties).Returns(true);
            engine.SetupGet(e => e.Properties).Returns(new List<IAspectPropertyMetaData>());

            var result = _service.GetMissingPropertyReason(
                "otherProperty",
                engine.Object);

            Assert.AreEqual(
                MissingPropertyReason.ProductNotAccessibleWithResourceKey,
                result.Reason);
        }

        /// <summary>
        /// When the cloud engine reports some properties but not the
        /// requested one, the resource key gives access to other
        /// properties for this engine but not the requested one — report
        /// <see cref="MissingPropertyReason.PropertyNotAccessibleWithResourceKey"/>.
        /// </summary>
        [TestMethod]
        public void MissingPropertyServiceCloud_GetReason_PropertyNotInResource()
        {
            Mock<ICloudAspectEngine> engine = new Mock<ICloudAspectEngine>();
            engine.SetupGet(e => e.ElementDataKey).Returns("testElement");
            engine.SetupGet(e => e.HasLoadedProperties).Returns(true);
            ConfigureCloudProperty(engine.As<IAspectEngine>(), "testProperty");

            var result = _service.GetMissingPropertyReason(
                "otherProperty",
                engine.Object);

            Assert.AreEqual(
                MissingPropertyReason.PropertyNotAccessibleWithResourceKey,
                result.Reason);
        }

        /// <summary>
        /// Cloud engines never report
        /// <see cref="MissingPropertyReason.DataFileUpgradeRequired"/> —
        /// that reason is exclusive to on-premise engines, where the
        /// service can compare the engine's data tier against the
        /// property's <c>DataTiersWherePresent</c> list. Cloud property
        /// metadata does not populate <c>DataTiersWherePresent</c>, so
        /// the cloud service must never derive this reason.
        /// </summary>
        [TestMethod]
        public void MissingPropertyServiceCloud_GetReason_NeverDataFileUpgradeRequired()
        {
            Mock<ICloudAspectEngine> engine = new Mock<ICloudAspectEngine>();
            engine.SetupGet(e => e.ElementDataKey).Returns("testElement");
            engine.SetupGet(e => e.DataSourceTier).Returns("cloud");
            engine.SetupGet(e => e.HasLoadedProperties).Returns(true);
            ConfigureCloudProperty(engine.As<IAspectEngine>(), "testProperty");

            var result = _service.GetMissingPropertyReason(
                "testProperty",
                engine.Object);

            Assert.AreNotEqual(
                MissingPropertyReason.DataFileUpgradeRequired,
                result.Reason,
                $"Cloud service must never return DataFileUpgradeRequired; got {result.Reason}.");
        }

        /// <summary>
        /// When the cloud-request-failed marker is set on the aspect
        /// data, the cloud service must short-circuit to
        /// <see cref="MissingPropertyReason.CloudRequestFailed"/> without
        /// running its standard heuristics. Other resolution paths could
        /// otherwise produce misleading reasons for a transient cloud
        /// failure.
        /// </summary>
        [TestMethod]
        public void MissingPropertyServiceCloud_GetReason_CloudRequestFailed_ShortCircuits()
        {
            var aspectData = new TestAspectData();
            aspectData.MarkCloudRequestFailed();

            Mock<ICloudAspectEngine> engine = new Mock<ICloudAspectEngine>();
            engine.SetupGet(e => e.ElementDataKey).Returns("testElement");
            engine.SetupGet(e => e.HasLoadedProperties).Returns(true);
            ConfigureCloudProperty(engine.As<IAspectEngine>(), "testProperty");
            var engines = new List<IAspectEngine> { engine.Object };

            var result = _service.GetMissingPropertyReason(
                "testProperty",
                engines,
                aspectData);

            Assert.AreEqual(
                MissingPropertyReason.CloudRequestFailed,
                result.Reason);
        }

        /// <summary>
        /// Without an aspect data (or with one whose marker is not set),
        /// the service must not synthesise
        /// <see cref="MissingPropertyReason.CloudRequestFailed"/>. The
        /// reason is per-request state, not derived from engine metadata.
        /// </summary>
        [TestMethod]
        public void MissingPropertyServiceCloud_GetReason_NoMarker_DoesNotReportCloudRequestFailed()
        {
            var aspectData = new TestAspectData();

            Mock<ICloudAspectEngine> engine = new Mock<ICloudAspectEngine>();
            engine.SetupGet(e => e.ElementDataKey).Returns("testElement");
            engine.SetupGet(e => e.HasLoadedProperties).Returns(true);
            ConfigureCloudProperty(engine.As<IAspectEngine>(), "testProperty");
            var engines = new List<IAspectEngine> { engine.Object };

            var result = _service.GetMissingPropertyReason(
                "testProperty",
                engines,
                aspectData);

            Assert.AreNotEqual(
                MissingPropertyReason.CloudRequestFailed,
                result.Reason);
        }

        private static void ConfigureCloudProperty(Mock<IAspectEngine> engine, string propertyName)
        {
            var property = new AspectPropertyMetaData(
                engine.Object,
                propertyName,
                typeof(string),
                "",
                new List<string>(),
                true);
            var propertyList = new List<IAspectPropertyMetaData>() { property };
            engine.Setup(e => e.Properties).Returns(propertyList);
        }

        private class TestAspectData : AspectDataBase
        {
            public TestAspectData()
                : base(
                    new TestLogger<AspectDataBase>(),
                    new Mock<IPipeline>().Object,
                    new Mock<IAspectEngine>().Object)
            {
            }
        }
    }
}
