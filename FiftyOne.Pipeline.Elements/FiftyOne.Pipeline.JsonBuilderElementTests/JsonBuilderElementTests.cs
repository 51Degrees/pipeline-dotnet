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

using FiftyOne.Pipeline.JsonBuilder;
using FiftyOne.Pipeline.JsonBuilder.FlowElement;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Data.Types;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Core.TypedMap;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using FiftyOne.Pipeline.JsonBuilder.Data;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using FiftyOne.Pipeline.Engines.TestHelpers;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Text;

namespace FiftyOne.Pipeline.JsonBuilderElementTests
{
    [TestClass]
    public class JsonBuilderElementTests
    {
        public enum JsPropertyType
        {
            JavaScript,
            IAspectPropertyValue,
            AspectPropertyValue
        }

        private IJsonBuilderElement _jsonBuilderElement;
        private Mock<IElementData> _elementDataMock;
        private ILoggerFactory _loggerFactory;
        private Mock<IPipeline> _pipeline;

        private Mock<IAspectEngine> _testEngine;
        private Dictionary<string, IReadOnlyDictionary<string, IElementPropertyMetaData>> _propertyMetaData;

        [TestInitialize]
        public void Init()
        {
            _testEngine = new Mock<IAspectEngine>();
            _testEngine.Setup(e => e.ElementDataKey).Returns("test");

            _loggerFactory = new LoggerFactory();

            _jsonBuilderElement = new JsonBuilderElementBuilder(_loggerFactory)
                .Build();

            _elementDataMock = new Mock<IElementData>();
            _elementDataMock.Setup(ed => ed.AsDictionary()).
                Returns(new Dictionary<string, object>() {
                    { "property", "thisIsAValue" },
                    { "jsproperty", "var = 'some js code';" }
                });

            _pipeline = new Mock<IPipeline>();
            _pipeline.Setup(p => p.GetHashCode()).Returns(1);
            _propertyMetaData = new Dictionary<string, IReadOnlyDictionary<string, IElementPropertyMetaData>>();
            _pipeline.Setup(p => p.ElementAvailableProperties).Returns(_propertyMetaData);
        }

        /// <summary>
        /// Check that the JSON produced by the JsonBuilder is valid.
        /// </summary>
        [TestMethod]
        public void JsonBuilder_ValidJson()
        {
            var json = TestIteration(1);

            Assert.IsTrue(IsExpectedJson(json));
        }

        /// <summary>
        /// Check that the JSON produced by the JsonBuilder is valid.
        /// </summary>
        [TestMethod]
        public void JsonBuilder_NonEmptyFallback()
        {
            var json = TestIteration(1, null, null, (e, fd) => ((JsonBuilderElement)e).GetFallbackResponse(fd).Json);

            Assert.IsFalse(string.IsNullOrWhiteSpace(json));
        }

        /// <summary>
        /// Check that the JSON element removes JavaScript properties from the 
        /// response after max number of iterations has been reached.
        /// </summary>
        [DataTestMethod]
        [DataRow(JsPropertyType.JavaScript)]
        [DataRow(JsPropertyType.AspectPropertyValue)]
        [DataRow(JsPropertyType.IAspectPropertyValue)]
        public void JsonBuilder_JsProperty(JsPropertyType propertyType)
        {
            _elementDataMock.Setup(ed => ed.AsDictionary()).
                Returns(new Dictionary<string, object>() {
                    { "jsproperty", "var = 'some js code';" }
                });

            var testElementMetaData = new Dictionary<string, IElementPropertyMetaData>();
            var p1 = new ElementPropertyMetaData(_testEngine.Object, "jsproperty", GetTypeFromEnum(propertyType), true, "");
            testElementMetaData.Add("jsproperty", p1);
            _propertyMetaData.Add("test", testElementMetaData);

            var json = TestIteration(1);
            Assert.IsTrue(ContainsJavaScriptProperties(json),
                "The 'javascriptProperties' element is missing from the JSON. " +
                "Complete JSON: " + Environment.NewLine + json);
        }

