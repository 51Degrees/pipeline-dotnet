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

using FiftyOne.Common.TestHelpers;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Core.Tests.HelperClasses;
using FiftyOne.Pipeline.Core.TypedMap;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace FiftyOne.Pipeline.Core.Tests.Data
{
    /// <summary>
    /// Tests of the FlowData class.
    /// </summary>
    [TestClass]
    public class FlowDataTests
    {

        private Mock<IPipelineInternal> _pipeline;
        private Mock<ILogger<FlowData>> _logger;
        private FlowData _flowData;
        private bool _flowDataDisposed;

        [TestInitialize]
        public void Init()
        {
            _pipeline = new Mock<IPipelineInternal>();
            _logger = new Mock<ILogger<FlowData>>();
            var evidenceLogger = new Mock<ILogger<Evidence>>();
            _flowData = new FlowData(_logger.Object, _pipeline.Object,
                new Evidence(evidenceLogger.Object));
            _flowDataDisposed = false;
        }

        [TestCleanup]
        public void Cleanup()
        {
            DisposeFlowData();
        }

         /// <summary>
         /// This method should be called to properly dispose of
         /// the global _flowData object.
         /// </summary>
        private void DisposeFlowData()
        {
    	    if (_flowDataDisposed == false) {
    		    _flowData.Dispose();
                _flowDataDisposed = true;
    	    }
        }

        /// <summary>
        /// Check that the Process method calls process on the pipeline
        /// passing itself as a parameter.
        /// </summary>
        [TestMethod]
        public void FlowData_Process()
        {
            _flowData.Process();
            _pipeline.Verify(p => p.Process(_flowData));
        }

        [TestMethod]
        public void FlowData_Process_WithToken()
        {
            var token = new CancellationToken(canceled: false);
            _flowData.Process(token);
            _pipeline.Verify(p => p.Process(_flowData));
        }

        [TestMethod]
        public void FlowData_Process_WithCancelledToken()
        {
            var token = new CancellationToken(canceled: true);
            try
            {
                _flowData.Process(token);
                Assert.Fail($"{nameof(_flowData.Process)} didn't throw.");
            }
            catch (OperationCanceledException)
            {
                // nop
            }
            _pipeline.Verify(p => p.Process(_flowData), Times.Never);
        }

        /// <summary>
        /// Check that the Process method will not allow multiple calls
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(PipelineException))]
        public void FlowData_ProcessAlreadyDone()
        {
            _flowData.Process();
            _flowData.Process();
        }

        /// <summary>
        /// Check that adding evidence works as expected
        /// </summary>
        [TestMethod]
        public void FlowData_AddEvidence()
        {
            string key = "key";
            _flowData.AddEvidence(key, "value");
            var result = _flowData.GetEvidence()[key];

            Assert.AreEqual("value", result);
        }

        /// <summary>
        /// Check that adding an evidence dictionary works as expected
        /// </summary>
        [TestMethod]
        public void FlowData_AddEvidenceDictionary()
        {
            string key1 = "key1";
            string key2 = "key2";
            Dictionary<string, object> evidence = new Dictionary<string, object>();

            evidence.Add(key1, "value1");
            evidence.Add(key2, "value2");
            _flowData.AddEvidence(evidence);
            var result1 = _flowData.GetEvidence()[key1];
            var result2 = _flowData.GetEvidence()[key2];

            Assert.AreEqual("value1", result1);
            Assert.AreEqual("value2", result2);
        }

        /// <summary>
        /// Add data to the FlowData using a string key
        /// Get it using a string key and check that it matches.
        /// </summary>
        [TestMethod]
        public void FlowData_AddDataString_GetDataString()
        {
            _flowData.Process();
            string key = "key";
            var data = _flowData.GetOrAdd(
                key,
                (p) => new TestElementData(p));

            var result = _flowData.Get(key);

            Assert.AreSame(data, result);
        }

        /// <summary>
        /// Add data to the FlowData using a string key
        /// Get it using a TypedKey and check that it matches.
        /// </summary>
        [TestMethod]
        public void FlowData_AddDataString_GetDataTypedKey()
        {
            _flowData.Process();
            string key = "key";
            var typedKey = new TypedKey<TestElementData>(key);
            var data = _flowData.GetOrAdd(
                key,
                (p) => new TestElementData(p));

            var result = _flowData.Get(typedKey);

            Assert.AreSame(data, result);
        }

        /// <summary>
        /// Add data to the FlowData using a string key
        /// Get it using a FlowElement and check that it matches.
        /// </summary>
        [TestMethod]
        public void FlowData_AddDataString_GetDataElement()
        {
            _flowData.Process();

            var element = new TestElement();
            var data = _flowData.GetOrAdd(
                element.ElementDataKey,
                (p) => new TestElementData(p));
            var result = _flowData.GetFromElement(element);

            Assert.AreSame(data, result);
        }

        /// <summary>
        /// Add data to the FlowData using a TypedKey
        /// Get it using a string key and check that it matches.
        /// </summary>
        [TestMethod]
        public void FlowData_AddDataTypedKey_GetDataString()
        {
            _flowData.Process();
            string key = "key";
            var typedKey = new TypedKey<TestElementData>(key);
            TestElementData data = _flowData.GetOrAdd(
                typedKey,
                (p) => new TestElementData(p));

            var result = _flowData.Get(key);

            Assert.AreSame(data, result);
        }

        /// <summary>
        /// Add data to the FlowData using a TypedKey
        /// Get it using a TypedKey and check that it matches.
        /// </summary>
        [TestMethod]
        public void FlowData_AddDataTypedKey_GetDataTypedKey()
        {
            _flowData.Process();
            string key = "key";
            var typedKey = new TypedKey<TestElementData>(key);
            TestElementData data = _flowData.GetOrAdd(
                typedKey,
                (p) => new TestElementData(p));

            var result = _flowData.Get(typedKey);

            Assert.AreSame(data, result);
        }

        /// <summary>
        /// Add data to the FlowData using a TypedKey
        /// Get it using a FlowElement and check that it matches.
        /// </summary>
        [TestMethod]
        public void FlowData_AddDataTypedKey_GetDataElement()
        {
            _flowData.Process();
            var element = new TestElement();
            var typedKey = new TypedKey<TestElementData>(element.ElementDataKey);
            TestElementData data = _flowData.GetOrAdd(
                typedKey,
                (p) => new TestElementData(p));

            var result = _flowData.GetFromElement(element);

            Assert.AreSame(data, result);
        }

        /// <summary>
        /// Get element data as a dictionary
        /// </summary>
        [TestMethod]
        public void FlowData_GetDataAsDictionary()
        {
            string key = "key";
            TestElementData data = _flowData.GetOrAdd(
                key,
                (p) => new TestElementData(p));

            var result = _flowData.ElementDataAsDictionary();

            Assert.AreEqual(1, result.Count);
            Assert.AreSame(data, result[key]);
        }

        /// <summary>
        /// Get element data as an enumerable.
        /// </summary>
        [TestMethod]
        public void FlowData_GetDataAsEnumerable()
        {
            _flowData.Process();
            string key = "key";
            TestElementData data = _flowData.GetOrAdd(
                key,
                (p) => new TestElementData(p));

            var result = _flowData.ElementDataAsEnumerable();

            var count = 0;
            foreach (var elementData in result)
            {
                count++;
                Assert.AreSame(data, elementData);
            }
            Assert.AreEqual(1, count);
        }

        /// <summary>
        /// Get element data as a dictionary, update it and retrieve
        /// through the FlowData instance.
        /// </summary>
        [TestMethod]
        public void FlowData_UpdateDataAsDictionary()
        {
            _flowData.Process();
            string key = "key";
            TestElementData data = new TestElementData(_pipeline.Object);
            var dataAsDict = _flowData.ElementDataAsDictionary();

            dataAsDict.Add(key, data);
            var result = _flowData.Get(key);

            Assert.AreSame(data, result);
        }

        /// <summary>
        /// Check that the expected exception is thrown if the key is null
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void FlowData_GetWithNullStringKey()
        {
            _flowData.Process();
            var result = _flowData.Get(null);
        }

        /// <summary>
        /// Check that the expected exception is thrown if the key is null
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void FlowData_GetWithNullTypedKey()
        {
            _flowData.Process();
            var result = _flowData.Get<TestElementData>(null);
        }

        /// <summary>
        /// Check that the expected exception is thrown if the key is null
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void FlowData_GetWithNullElement()
        {
            _flowData.Process();
            var result = _flowData.GetFromElement<TestElementData, IElementPropertyMetaData>(null);
        }

        /// <summary>
        /// Check that the expected exception is thrown if the FlowData 
        /// has not yet been processed.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(PipelineException))]
        public void FlowData_GetBeforeProcess_String()
        {
            var result = _flowData.Get("key");
        }

        /// <summary>
        /// Check that the expected exception is thrown if the FlowData 
        /// has not yet been processed.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(PipelineException))]
        public void FlowData_GetBeforeProcess_TypedKey()
        {
            var result = _flowData.Get(new TypedKey<TestElementData>("key"));
        }

        /// <summary>
        /// Check that the expected exception is thrown if the FlowData 
        /// has not yet been processed.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(PipelineException))]
        public void FlowData_GetBeforeProcess_FlowElement()
        {
            var result = _flowData.GetFromElement(new TestElement());
        }

        /// <summary>
        /// Check that the expected exception is thrown  if the data key 
        /// is not present
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(KeyNotFoundException))]
        public void FlowData_GetNotPresent_String()
        {
            _flowData.Process();
            var result = _flowData.Get("key");
        }

        /// <summary>
        /// Check that the expected exception is thrown  if the data key 
        /// is not present
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(KeyNotFoundException))]
        public void FlowData_GetNotPresent_TypedKey()
        {
            _flowData.Process();
            var result = _flowData.Get(new TypedKey<TestElementData>("key"));
        }

        /// <summary>
        /// Check that the expected exception is thrown  if the data key 
        /// is not present
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(KeyNotFoundException))]
        public void FlowData_GetNotPresent_Element()
        {
            _flowData.Process();
            var result = _flowData.GetFromElement(new TestElement());
        }

        /// <summary>
        /// Get the value for an evidence key though flow data.
        /// </summary>
        [TestMethod]
        public void FlowData_TryGetEvidence()
        {
            string key = "key";
            _flowData.AddEvidence(key, "value");
            string value;
            var result = _flowData.TryGetEvidence(key, out value);

            Assert.IsTrue(result);
            Assert.AreEqual("value", value);
        }

        /// <summary>
        /// Try to get the value for an invalid evidence key though flow data.
        /// </summary>
        [TestMethod]
        public void FlowData_TryGetEvidence_InvalidKey()
        {
            string key = "key";
            _flowData.AddEvidence(key, "value");
            string value;
            var result = _flowData.TryGetEvidence("key2", out value);

            Assert.IsFalse(result);
        }

        /// <summary>
        /// Check that the method can handle an invalid cast.
        /// </summary>
        [TestMethod]
        public void FlowData_TryGetEvidence_InvalidCast()
        {
            string key = "key";
            _flowData.AddEvidence(key, "value");
            int value;
            var result = _flowData.TryGetEvidence("key", out value);

            Assert.IsFalse(result);
        }

        #region GetAs tests

        /// <summary>
        /// Test that getting a property value directly using 'GetAs' 
        /// works as expected for an integer property
        /// </summary>
        [TestMethod]
        public void FlowData_GetAs_Int()
        {
            ConfigureMultiElementValues();
            var flowData = StaticFactories.CreateFlowData(_pipeline.Object);
            flowData.Process();

            Assert.AreEqual(INT_PROPERTY_VALUE, flowData.GetAs<int>(INT_PROPERTY));
            Assert.AreEqual(INT_PROPERTY_VALUE, flowData.GetAsInt(INT_PROPERTY));
        }

        /// <summary>
        /// Test that getting a property value directly using 'GetAs' 
        /// works as expected for a string property
        /// </summary>
        [TestMethod]
        public void FlowData_GetAs_String()
        {
            ConfigureMultiElementValues();
            var flowData = StaticFactories.CreateFlowData(_pipeline.Object);
            flowData.Process();

            Assert.AreEqual(STRING_PROPERTY_VALUE, flowData.GetAs<string>(STRING_PROPERTY));
            Assert.AreEqual(STRING_PROPERTY_VALUE, flowData.GetAsString(STRING_PROPERTY));
        }

        /// <summary>
        /// Test that getting a property value directly using 'GetAs' 
        /// works as expected for a string property where the value is null
        /// </summary>
        [TestMethod]
        public void FlowData_GetAs_StringNull()
        {
            ConfigureMultiElementValues();
            var flowData = StaticFactories.CreateFlowData(_pipeline.Object);
            flowData.Process();

            Assert.IsNull(flowData.GetAs<string>(NULL_STRING_PROPERTY));
            Assert.IsNull(flowData.GetAsString(NULL_STRING_PROPERTY));
        }
        
        /// <summary>
        /// Test that getting a property value directly using 'GetAs' 
        /// works as expected for a reference type.
        /// </summary>
        [TestMethod]
        public void FlowData_GetAs_List()
        {
            ConfigureMultiElementValues();
            var flowData = StaticFactories.CreateFlowData(_pipeline.Object);
            flowData.Process();

            Assert.IsTrue(LIST_PROPERTY_VALUE.SequenceEqual(
                flowData.GetAs<List<string>>(LIST_PROPERTY)));
        }

        /// <summary>
        /// Test that getting a property value directly using 'GetAs' 
        /// throws the expected error if flow data has not yet been processed
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(PipelineException))]
        public void FlowData_GetAs_NotProcessed()
        {
            ConfigureMultiElementValues();
            var flowData = StaticFactories.CreateFlowData(_pipeline.Object);

            var result = flowData.GetAs<string>(STRING_PROPERTY);
        }

        /// <summary>
        /// Test that getting a property value directly using 'GetAs' 
        /// throws the expected error if the requested property does not
        /// exist
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(PipelineDataException))]
        public void FlowData_GetAs_NoProperty()
        {
            ConfigureMultiElementValues();
            var flowData = StaticFactories.CreateFlowData(_pipeline.Object);
            flowData.Process();

            var result = flowData.GetAs<string>("not a property");
        }

        /// <summary>
        /// Test that getting a property value directly using 'GetAs' 
        /// throws the expected error if there are two elements with 
        /// the requested property.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(PipelineDataException))]
        public void FlowData_GetAs_MultipleProperties()
        {
            ConfigureMultiElementValues();
            var flowData = StaticFactories.CreateFlowData(_pipeline.Object);
            flowData.Process();

            var result = flowData.GetAs<string>(DUPLICATE_PROPERTY);
        }

        /// <summary>
        /// Test that getting a property value directly using 'GetAs' 
        /// throws the expected error if the property is being cast to
        /// the wrong type.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidCastException))]
        public void FlowData_GetAs_WrongType()
        {
            ConfigureMultiElementValues();
            var flowData = StaticFactories.CreateFlowData(_pipeline.Object);
            flowData.Process();

            var result = flowData.GetAs<int>(STRING_PROPERTY);
        }
        

        private const string INT_PROPERTY = "intvalue";
        private const string STRING_PROPERTY = "stringvalue";
        private const string NULL_STRING_PROPERTY = "nullstringvalue";
        private const string LIST_PROPERTY = "listvalue";
        private const string DUPLICATE_PROPERTY = "duplicate";

        private const int INT_PROPERTY_VALUE = 5;
        private const string STRING_PROPERTY_VALUE = "test";
        private List<string> LIST_PROPERTY_VALUE = 
            new List<string>() { "test", "abc" };

        private void ConfigureMultiElementValues()
        {
            Mock<IFlowElement> element1 = new Mock<IFlowElement>();
            element1.Setup(e => e.ElementDataKey).Returns("element1");
            List<IElementPropertyMetaData> metaData1 = new List<IElementPropertyMetaData>()
            {
                new ElementPropertyMetaData(element1.Object, INT_PROPERTY, INT_PROPERTY_VALUE.GetType(), true),
                new ElementPropertyMetaData(element1.Object, NULL_STRING_PROPERTY, typeof(string), true),
                new ElementPropertyMetaData(element1.Object, LIST_PROPERTY, LIST_PROPERTY_VALUE.GetType(), true),
                new ElementPropertyMetaData(element1.Object, DUPLICATE_PROPERTY, typeof(string), true),
            };
            element1.Setup(e => e.Properties).Returns(metaData1);

            Mock<IFlowElement> element2 = new Mock<IFlowElement>();
            element2.Setup(e => e.ElementDataKey).Returns("element2");
            List<IElementPropertyMetaData> metaData2 = new List<IElementPropertyMetaData>()
            {
                new ElementPropertyMetaData(element2.Object, STRING_PROPERTY, STRING_PROPERTY_VALUE.GetType(), true),
                new ElementPropertyMetaData(element2.Object, DUPLICATE_PROPERTY, typeof(string), true),
            };
            element2.Setup(e => e.Properties).Returns(metaData2);
            
            DictionaryElementData elementData1 = new DictionaryElementData(
                new TestLogger<DictionaryElementData>(), _pipeline.Object);
            elementData1[INT_PROPERTY] = INT_PROPERTY_VALUE;
            elementData1[NULL_STRING_PROPERTY] = null;
            elementData1[LIST_PROPERTY] = LIST_PROPERTY_VALUE;
            DictionaryElementData elementData2 = new DictionaryElementData(
                new TestLogger<DictionaryElementData>(), _pipeline.Object);
            elementData2[STRING_PROPERTY] = STRING_PROPERTY_VALUE;

            List<IFlowElement> elements = new List<IFlowElement>()
            {
                element1.Object,
                element2.Object,
            };
            _pipeline.Setup(p => p.FlowElements).Returns(elements);
            _pipeline.Setup(p => p.Process(It.IsAny<IFlowData>()))
                .Callback((IFlowData data) =>
                {
                    data.GetOrAdd("element1", (p) => elementData1);
                    data.GetOrAdd("element2", (p) => elementData2);
                });
            _pipeline.Setup(p => p.GetMetaDataForProperty(It.IsAny<string>())).Returns((string propertyName) =>
            {
                var matches = metaData1.Union(metaData2)
                    .Where(d => d.Name == propertyName);
                if(matches.Count() == 0 || matches.Count() > 1)
                {
                    throw new PipelineDataException("");
                }
                return matches.Single();
            });

        }
        #endregion

        #region GetWhere Tests

        /// <summary>
        /// Set up the pipeline and flow data with an element which contains
        /// properties which can used to test the GetWhere method.
        /// </summary>
        private void ConfigureGetWhere(bool includePropertyWithException = false)
        {
            // Mock the element
            var element1 = new Mock<IFlowElement>();
            element1.SetupGet(e => e.ElementDataKey).Returns("element1");
            // Set up the properties
            var propertyMetaData =
                new List<IElementPropertyMetaData>()
                {
                    new ElementPropertyMetaData(element1.Object, "available", typeof(string),true, "category"),
                    new ElementPropertyMetaData(element1.Object, "anotheravailable", typeof(string),true, "category"),
                    new ElementPropertyMetaData(element1.Object, "unavailable", typeof(string), false, "category"),
                    new ElementPropertyMetaData(element1.Object, "differentcategory", typeof(string),true, "another category"),
                    new ElementPropertyMetaData(element1.Object, "nocategory", typeof(string), true)
                };
            if (includePropertyWithException)
            {
                propertyMetaData.Add(new ElementPropertyMetaData(element1.Object, "throws", typeof(string), true));
            }
            element1.SetupGet(e => e.Properties).Returns(propertyMetaData);

            IElementData elementData1 = null;
            // Use a different element data instance based on whether 
            // we want it to be able to throw an exception or not.
            if (includePropertyWithException == false)
            {
                elementData1 = new DictionaryElementData(new TestLogger<DictionaryElementData>(), _pipeline.Object);
            }
            else
            {
                var data = new PropertyExceptionElementData(new TestLogger<DictionaryElementData>(), _pipeline.Object);
                data.ConfigureExceptionForProperty("throws", new Exception("This property is broken!"));
                elementData1 = data;
            }
            // Set up the values for the available properties
            elementData1["available"] = "a value";
            elementData1["anotheravailable"] = "a value";
            elementData1["differentcategory"] = "a value";
            elementData1["nocategory"] = "a value";

            // Set up the process method to add the values to the flow data
            _pipeline.Setup(p => p.Process(It.IsAny<IFlowData>()))
                .Callback((IFlowData data) =>
                {
                    data.GetOrAdd("element1", (p) => elementData1);
                });
            // Set up the element in the pipeline
            _pipeline.SetupGet(i => i.FlowElements).Returns(new List<IFlowElement>() { element1.Object });
        }

        /// <summary>
        /// Test that when calling the GetWhere method, filtering on properties
        /// which have 'Available' set to true, a valid set of properties and
        /// values are returned. Also check that the values returned are
        /// correct.
        /// </summary>
        [TestMethod]
        public void FlowData_GetWhere_Available()
        {
            ConfigureGetWhere();
            _flowData.Process();
            foreach (var value in _flowData.GetWhere(i => i.Available))
            {
                Assert.IsNotNull(value);
                Assert.IsNotNull(value.Key);
                Assert.IsTrue(value.Key.StartsWith("element1."));
                Assert.IsNotNull(value.Value);
                Assert.AreEqual(_flowData.Get("element1")[value.Key.Split(".")[1]], value.Value);
            }
            Assert.AreEqual(4, _flowData.GetWhere(i => i.Available).Count());
        }

        /// <summary>
        /// Test that when calling the GetWhere method, filtering on properties
        /// which have 'Category' set to 'category', only the properties in
        /// that category are returned. Also check that the values returned are
        /// correct.
        /// </summary>
        [TestMethod]
        public void FlowData_GetWhere_Category()
        {
            ConfigureGetWhere();
            _flowData.Process();
            foreach (var value in _flowData.GetWhere(i => i.Category == "category"))
            {
                Assert.IsNotNull(value);
                Assert.IsNotNull(value.Key);
                Assert.IsTrue(
                    value.Key.Equals("element1.available") ||
                    value.Key.Equals("element1.anotheravailable"));
                Assert.IsNotNull(value.Value);
                Assert.AreEqual(_flowData.Get("element1")[value.Key.Split(".")[1]], value.Value);
            }
            Assert.AreEqual(2, _flowData.GetWhere(i => i.Category == "category").Count());
        }

        /// <summary>
        /// Test that when calling the GetWhere method, filtering on all
        /// properties so that unavailable properties are also included, the
        /// unavailable property is not returned and does not throw an
        /// exception. Also check that the values returned are correct.
        /// </summary>
        [TestMethod]
        public void FlowData_GetWhere_UnavailableExcluded()
        {
            ConfigureGetWhere();
            _flowData.Process();
            foreach (var value in _flowData.GetWhere(i => true))
            {
                Assert.IsNotNull(value);
                Assert.IsNotNull(value.Key);
                Assert.IsTrue(value.Key.StartsWith("element1."));
                Assert.AreNotEqual("element1.unavailable", value.Key);
                Assert.IsNotNull(value.Value);
                Assert.AreEqual(_flowData.Get("element1")[value.Key.Split(".")[1]], value.Value);
            }
            Assert.AreEqual(4, _flowData.GetWhere(i => true).Count());
        }

        /// <summary>
        /// Test that when calling the GetWhere method, a property that
        /// would throw an exception is just ignored rather than
        /// the exception being thrown to the caller.
        /// </summary>
        [TestMethod]
        public void FlowData_GetWhere_PropertyThrowsException()
        {
            ConfigureGetWhere();
            _flowData.Process();
            foreach (var value in _flowData.GetWhere(i => true))
            {
                Assert.IsNotNull(value);
                Assert.IsNotNull(value.Key);
                Assert.IsTrue(value.Key.StartsWith("element1."));
                Assert.AreNotEqual("element1.unavailable", value.Key);
                Assert.AreNotEqual("element1.throws", value.Key);
                Assert.IsNotNull(value.Value);
                Assert.AreEqual(_flowData.Get("element1")[value.Key.Split(".")[1]], value.Value);
            }
            Assert.AreEqual(4, _flowData.GetWhere(i => true).Count());
        }

        /// <summary>
        /// Check that a FlowData instance can be created with null constructor
        /// parameters.
        /// This is required to reduce the complexity of unit tests that rely on
        /// functionality from <see cref="FlowData"/>.
        /// </summary>
        [TestMethod]
        public void FlowData_CreateWithNullPipeline()
        {
            _flowData = new FlowData(_logger.Object, null, null);
            Assert.IsNotNull(_flowData);
        }

        #endregion

        public interface IDisposableData : IElementData, IDisposable
        {

        }

        /// <summary>
        /// Test that when disposing of the FlowData instance, an IDisposable
        /// ElementData is disposed of.
        /// </summary>
        [TestMethod]
        public void FlowData_Close()
        {
            _flowData.Process();

            var data = new Mock<IDisposableData>();

            _flowData.GetOrAdd(
                "test",
                (p) => { return data.Object; });

            DisposeFlowData();

            data.Verify(d => d.Dispose(), Times.Once);
        }


        /// <summary>
        /// Test that when disposing of the FlowData instance that
        /// an ElementData which is not IDisposable does not
        /// throw an exception.
        /// </summary>
        [TestMethod]
        public void FlowData_CloseNotDisposable()
        {
            _flowData.Process();

            var data = new Mock<IElementData>();

            _flowData.GetOrAdd(
                "test",
                (p) => { return data.Object; });

            DisposeFlowData();
        }
    }
}
