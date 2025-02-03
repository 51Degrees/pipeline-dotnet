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

using FiftyOne.Pipeline.CloudRequestEngine.Data;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Data.Types;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FiftyOne.Pipeline.CloudRequestEngine.Tests
{
    /// <summary>
    /// Tests for the <see cref="CloudAspectEngineBase{T}"/> class.
    /// </summary>
    [TestClass]
    public class CloudAspectEngineBaseTests
    {

        /// <summary>
        /// Expected properties error, duplicated here as error Messages has 
        /// internal access modifier.
        /// </summary>
        private static readonly string PropertiesError = 
            "Failed to load aspect properties for element '{0}'. This is " +
            "because your resource key does not include access to any " +
            "properties under '{0}'. For more details on resource keys, " +
            "see our explainer: https://51degrees.com/documentation/_info__resource_keys.html";

        #region Test Classes

        public class ItemData : AspectDataBase
        {
            public ItemData(ILogger<AspectDataBase> logger,
                IPipeline pipeline,
                IAspectEngine engine) :
                base(logger, pipeline, engine)
            {
            }

            public bool IsMobile { get; set; }
            public string HardwareVendor { get; set; }
            public IReadOnlyList<string> HardwareVariants { get; set; }
        }

        public class TestData : AspectDataBase
        {
            public TestData(ILogger<AspectDataBase> logger,
                IPipeline pipeline,
                IAspectEngine engine) :
                base(logger, pipeline, engine)
            {
            }
            public JavaScript JavaScript { get; set; }
            public bool IsMobile { get; set; }
            public string HardwareVendor { get; set; }
            public IReadOnlyList<string> HardwareVariants { get; set; }

            public IReadOnlyList<ItemData> Devices { get; set; }
        }

        public class TestInstance : CloudAspectEngineBase<TestData>
        {
            private Dictionary<string, Type> _complexPropertyTypes;

            public TestInstance()
                : this(new Dictionary<string, Type>())
            {

            }

            public TestInstance(Dictionary <string, Type> propertyTypes) :
                base(new Logger<TestInstance>(new LoggerFactory()), CreateData)
            {
                _complexPropertyTypes = propertyTypes;
            }

            private static TestData CreateData(IPipeline pipeline,
                FlowElementBase<TestData, IAspectPropertyMetaData> element)
            {
                return new TestData(null,
                    pipeline, element as IAspectEngine);
            }

            public override string ElementDataKey => "test";

            public override IEvidenceKeyFilter EvidenceKeyFilter =>
                new EvidenceKeyFilterWhitelist(new List<string>());

            protected override void ProcessCloudEngine(IFlowData data, TestData aspectData, string json)
            {
                if (string.IsNullOrEmpty(json)) 
                {
                    Assert.Fail("'json' value should not be null or empty if " +
                        "this method is called");
                }
            }

            protected override Type GetPropertyType(PropertyMetaData propertyMetaData, Type parentObjectType)
            {
                if (_complexPropertyTypes.ContainsKey(propertyMetaData.Name))
                {
                    return _complexPropertyTypes[propertyMetaData.Name];
                }
                return base.GetPropertyType(propertyMetaData, parentObjectType);
            }
        }

        public class TestRequestEngine : AspectEngineBase<CloudRequestData, IAspectPropertyMetaData>, ICloudRequestEngine
        {
            public TestRequestEngine() : base(new Logger<TestRequestEngine>(new LoggerFactory()), CreateData)
            {
            }

            private static CloudRequestData CreateData(IPipeline pipeline,
                FlowElementBase<CloudRequestData, IAspectPropertyMetaData> element)
            {
                return new CloudRequestData(null,
                    pipeline, element as IAspectEngine);
            }

            public virtual IReadOnlyDictionary<string, ProductMetaData> PublicProperties { get; set; }

            public override string DataSourceTier => "";

            public override string ElementDataKey => "cloud";

            public override IEvidenceKeyFilter EvidenceKeyFilter => new EvidenceKeyFilterWhitelist(new List<string>());

            public override IList<IAspectPropertyMetaData> Properties => new List<IAspectPropertyMetaData>();

            protected override void ProcessEngine(IFlowData data, CloudRequestData aspectData)
            {
            }

            protected override void UnmanagedResourcesCleanup()
            {
            }
        }

        #endregion

        #region Test

        private TestInstance _engine;
        private TestRequestEngine _requestEngine;
        private IPipeline _pipeline;

        private Dictionary<string, ProductMetaData> _propertiesReturnedByRequestEngine;
        private Dictionary<string, Type> _complexPropertyTypes;

        [TestInitialize]
        public void Init()
        {
            _propertiesReturnedByRequestEngine = new Dictionary<string, ProductMetaData>();
            _complexPropertyTypes = new Dictionary<string, Type>();
        }

        /// <summary>
        /// Test the LoadProperties method of the CloudAspectEngine which
        /// retrieves property meta-data from the cloud request engine.
        /// </summary>
        [TestMethod]
        public void LoadProperties()
        {
            List<PropertyMetaData> properties = new List<PropertyMetaData>();
            properties.Add(new PropertyMetaData() { Name = "ismobile", Type = "Boolean" });
            properties.Add(new PropertyMetaData() { Name = "hardwarevendor", Type = "String" });
            properties.Add(new PropertyMetaData() { Name = "hardwarevariants", Type = "Array" });
            ProductMetaData devicePropertyData = new ProductMetaData();
            devicePropertyData.Properties = properties;
            _propertiesReturnedByRequestEngine.Add("test", devicePropertyData);
            _complexPropertyTypes.Add("hardwarevariants", typeof(IReadOnlyList<string>));

            CreatePipeline();

            Assert.AreEqual(3, _engine.Properties.Count);
            Assert.IsTrue(_engine.Properties.Any(p => p.Name == "ismobile"));
            Assert.IsTrue(_engine.Properties.Any(p => p.Name == "hardwarevendor"));
            Assert.IsTrue(_engine.Properties.Any(p => p.Name == "hardwarevariants"));
        }

        /// <summary>
        /// Test that an exception is thrown by the Properties auto property if 
        /// the cloud request engine returns no properties.
        /// </summary>
        [TestMethod]
        public void LoadProperties_NoProperties()
        {
            try
            {
                CreatePipeline();
                Assert.Fail("PipelineException should be thrown");
            }
            catch (PipelineException ex) {
                Assert.AreEqual(
                    string.Format(CultureInfo.InvariantCulture,
                        PropertiesError,
                        "test"),
                    ex.Message);
            }
        }

        /// <summary>
        /// Test that <see cref="CloudRequestException"/>
        /// thrown by <see cref="ICloudRequestEngine.PublicProperties"/>
        /// gets wrapped in <see cref="PropertiesNotYetLoadedException"/>.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(PropertiesNotYetLoadedException))]
        public void LoadProperties_CloudRequestExceptionOnPublicProperties()
        {
            // prepare flow elements
            var mockTestInstance = new Mock<TestInstance>() { CallBase = true };
            var mockRequestEngine = new Mock<TestRequestEngine>() { CallBase = true };

            // count thrown `CloudRequestException` instances
            int[] exceptionsGenerated = new int[] { 0 };
            mockRequestEngine.Setup(x => x.PublicProperties).Throws(() => {
                exceptionsGenerated[0] += 1;
                return new CloudRequestException("Fake Properties extraction failure");
            });

            IPipeline pipeline;
            try
            {
                // Verify that pipeline itself is built just fine
                pipeline = new PipelineBuilder(new LoggerFactory())
                    .AddFlowElement(mockRequestEngine.Object)
                    .AddFlowElement(mockTestInstance.Object)
                    .Build();
            } 
            catch(Exception ex)
            {
                // Else fail the test
                Assert.Fail($"Exception was thrown by the {nameof(PipelineBuilder)}: {ex}");
            }

            // Verify `CloudRequestException` was generated
            Assert.AreEqual(1, exceptionsGenerated[0]);

            // Verify `CloudAspectEngineBase` wraps `CloudRequestException` into `PropertiesNotYetLoadedException`
            _ = mockTestInstance.Object.Properties;
        }

        /// <summary>
        /// Test that an exception is thrown by the Properties auto property if
        /// the cloud engine only returns properties for other engines.
        /// </summary>
        [TestMethod]
        public void LoadProperties_WrongProperties()
        {
            List<PropertyMetaData> properties = new List<PropertyMetaData>();
            properties.Add(new PropertyMetaData() { Name = "ismobile", Type = "Boolean" });
            properties.Add(new PropertyMetaData() { Name = "hardwarevendor", Type = "String" });
            properties.Add(new PropertyMetaData() { Name = "hardwarevariants", Type = "Array" });
            ProductMetaData devicePropertyData = new ProductMetaData();
            devicePropertyData.Properties = properties;
            _propertiesReturnedByRequestEngine.Add("test2", devicePropertyData);

            try
            {
                CreatePipeline();
                Assert.Fail("PipelineException should be thrown");
            }
            catch (PipelineException ex)
            {
                Assert.AreEqual(
                    string.Format(CultureInfo.InvariantCulture,
                        PropertiesError,
                        "test"),
                    ex.Message);
            }
        }

        /// <summary>
        /// Test loading sub-property meta data where a cloud aspect engine 
        /// has nested properties. E.g. the cloud property keyed engine.
        /// </summary>
        [TestMethod]
        public void LoadProperties_SubProperties()
        {
            List<PropertyMetaData> subproperties = new List<PropertyMetaData>();
            subproperties.Add(new PropertyMetaData() { Name = "ismobile", Type = "Boolean" });
            subproperties.Add(new PropertyMetaData() { Name = "hardwarevendor", Type = "String" });
            subproperties.Add(new PropertyMetaData() { Name = "hardwarevariants", Type = "Array" });

            List<PropertyMetaData> properties = new List<PropertyMetaData>();
            properties.Add(new PropertyMetaData() { Name = "devices", Type = "Array", ItemProperties = subproperties });

            ProductMetaData devicePropertyData = new ProductMetaData();
            devicePropertyData.Properties = properties;
            _propertiesReturnedByRequestEngine.Add("test", devicePropertyData);
            _complexPropertyTypes.Add("hardwarevariants", typeof(IReadOnlyList<string>));
            _complexPropertyTypes.Add("devices", typeof(IReadOnlyList<object>));

            CreatePipeline();

            Assert.AreEqual(1, _engine.Properties.Count);
            Assert.AreEqual("devices", _engine.Properties[0].Name);
            Assert.AreEqual(3, _engine.Properties[0].ItemProperties.Count);
            Assert.IsTrue(_engine.Properties[0].ItemProperties.Any(p => p.Name == "ismobile"));
            Assert.IsTrue(_engine.Properties[0].ItemProperties.Any(p => p.Name == "hardwarevendor"));
            Assert.IsTrue(_engine.Properties[0].ItemProperties.Any(p => p.Name == "hardwarevariants"));
        }

        /// <summary>
        /// Test loading delayed evidence property meta data.
        /// </summary>
        [TestMethod]
        public void LoadProperties_DelayedProperties()
        {
            List<string> evidenceProperties = new List<string>();
            evidenceProperties.Add("javascript");

            List<PropertyMetaData> properties = new List<PropertyMetaData>();
            properties.Add(new PropertyMetaData() { Name = "javascript", Type = "JavaScript", DelayExecution = true });
            properties.Add(new PropertyMetaData() { Name = "hardwarevendor", Type = "String", EvidenceProperties = evidenceProperties });
            properties.Add(new PropertyMetaData() { Name = "hardwarevariants", Type = "Array", EvidenceProperties = evidenceProperties });
            ProductMetaData devicePropertyData = new ProductMetaData();
            devicePropertyData.Properties = properties;
            _propertiesReturnedByRequestEngine.Add("test", devicePropertyData);
            _complexPropertyTypes.Add("hardwarevariants", typeof(IReadOnlyList<string>));

            CreatePipeline();

            Assert.AreEqual(3, _engine.Properties.Count);
            Assert.IsTrue(_engine.Properties.Any(p => p.Name == "javascript"));
            Assert.IsTrue(_engine.Properties.Any(p => p.Name == "hardwarevendor"));
            Assert.IsTrue(_engine.Properties.Any(p => p.Name == "hardwarevariants"));
            Assert.IsTrue(_engine.Properties.Single(p => p.Name == "javascript").DelayExecution);
            Assert.AreEqual(1, _engine.Properties.Single(p => p.Name == "hardwarevendor").EvidenceProperties.Count);
            Assert.AreEqual(1, _engine.Properties.Single(p => p.Name == "hardwarevariants").EvidenceProperties.Count);
        }

        /// <summary>
        /// Test that the LoadProperties method of the CloudAspectEngine will
        /// handle a situation where a property from cloud does not exist as a c# property
        /// and has a simple type.
        /// </summary>
        [TestMethod]
        public void LoadProperties_NoMatch()
        {
            List<PropertyMetaData> properties = new List<PropertyMetaData>();
            properties.Add(new PropertyMetaData() { Name = "cloudStringProperty", Type = "String" });
            properties.Add(new PropertyMetaData() { Name = "cloudBoolProperty", Type = "Boolean" });
            properties.Add(new PropertyMetaData() { Name = "cloudJavaScriptProperty", Type = "JavaScript" });
            ProductMetaData devicePropertyData = new ProductMetaData();
            devicePropertyData.Properties = properties;
            _propertiesReturnedByRequestEngine.Add("test", devicePropertyData);

            CreatePipeline();

            Assert.AreEqual(3, _engine.Properties.Count);
            var query1 = _engine.Properties.Where(p => p.Name == "cloudStringProperty");
            Assert.IsTrue(query1.Any());
            Assert.IsTrue(query1.First().Type.Equals(typeof(string)));
            var query2 = _engine.Properties.Where(p => p.Name == "cloudBoolProperty");
            Assert.IsTrue(query2.Any());
            Assert.IsTrue(query2.First().Type.Equals(typeof(bool)));
            var query3 = _engine.Properties.Where(p => p.Name == "cloudJavaScriptProperty");
            Assert.IsTrue(query3.Any());
            Assert.IsTrue(query3.First().Type.Equals(typeof(JavaScript)));
        }

        /// <summary>
        /// Test that the LoadProperties method of the CloudAspectEngine will
        /// throw an exception when a property from cloud does not exist as a c# property
        /// and has an array type.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(PipelineException))]
        public void LoadProperties_NoMatchArray()
        {
            List<PropertyMetaData> properties = new List<PropertyMetaData>();
            properties.Add(new PropertyMetaData() { Name = "cloudArrayProperty", Type = "Array" });
            ProductMetaData devicePropertyData = new ProductMetaData();
            devicePropertyData.Properties = properties;
            _propertiesReturnedByRequestEngine.Add("test", devicePropertyData);

            CreatePipeline();
        }

        /// <summary>
        /// Test that the LoadProperties method of the CloudAspectEngine will
        /// throw an exception when a property from cloud does not exist as a c# property
        /// and has sub-properties.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(PipelineException))]
        public void LoadProperties_NoMatchSubProperties()
        {
            List<PropertyMetaData> subproperties = new List<PropertyMetaData>();
            subproperties.Add(new PropertyMetaData() { Name = "ismobile", Type = "Boolean" });
            subproperties.Add(new PropertyMetaData() { Name = "hardwarevendor", Type = "String" });
            subproperties.Add(new PropertyMetaData() { Name = "hardwarevariants", Type = "Array" });

            List<PropertyMetaData> properties = new List<PropertyMetaData>();
            properties.Add(new PropertyMetaData() { Name = "cloudComplexProperty", Type = "Array", ItemProperties = subproperties });

            ProductMetaData devicePropertyData = new ProductMetaData();
            devicePropertyData.Properties = properties;
            _propertiesReturnedByRequestEngine.Add("test", devicePropertyData);

            CreatePipeline();
        }

        /// <summary>
        /// Test that when processing the cloud aspect engine, the 
        /// ProcessCloudMethod is called when the JSON response is 
        /// populated.
        /// </summary>
        [TestMethod]
        public void Process_CloudResponse()
        {
            // Setup properties.
            List<PropertyMetaData> properties = new List<PropertyMetaData>();
            properties.Add(new PropertyMetaData() { Name = "ismobile", Type = "Boolean" });
            ProductMetaData devicePropertyData = new ProductMetaData();
            devicePropertyData.Properties = properties;
            _propertiesReturnedByRequestEngine.Add("test", devicePropertyData);

            // Create mock TestInstance so we can see if the ProcessCloudEngine
            // method is called.
            var mockTestInstance = new Mock<TestInstance>() { CallBase = true };
            mockTestInstance.Protected().Setup(
                "ProcessCloudEngine",
                ItExpr.IsAny<IFlowData>(),
                ItExpr.IsAny<TestData>(),
                ItExpr.IsAny<string>()).Verifiable();

            // Create mock TestRequestEngine so we can mock the ProcessEngine 
            // method and get it to set it's aspect data.
            var mockRequestEngine = new Mock<TestRequestEngine>() { CallBase = true };
            mockRequestEngine.Object.PublicProperties = _propertiesReturnedByRequestEngine;
            mockRequestEngine.Protected()
                .Setup(
                    "ProcessEngine",
                    ItExpr.IsAny<IFlowData>(),
                    ItExpr.IsAny<CloudRequestData>())
                .Callback((IFlowData data, CloudRequestData aspectData) => {
                    aspectData.JsonResponse = "{ \"response\": true }";
                });

            // Construct the pipeline.
            var pipeline = new PipelineBuilder(new LoggerFactory())
                .AddFlowElement(mockRequestEngine.Object)
                .AddFlowElement(mockTestInstance.Object)
                .Build();

            // Process the pipeline.
            using (var flowData = pipeline.CreateFlowData())
            {
                flowData
                    .AddEvidence("query.user-agent", "iPhone")
                    .Process();
            }

            // Verify the ProcessCloudEngine method was called
            mockTestInstance.Protected().Verify(
                "ProcessCloudEngine", 
                Times.AtLeastOnce(),
                ItExpr.IsAny<IFlowData>(),
                ItExpr.IsAny<TestData>(),
                ItExpr.IsAny<string>());
            mockTestInstance.Verify();
        }

        /// <summary>
        /// Test that when processing the cloud aspect engine, the 
        /// ProcessCloudMethod is not called when the JSON response is not
        /// populated.
        /// </summary>
        [TestMethod]
        public void Process_NoCloudResponse() 
        {
            // Setup properties.
            List<PropertyMetaData> properties = new List<PropertyMetaData>();
            properties.Add(new PropertyMetaData() { Name = "ismobile", Type = "Boolean" });
            ProductMetaData devicePropertyData = new ProductMetaData();
            devicePropertyData.Properties = properties;
            _propertiesReturnedByRequestEngine.Add("test", devicePropertyData);

            // Create mock TestInstance so we can see if the ProcessCloudEngine
            // method is called.
            var mockTestInstance = new Mock<TestInstance>() { CallBase = true };
            mockTestInstance.Protected().Setup(
                "ProcessCloudEngine",
                ItExpr.IsAny<IFlowData>(),
                ItExpr.IsAny<TestData>(),
                ItExpr.IsAny<string>()).Verifiable();

            // Create mock TestRequestEngine
            var mockRequestEngine = new Mock<TestRequestEngine>() { CallBase = true };
            mockRequestEngine.Object.PublicProperties = _propertiesReturnedByRequestEngine;

            // Construct the pipeline.
            var pipeline = new PipelineBuilder(new LoggerFactory())
                .AddFlowElement(mockRequestEngine.Object)
                .AddFlowElement(mockTestInstance.Object)
                .Build();

            // Process the pipeline.
            using (var flowData = pipeline.CreateFlowData())
            {
                flowData
                    .AddEvidence("query.user-agent", "iPhone")
                    .Process();
            }

            // Verify the ProcessCloudEngine method was called
            mockTestInstance.Protected().Verify(
                "ProcessCloudEngine",
                Times.Never(),
                ItExpr.IsAny<IFlowData>(),
                ItExpr.IsAny<TestData>(), 
                ItExpr.IsAny<string>());
        }

        /// <summary>
        /// Test that the expected exception is thrown when the 
        /// CloudRequestEngine and a CloudAspectEngine have been added to the 
        /// Pipeline in the wrong order.
        /// </summary>
        [TestMethod]
        public void CloudEngines_WrongOrder()
        {
            // Setup properties.
            List<PropertyMetaData> properties = new List<PropertyMetaData>();
            properties.Add(new PropertyMetaData() { Name = "ismobile", Type = "Boolean" });
            ProductMetaData devicePropertyData = new ProductMetaData();
            devicePropertyData.Properties = properties;
            _propertiesReturnedByRequestEngine.Add("test", devicePropertyData);

            // Create mock TestInstance so we can see if the ProcessCloudEngine
            // method is called.
            var mockTestInstance = new Mock<TestInstance>() { CallBase = true };
            mockTestInstance.Protected().Setup(
                "ProcessCloudEngine",
                ItExpr.IsAny<IFlowData>(),
                ItExpr.IsAny<TestData>(),
                ItExpr.IsAny<string>()).Verifiable();

            // Create mock TestRequestEngine so we can mock the ProcessEngine 
            // method and get it to set it's aspect data.
            var mockRequestEngine = new Mock<TestRequestEngine>() { CallBase = true };
            mockRequestEngine.Object.PublicProperties = _propertiesReturnedByRequestEngine;
            mockRequestEngine.Protected()
                .Setup(
                    "ProcessEngine",
                    ItExpr.IsAny<IFlowData>(),
                    ItExpr.IsAny<CloudRequestData>())
                .Callback((IFlowData data, CloudRequestData aspectData) => {
                    aspectData.JsonResponse = "{ \"response\": true }";
                    aspectData.ProcessStarted = true;
                });

            // Construct the pipeline.
            var pipeline = new PipelineBuilder(new LoggerFactory())
                .AddFlowElement(mockTestInstance.Object)
                .AddFlowElement(mockRequestEngine.Object)
                .Build();

            // Process the pipeline.
            try
            {
                using (var flowData = pipeline.CreateFlowData())
                {
                    flowData
                        .AddEvidence("query.user-agent", "iPhone")
                        .Process();
                }
            } 
            catch (AggregateException ex)
            {
                var expectedExceptions = ex.InnerExceptions
                    .Where(e => e.GetType() == typeof(PipelineConfigurationException));

                Assert.IsTrue(
                    expectedExceptions.Count() > 0,
                    "Aggregate exception did not contain expected PipelineConfigurationException.");
                Assert.AreEqual(
                    "The 'TestInstanceProxy' requires a 'CloudRequestEngine' " +
                    "before it in the Pipeline. This engine will be unable to " +
                    "produce results until this is corrected.",
                    expectedExceptions.FirstOrDefault()?.Message, 
                    "PipelineConfigurationException message was incorrect.");
            }
        }

        /// <summary>
        /// Test that the expected exception is thrown when the 
        /// CloudRequestEngine has not been added to the Pipeline but a 
        /// CloudAspectEngine has
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(PipelineConfigurationException))]
        public void CloudEngines_NoRequestEngine()
        {
            var engine = new TestInstance();
            var pipeline = new PipelineBuilder(new LoggerFactory())
                .AddFlowElement(engine)
                .Build();
        }

        #endregion

        #region Private methods

        private void CreatePipeline()
        {
            _engine = new TestInstance(_complexPropertyTypes);
            _requestEngine = new TestRequestEngine();
            _requestEngine.PublicProperties = _propertiesReturnedByRequestEngine;
            _pipeline = new PipelineBuilder(new LoggerFactory())
                .AddFlowElement(_requestEngine)
                .AddFlowElement(_engine)
                .Build();
        }
        #endregion
    }
}