        /// <summary>
        /// Check that the JSON element removes JavaScript properties from the 
        /// response after max number of iterations has been reached.
        /// </summary>
        [TestMethod]
        public void JsonBuilder_MaxIterations()
        {
            _elementDataMock.Setup(ed => ed.AsDictionary()).
                Returns(new Dictionary<string, object>() {
                    { "jsproperty", "var = 'some js code';" }
                });

            var testElementMetaData = new Dictionary<string, IElementPropertyMetaData>();
            var p1 = new ElementPropertyMetaData(_testEngine.Object, "jsproperty", typeof(AspectPropertyValue<JavaScript>), true, "");
            testElementMetaData.Add("jsproperty", p1);
            _propertyMetaData.Add("test", testElementMetaData);

            for (var i = 0; true; i++)
            {
                var json = TestIteration(i);
                var result = ContainsJavaScriptProperties(json);

                if (i >= Constants.MAX_JAVASCRIPT_ITERATIONS)
                {
                    Assert.IsFalse(result, $"Failed on iteration {i}");
                    break;
                }
                else
                {
                    Assert.IsTrue(result, $"Failed on iteration {i}");
                }
            }
        }

        /// <summary>
        /// Check that entries will not appear in the output 
        /// for elements in the exclusion list.
        /// </summary>
        [TestMethod]
        public void JsonBuilder_ElementExclusionlist()
        {
            var json = TestIteration(1,
               new Dictionary<string, object>() {
                    { "test", _elementDataMock.Object },
                    { "cloud-response", _elementDataMock.Object },
                    { JsonBuilderElement.DEFAULT_ELEMENT_DATA_KEY, _elementDataMock.Object }
               });

            Assert.IsTrue(IsExpectedJson(json));
            JObject obj = JObject.Parse(json);
            Assert.AreEqual(1, obj.Children().Count(),
                $"There should only be the 'test' key at the top level as " +
                $"the other elements should have been ignored. Complete JSON: " +
                Environment.NewLine + json);
        }

        /// <summary>
        /// Data class used in the nested properties test.
        /// </summary>
        private class NestedData : ElementDataBase
        {
            public NestedData(ILogger<ElementDataBase> logger, IPipeline pipeline)
                : base(logger, pipeline)
            { }

            public string Value1
            {
                get { return this["Value1"] as string; }
                set { this["Value1"] = value; }
            }
            public int Value2
            {
                get { return (int)this["Value2"]; }
                set { this["Value2"] = value; }
            }
        }

        /// <summary>
        /// Check that nested properties are serialised as expected
        /// </summary>
        [TestMethod]
        public void JsonBuilder_NestedProperties()
        {
            _elementDataMock.Setup(ed => ed.AsDictionary()).
                Returns(new Dictionary<string, object>() {
                    { "property", new List<NestedData>() {
                        new NestedData(_loggerFactory.CreateLogger<NestedData>(), _pipeline.Object) { Value1 = "abc", Value2 = 123 },
                        new NestedData(_loggerFactory.CreateLogger<NestedData>(), _pipeline.Object) { Value1 = "xyz", Value2 = 789 }
                    } },
                });

            // Configure the property meta-data as needed for
            // this test.
            var testElementMetaData = new Dictionary<string, IElementPropertyMetaData>();
            var nestedMetaData = new List<IElementPropertyMetaData>() {
                new ElementPropertyMetaData(_testEngine.Object, "value1", typeof(string), true),
                new ElementPropertyMetaData(_testEngine.Object, "value2", typeof(int), true),
            };
            var p1 = new ElementPropertyMetaData(_testEngine.Object, "property", typeof(List<NestedData>), true, "", nestedMetaData);
            testElementMetaData.Add("property", p1);
            _propertyMetaData.Add("test", testElementMetaData);

            var json = TestIteration(1);

            JObject obj = JObject.Parse(json);
            Assert.IsTrue(obj.Children().Count() == 1 &&
                (obj.Children().Single() as JProperty)?.Name == "test",
                $"There should only be the 'test' key at the top level. " +
                $"Complete JSON: " + Environment.NewLine + json);
            var element = obj.Children().Single();
            var x = element.Children().Single().Children().Single();
            Assert.IsTrue(element.Children().Children().Single().Count() == 1 &&
                (element.Children().Single().Children().Single() as JProperty)?.Name == "property",
                $"There should only be one property, named 'property'," +
                $" under the 'test' element. " +
                $"Complete JSON: " + Environment.NewLine + json);
            element = element.Children().Single().Children().Single();
            Assert.AreEqual(2, element.Children().Single().Children().Count(),
                $"There should be two entries under the 'test.property' property. " +
                $"Complete JSON: " + Environment.NewLine + json);
            var v1 = element.Children().Single().Children().ElementAt(0);
            var v2 = element.Children().Single().Children().ElementAt(1);
            Assert.IsTrue(v1.Children().Count() == 2 &&
                v1.Children().Any(t => (t as JProperty)?.Name == "value1") &&
                v1.Children().Any(t => (t as JProperty)?.Name == "value2"),
                $"There should be two properties, 'value1' and 'value2', " +
                $"under the 'test.property[0]' entry. " +
                $"Complete JSON: " + Environment.NewLine + json);
            Assert.IsTrue(v2.Children().Count() == 2 &&
                v2.Children().Any(t => (t as JProperty)?.Name == "value1") &&
                v2.Children().Any(t => (t as JProperty)?.Name == "value2"),
                $"There should be two properties, 'value1' and 'value2', " +
                $"under the 'test.property[0]' entry. " +
                $"Complete JSON: " + Environment.NewLine + json);
        }

