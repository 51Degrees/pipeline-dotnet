/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2020 51 Degrees Mobile Experts Limited, 5 Charlotte Close,
 * Caversham, Reading, Berkshire, United Kingdom RG4 7BY.
 *
 * This Original Work is licensed under the European Union Public Licence (EUPL) 
 * v.1.2 and is subject to its terms as set out below.
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FiftyOne.Pipeline.Engines.Tests.Services
{
    [TestClass]
    public class MissingPropertyServiceTests
    {
        private IMissingPropertyService _service;

        [TestInitialize]
        public void Initialise()
        {
            _service = MissingPropertyService.Instance;
        }

        /// <summary>
        /// Check that the missing property service works as expected when
        /// the property is available in a different data file.
        /// </summary>
        [TestMethod]
        public void MissingPropertyService_GetReason_Upgrade()
        {
            // Arrange
            Mock<IAspectEngine> engine = new Mock<IAspectEngine>();
            engine.Setup(e => e.DataSourceTier).Returns("lite");
            ConfigureProperty(engine);

            // Act
            var result = _service.GetMissingPropertyReason("testProperty", engine.Object);

            // Assert
            Assert.AreEqual(MissingPropertyReason.DataFileUpgradeRequired, result.Reason);
        }

        /// <summary>
        /// Check that the missing property service works as expected when
        /// the property has been excluded from the result set
        /// </summary>
        [TestMethod]
        public void MissingPropertyService_GetReason_Excluded()
        {
            // Arrange
            Mock<IAspectEngine> engine = new Mock<IAspectEngine>();
            engine.Setup(e => e.DataSourceTier).Returns("premium");
            ConfigureProperty(engine, false);

            // Act
            var result = _service.GetMissingPropertyReason("testProperty", engine.Object);

            // Assert
            Assert.AreEqual(MissingPropertyReason.PropertyExcludedFromEngineConfiguration, result.Reason);
        }

        /// <summary>
        /// Check that the missing property service works as expected when
        /// the property is not present in the engine
        /// </summary>
        [TestMethod]
        public void MissingPropertyService_GetReason_NotInEngine()
        {
            // Arrange
            Mock<IAspectEngine> engine = new Mock<IAspectEngine>();
            engine.Setup(e => e.DataSourceTier).Returns("premium");
            ConfigureProperty(engine, false);

            // Act
            var result = _service.GetMissingPropertyReason("otherProperty", engine.Object);

            // Assert
            Assert.AreEqual(MissingPropertyReason.Unknown, result.Reason);
        }

        /// <summary>
        /// Check that a "product not in resource" reason is returned when a cloud
        /// engine does not contain the product.
        ///</summary>
        [TestMethod]
        public void MissingPropertyService_GetReason_ProductNotInResource() 
        {
            // Arrange
            Mock<ICloudAspectEngine> engine = new Mock<ICloudAspectEngine>();
            engine.SetupGet(e => e.ElementDataKey).Returns("testElement");
            engine.SetupGet(e => e.Properties).Returns(new List<IAspectPropertyMetaData>());

            // Act
            var result = _service.GetMissingPropertyReason(
                "otherProperty",
                engine.Object);

            // Assert
            Assert.AreEqual(
                MissingPropertyReason.ProductNotAccessibleWithResourceKey,
                result.Reason);
            Assert.AreEqual(
            string.Format(
                Messages.MissingPropertyMessagePrefix,
                "otherProperty",
                "testElement") +
            string.Format(
                Messages.MissingPropertyMessageProductNotInCloudResource,
                "testElement"),
            result.Description);
        }

        /// <summary>
        /// Check that a "property not in resource" reason is returned when a cloud
        /// engine does contain the product, but not the property.
        /// </summary>
        [TestMethod]
        public void MissingPropertyService_GetReason_PropertyNotInResource() 
        {
            // Arrange
            Mock<ICloudAspectEngine> engine = new Mock<ICloudAspectEngine>();
            engine.SetupGet(e => e.ElementDataKey).Returns("testElement");
            ConfigureProperty(engine.As<IAspectEngine>());

            // Act
            var result = _service.GetMissingPropertyReason(
                "otherProperty",
                engine.Object);

            // Assert
            Assert.AreEqual(
                MissingPropertyReason.PropertyNotAccessibleWithResourceKey,
                result.Reason);
            Assert.AreEqual(
            string.Format(
                Messages.MissingPropertyMessagePrefix,
                "otherProperty",
                "testElement") +
            string.Format(
                Messages.MissingPropertyMessagePropertyNotInCloudResource,
                "testElement",
                "testProperty"),
            result.Description);
        }


        /// <summary>
        /// Check that the missing property service works as expected when
        /// the property is not missing for any of the other reasons.
        /// </summary>
        [TestMethod]
        public void MissingPropertyService_GetReason_Unknown()
        {
            // Arrange
            Mock<IAspectEngine> engine = new Mock<IAspectEngine>();
            engine.Setup(e => e.DataSourceTier).Returns("premium");
            ConfigureProperty(engine);

            // Act
            var result = _service.GetMissingPropertyReason("testProperty", engine.Object);

            // Assert
            Assert.AreEqual(MissingPropertyReason.Unknown, result.Reason);
        }

        private void ConfigureProperty(Mock<IAspectEngine> engine)
        {
            ConfigureProperty(engine, true);
        }

        /// <summary>
        /// Helper method that configures the specified mock engine to
        /// return a specific property called 'testProperty'.
        /// </summary>
        /// <param name="engine"></param>
        /// <param name="propertyAvailable"></param>
        private void ConfigureProperty(Mock<IAspectEngine> engine,
            bool propertyAvailable = true)
        {
            List<string> dataFiles = new List<string>() { "premium", "enterprise" };
            var property = new AspectPropertyMetaData(
                engine.Object,
                "testProperty",
                typeof(string),
                "",
                dataFiles,
                propertyAvailable);
            List<IAspectPropertyMetaData> propertyList =
                new List<IAspectPropertyMetaData>()
                {
                    property
                };
            engine.Setup(e => e.Properties).Returns(propertyList);
        }
    }
}