        public static IEnumerable<object[]> GetDelayedExecutionTestParameters
        {
            get
            {
                foreach (var entry in Enum.GetValues(typeof(JsPropertyType)))
                {
                    yield return new object[] { true, true, entry };
                    yield return new object[] { false, true, entry };
                    yield return new object[] { true, false, entry };
                    yield return new object[] { false, false, entry };
                }
            }
        }

        /// <summary>
        /// Check that delayed execution and evidence properties values
        /// are populated correctly.
        /// </summary>
        [DataTestMethod]
        [DynamicData(nameof(GetDelayedExecutionTestParameters))]
        public void JsonBuilder_DelayedExecution(
            bool delayExecution, 
            bool propertyValueNull,
            JsPropertyType jsPropertyType)
        {
            // If the flag is set then initialise the 'property' value to null.
            if (propertyValueNull)
            {
                _elementDataMock.Setup(ed => ed.AsDictionary()).
                    Returns(new Dictionary<string, object>() {
                    { "property", null },
                    { "jsproperty", "var = 'some js code';" }
                    });
            }
                       
            // Configure the property meta-data as needed for
            // this test.
            var testElementMetaData = new Dictionary<string, IElementPropertyMetaData>();
            var p1 = new ElementPropertyMetaData(_testEngine.Object, "property", typeof(string), true, "", null, false, new List<string>() { "jsproperty" });
            testElementMetaData.Add("property", p1);
            var p2 = new ElementPropertyMetaData(_testEngine.Object, "jsproperty", GetTypeFromEnum(jsPropertyType), true, "", null, delayExecution);
            testElementMetaData.Add("jsproperty", p2);
            _propertyMetaData.Add("test", testElementMetaData);

            // Run the test
            var json = TestIteration(1);

            // Verify that the *delayexecution and *evidenceproperties
            // values are populated as expected.
            JObject obj = JObject.Parse(json);
            var results = obj["test"].Children().ToList();

            bool hasEvidenceProperties = results.Any(t => t.ToString().Contains("propertyevidenceproperties"));
            bool hasDelayedExecution = results.Any(t => t.ToString().Contains("jspropertydelayexecution"));

            Assert.AreEqual(delayExecution ? true : false, hasEvidenceProperties,
                $"The JSON data does {(delayExecution ? "not" : "")} " +
                $"contain a 'propertyevidenceproperties' property. " +
                $"Complete JSON: {json}");
            Assert.AreEqual(delayExecution ? true : false, hasDelayedExecution,
                $"The JSON data does {(delayExecution ? "not" : "")} " +
                $"contain a 'jspropertydelayexecution' property. " +
                $"Complete JSON: {json}");
            if (delayExecution)
            {
                var evidenceProperties = results.Single(t => (t as JProperty)?.Name.Equals("propertyevidenceproperties") ?? false) as JProperty;
                Assert.IsInstanceOfType(evidenceProperties.Value, typeof(JArray));
                Assert.AreEqual("test.jsproperty", (evidenceProperties.Value as JArray).Single().ToString());
            }
        }
        
        /// <summary>
        /// Check that delayed execution and evidence properties values
        /// are populated correctly when a property has multiple 
        /// evidence properties
        /// </summary>
        [DataTestMethod]
        public void JsonBuilder_MultipleEvidenceProperties()
        {
            // Configure the property meta-data as needed for
            // this test.
            // property is populated by 2 JavaScript properties: 
            // jsproperty and jsproperty2.
            // jsproperty has delayed execution true and
            // jsproperty2 does not.
            var testElementMetaData = new Dictionary<string, IElementPropertyMetaData>();
            var p1 = new ElementPropertyMetaData(_testEngine.Object, "property", typeof(string), true, "", null, false, new List<string>() { "jsproperty", "jsproperty2" });
            testElementMetaData.Add("property", p1);
            var p2 = new ElementPropertyMetaData(_testEngine.Object, "jsproperty", typeof(JavaScript), true, "", null, true);
            testElementMetaData.Add("jsproperty", p2);
            var p3 = new ElementPropertyMetaData(_testEngine.Object, "jsproperty2", typeof(JavaScript), true, "", null, false);
            testElementMetaData.Add("jsproperty2", p2);
            _propertyMetaData.Add("test", testElementMetaData);

            _elementDataMock.Setup(ed => ed.AsDictionary()).
                Returns(new Dictionary<string, object>() {
                    { "property", "thisIsAValue" },
                    { "jsproperty", "var = 'some js code';" },
                    { "jsproperty2", "var = 'some js code';" }
                });

            // Run the test
            var json = TestIteration(1);

            // Verify that the *delayexecution and *evidenceproperties
            // values are populated as expected.
            JObject obj = JObject.Parse(json);
            var results = obj["test"].Children().ToList();

            Assert.IsTrue(results.Any(t => t.ToString().Contains("propertyevidenceproperties")),
                $"Expected the JSON to contain a 'propertyevidenceproperties' " +
                $"item." +
                $"Complete JSON: {json}");
            var pep = results.Where(t => t.ToString().Contains("propertyevidenceproperties")).Single();
            Assert.AreEqual("test.jsproperty", pep.Values().Single().ToString(),
                $"Expected the JSON to contain a 'propertyevidenceproperties' " +
                $"item where the value is an array with one item, 'test.jsproperty'." +
                $"Complete JSON: {json}");
            Assert.IsTrue(results.Any(t => t.ToString().Contains("jspropertydelayexecution")),
                $"Expected the JSON to contain a 'jspropertydelayexecution' " +
                $"item." +
                $"Complete JSON: {json}");
            Assert.IsFalse(results.Any(t => t.ToString().Contains("jsproperty2delayexecution")),
                $"Expected the JSON not to contain a 'jsproperty2delayexecution' " +
                $"item." +
                $"Complete JSON: {json}");
        }

        /// <summary>
        /// Check that the JSON produced by the JsonBuilder is correct 
        /// when lazy loading is enabled.
        /// </summary>
        [TestMethod]
        public void JsonBuilder_LazyLoading()
        {
            var engine = new EmptyEngineBuilder(_loggerFactory)
                .SetLazyLoadingTimeout(1000)
                .SetProcessCost(TimeSpan.FromMilliseconds(500).Ticks)
                .Build();
            var jsonBuilder = new JsonBuilderElementBuilder(_loggerFactory)
                .Build();
            var sequenceElement = new SequenceElementBuilder(_loggerFactory)
                .Build();
            var pipeline = new PipelineBuilder(_loggerFactory)
                .AddFlowElement(sequenceElement)
                .AddFlowElement(engine)
                .AddFlowElement(jsonBuilder)
                .Build();

            using (var flowData = pipeline.CreateFlowData())
            {
                Trace.WriteLine("Process starting");
                flowData.Process();
                Trace.WriteLine("Process complete");

                var jsonResult = flowData.Get<IJsonBuilderElementData>();
                Assert.IsNotNull(jsonResult);
                Assert.IsNotNull(jsonResult.Json);

                var jsonData = JsonConvert.DeserializeObject<JsonData>(jsonResult.Json);
                Assert.AreEqual(1, jsonData.EmptyAspect.Valueone);
                Assert.AreEqual(2, jsonData.EmptyAspect.Valuetwo);
                Trace.WriteLine("Data validated");
            }
        }

        /// <summary>
        /// Inner class used to test serialization of values in 
        /// isolation
        /// </summary>
        private class TestJsonBuilderElement : JsonBuilderElement
        {
            private bool _throwExceptionOnSerialize;
            private static ILoggerFactory _loggerFactory;

            public TestJsonBuilderElement(
                ILoggerFactory loggerFactory,
                bool throwExceptionOnSerialize = false) 
                : base(loggerFactory.CreateLogger<TestJsonBuilderElement>(), 
                      new List<JsonConverter>(), CreateData)
            {
                _throwExceptionOnSerialize = throwExceptionOnSerialize;
                _loggerFactory = loggerFactory;
            }


            public string Serialize(Dictionary<string, object> data)
            {
                return BuildJson(data);
            }

            // Configure the BuildJson method to throw an exception.
            protected override string BuildJson(Dictionary<string, object> allProperties)
            {
                if (_throwExceptionOnSerialize)
                {
                    throw new JsonWriterException("Error");
                }
                else
                {
                    return base.BuildJson(allProperties);
                }
            }

            private static IJsonBuilderElementData CreateData(
                IPipeline pipeline,
                FlowElementBase<IJsonBuilderElementData, IElementPropertyMetaData> jsonBuilderElement)
            {
                return new JsonBuilderElementData(
                    _loggerFactory.CreateLogger<JsonBuilderElementData>(), 
                    pipeline);
            }
        }

        /// <summary>
        /// Used by the serialization tests below
        /// </summary>
        public enum TypeToBeTested
        {
            String,
            JavaScript,
            List,
            APV_String,
            APV_JavaScript,
            APV_List
        }
        public static IEnumerable<object[]> SerializationTestGenerator()
        {
            List<string> propertyValues = new List<string>() {
                null,
                "abc",
                "{ curly braces }",
                "[ square braces ]",
                "@ at signs @",
                "% percent sign %",
                "$ dollar sign $",
                "\" double quotes \"",
                "' single quotes '",
                "& ampersand &",
                "\\ backslash \\",
                "   tabs  ",
                @"
carriage return and new line  
",
                "{ \"json\": [ \"text\", \"abc\" ] }"
            };

            foreach (var type in Enum.GetValues(typeof(TypeToBeTested)))
            {
                foreach (var value in propertyValues)
                {
                    yield return new object[] { value, (TypeToBeTested)type };
                }
            }
        }

        /// <summary>
        /// Verify that various types and values are serialized correctly.
        /// </summary>
        /// <param name="valueOfProperty">
        /// The string representation of the value to serialize
        /// </param>
        /// <param name="typeToBeTested">
        /// The type of the value to be serialized
        /// </param>
        [DataTestMethod]
        [DynamicData(nameof(SerializationTestGenerator), DynamicDataSourceType.Method)]
        public void JsonBuilder_Serialization_Text(string valueOfProperty, TypeToBeTested typeToBeTested)
        {
            var jsonBuilder = new TestJsonBuilderElement(_loggerFactory);

            // valueInDict holds the object that will be added
            // to the dictionary to be serialized.
            object valueInDict = null;

            // ExpectedValue holds the value that we expect
            // the property to have in the generated json
            // including any surrounding tokens such as quotes,
            // square brackets, etc.
            string expectedValue = (valueOfProperty ?? "null").Replace(@"\", @"\\");
            expectedValue = expectedValue.Replace(@"""", @"\""");
            expectedValue = expectedValue.Replace(@"    ", @"\  ");
            expectedValue = expectedValue.Replace(@"
", @"\r\n");

            // Replacing as there is no carriage return in Linux format.
            expectedValue = expectedValue.Replace(@"\r", @"");
            expectedValue = valueOfProperty == null ? expectedValue : $"\"{expectedValue}\"";

            // Create the object to be serialized
            switch (typeToBeTested)
            {
                case TypeToBeTested.String:
                    valueInDict = valueOfProperty;
                    break;
                case TypeToBeTested.JavaScript:
                    valueInDict = new JavaScript(valueOfProperty);
                    break;
                case TypeToBeTested.List:
                    valueInDict = new List<string>() { valueOfProperty };
                    expectedValue = $@"[
                      {expectedValue}
                    ]";
                    break;
                case TypeToBeTested.APV_String:
                    valueInDict = new AspectPropertyValue<string>(valueOfProperty);
                    break;
                case TypeToBeTested.APV_JavaScript:
                    valueInDict = new AspectPropertyValue<JavaScript>(new JavaScript(valueOfProperty));
                    break;
                case TypeToBeTested.APV_List:
                    valueInDict = new AspectPropertyValue<IReadOnlyList<string>>(new List<string>() { valueOfProperty });
                    expectedValue = $@"[
                      {expectedValue}
                    ]";
                    break;
                default:
                    break;
            }

            // Create the dictionary to be serialized
            var data = new Dictionary<string, object>()
            {
                {
                    "element",
                    new Dictionary<string, object>()
                    {
                        { "property", valueInDict },
                    }
                }
            };

            var result = jsonBuilder.Serialize(data);
            JsonEqual($@"{{
              ""element"": {{
                ""property"": {expectedValue}
              }}
            }}", result);
        }

        /// <summary>
        /// Test that various values are serialized properly.
        /// For tests on string-based values, see the 
        /// <seealso cref="Serialization_Text"/> tests.
        /// </summary>
        [TestMethod]
        public void JsonBuilder_Serialization_PropertyValues()
        {
            var jsonBuilder = new TestJsonBuilderElement(_loggerFactory);

            var data = new Dictionary<string, object>()
            {
                {
                    "element",
                    new Dictionary<string, object>()
                    {
                        { "property1", 10 },
                        { "property2", new AspectPropertyValue<int>(10) },
                        { "property3", 10.1 },
                        { "property4", new AspectPropertyValue<double>(10.1) },
                        { "property5", true },
                        { "property6", new AspectPropertyValue<bool>(true) },
                        { "property7", new List<string>() { "itema", "itemb" } },
                        { "property8", new AspectPropertyValue<IReadOnlyList<string>>(new List<string>() { "itema", "itemb" }) },
                        { "property9", new List<string>() },
                        { "property10", new AspectPropertyValue<IReadOnlyList<string>>(new List<string>()) },
                    }
                }
            };

            var result = jsonBuilder.Serialize(data);
            JsonEqual(@"{
                ""element"": {
                ""property1"": 10,
                ""property2"": 10,
                ""property3"": 10.1,
                ""property4"": 10.1,
                ""property5"": true,
                ""property6"": true,
                ""property7"": [
                    ""itema"",
                    ""itemb""
                ],
                ""property8"": [
                    ""itema"",
                    ""itemb""
                ],
                ""property9"": [],
                ""property10"": []
                }
                }", result);
        }

        [TestMethod]
        /// <summary>
        /// Check that error handling works as expected when a serialization
        /// error occurs.
        /// </summary>
        public void JsonBuilder_VerifyErrorHandling()
        {
            _jsonBuilderElement = new TestJsonBuilderElement(_loggerFactory, true);

            // Create a moderately complex set of values so that we can
            // validate the complex value handling.
            _elementDataMock.Setup(ed => ed.AsDictionary()).
                Returns(new Dictionary<string, object>() {
                    { "property", new List<NestedData>() {
                        new NestedData(_loggerFactory.CreateLogger<NestedData>(), _pipeline.Object) { Value1 = "abc", Value2 = 123 },
                        new NestedData(_loggerFactory.CreateLogger<NestedData>(), _pipeline.Object) { Value1 = "xyz", Value2 = 789 }
                    } },
                });

            // Configure the property meta-data as needed for
            // this test.
            var testElementMetaData = new Dictionary<string, IElementPropertyMetaData>();
            var nestedMetaData = new List<IElementPropertyMetaData>() {
                new ElementPropertyMetaData(_testEngine.Object, "value1", typeof(string), true),
                new ElementPropertyMetaData(_testEngine.Object, "value2", typeof(int), true),
            };
            var p1 = new ElementPropertyMetaData(_testEngine.Object, "property", typeof(List<NestedData>), true, "", nestedMetaData);
            testElementMetaData.Add("property", p1);
            _propertyMetaData.Add("test", testElementMetaData);

            // Configure the flow data to record error that are added.
            Mock<IFlowData> flowData = new Mock<IFlowData>();
            Exception lastException = null;
            flowData.Setup(d => d.AddError(It.IsAny<Exception>(), _jsonBuilderElement)).Callback(
                (Exception ex, IFlowElement element) =>
                {
                    lastException = ex;
                });
            var json = TestIteration(1, null, flowData);

            // Check that the error message was logged and contains 
            // some of the expected content.
            // Checking for an exact match would be too brittle
            Assert.IsNotNull(lastException, "Expected an error to be logged but it was not");
            Assert.IsTrue(lastException.Message.Contains("abc"), 
                $"Logged message did not contain expected text 'abc'. {lastException.Message}");
            Assert.IsTrue(lastException.Message.Contains("xyz"),
                $"Logged message did not contain expected text 'xyz'. {lastException.Message}");
            Assert.IsTrue(lastException.Message.Contains("123"), 
                $"Logged message did not contain expected text '123'. {lastException.Message}");
            Assert.IsTrue(lastException.Message.Contains("789"), 
                $"Logged message did not contain expected text '789'. {lastException.Message}");
        }

        public class JsonData
        {
            [JsonProperty("empty-aspect")]
            public EmptyAspect EmptyAspect { get; set; }

            [JsonProperty(JsonBuilderElement.DEFAULT_ELEMENT_DATA_KEY)]
            public JsonBuilder JsonBuilder { get; set; }
        }

        public class EmptyAspect
        {
            [JsonProperty("valueone")]
            public long Valueone { get; set; }

            [JsonProperty("valuetwo")]
            public long Valuetwo { get; set; }
        }

        public class JsonBuilder
        {
        }

        private string TestIteration(int iteration,
            Dictionary<string, object> data = null,
            Mock<IFlowData> flowData = null,
            Func<IJsonBuilderElement, IFlowData, string> processFunc = null)
        {
            if(data == null)
            {
                data = new Dictionary<string, object>() { { "test", _elementDataMock.Object } };
            }

            if (flowData == null) { flowData = new Mock<IFlowData>(); }
            var _missingPropertyService = new Mock<IMissingPropertyService>();

            flowData.Setup(d => d.ElementDataAsDictionary()).Returns(data);
            string session = "somesessionid";
            flowData.Setup(d => d.TryGetEvidence("query.session-id", out session)).Returns(true);
            flowData.Setup(d => d.TryGetEvidence("query.sequence", out iteration)).Returns(true);
            flowData.Setup(d => d.GetWhere(It.IsAny<Func<IElementPropertyMetaData, bool>>())).Returns(
                (Func<IElementPropertyMetaData, bool> filter) => {
                    return _propertyMetaData
                        .SelectMany(e => e.Value)
                        .Where(p => filter(p.Value))
                        .Select(p => new KeyValuePair<string, object>(p.Value.Element.ElementDataKey + "." + p.Key, p.Value));
                });

            IJsonBuilderElementData result = null;
            flowData.Setup(d => d.GetOrAdd(
                It.IsAny<TypedKey<IJsonBuilderElementData>>(),
                It.IsAny<Func<IPipeline, IJsonBuilderElementData>>()))
                .Returns<TypedKey<IJsonBuilderElementData>, Func<IPipeline, IJsonBuilderElementData>>(
                (k, f) =>
                {
                    result = f(flowData.Object.Pipeline);
                    return result;
                });
            flowData.Setup(d => d.Pipeline).Returns(_pipeline.Object);

            if (processFunc != null)
            {
                return processFunc(_jsonBuilderElement, flowData.Object);
            }

            _jsonBuilderElement.Process(flowData.Object);

            var json = result["json"].ToString();

            return json;
        }

        private bool IsExpectedJson(string json)
        {
            JObject obj = JObject.Parse(json);
            var results = obj["test"].Children().ToList();

            foreach (var result in results)
            {
                var res = result.ToString();
                if (res.Contains("property") && res.Contains("thisIsAValue"))
                {
                    return true;
                }
            }
            return false;
        }

        private bool ContainsJavaScriptProperties(string json)
        {
            JObject obj = JObject.Parse(json);
            var results = obj.Children().ToList();

            foreach (var result in results)
            {
                var res = result.ToString();
                if (res.Contains("javascriptProperties"))
                {
                    return true;
                }
            }
            return false;
        }

        private Type GetTypeFromEnum(JsPropertyType type)
        {
            Type result = typeof(JavaScript);
            switch (type)
            {
                case JsPropertyType.JavaScript:
                    result = typeof(JavaScript);
                    break;
                case JsPropertyType.IAspectPropertyValue:
                    result = typeof(IAspectPropertyValue<JavaScript>);
                    break;
                case JsPropertyType.AspectPropertyValue:
                    result = typeof(AspectPropertyValue<JavaScript>);
                    break;
                default:
                    break;
            }
            return result;
        }

        /// <summary>
        /// Asserts two JSON strings are equal having first removed all 
        /// carriage returns and leading and trailing white space for each
        /// line.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        private static void JsonEqual(string a, string b)
        {
            Assert.AreEqual(JsonTrim(a), JsonTrim(b));
        }

        private static string JsonTrim(string value)
        {
            var sb = new StringBuilder();
            var lines = value.Split(
                new char[] { '\r', '\n' }, 
                StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                sb.Append(line.Trim());
            }
            return sb.ToString();
        }
    }
}