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
using FiftyOne.Pipeline.Core.Tests.HelperClasses;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Core.Tests.Data
{
    /// <summary>
    /// Test that the <see cref="ElementData"/> class behaves as expected
    /// </summary>
    [TestClass]
    public class ElementDataTests
    {
        private Mock<IPipeline> _pipeline = new Mock<IPipeline>();

        /// <summary>
        /// Test storing and retrieving string data
        /// </summary>
        [TestMethod]
        public void ElementData_String()
        {
            TestElementData data = new TestElementData(_pipeline.Object);
            string key = "key";
            data[key] = "value";
            var result = data[key];

            Assert.AreEqual("value", result);
        }

        /// <summary>
        /// Test storing and retrieving a simple value type
        /// </summary>
        [TestMethod]
        public void ElementData_SimpleValueType()
        {
            TestElementData data = new TestElementData(_pipeline.Object);
            string key = "key";
            data[key] = 1;
            var result = data[key];

            Assert.AreEqual(1, result);
        }

        /// <summary>
        /// Test storing and retrieving a complex value type
        /// </summary>
        [TestMethod]
        public void ElementData_ComplexValueType()
        {
            TestElementData data = new TestElementData(_pipeline.Object);
            string key = "key";
            data[key] = new KeyValuePair<string, int>("test", 1);
            var result = (KeyValuePair<string, int>)data[key];

            Assert.AreEqual("test", result.Key);
            Assert.AreEqual(1, result.Value);
        }

        /// <summary>
        /// Test storing and retrieving a complex reference type
        /// </summary>
        [TestMethod]
        public void ElementData_ComplexReferenceType()
        {
            TestElementData data = new TestElementData(_pipeline.Object);
            string key = "key";
            data[key] = new List<string>() { "a", "b" };
            var result = data[key] as List<string>;

            Assert.IsTrue(result.Contains("a"));
            Assert.IsTrue(result.Contains("b"));
        }

        /// <summary>
        /// Test retrieving a value using a key cased differently to that used
        /// to set the value. By default the dictionary is case insensitive, so
        /// the same value will be returned.
        /// </summary>
        [TestMethod]
        public void ElementData_CaseInsensitiveKey()
        {
            TestElementData data = new TestElementData(_pipeline.Object);
            string key = "key";
            data[key] = "value";
            var result = data[key];
            var otherResult = data["Key"];
            Assert.AreEqual("value", result);
            Assert.AreEqual("value", otherResult);
        }

        /// <summary>
        /// Test setting a value using a key cased differently to that used
        /// to set the initial value. By default the dictionary is case
        /// insensitive, so the value will be overwritten.
        /// </summary>
        [TestMethod]
        public void ElementData_CaseInsensitiveKeySet()
        {
            TestElementData data = new TestElementData(_pipeline.Object);
            string key = "key";
            data[key] = "value";
            data["Key"] = "otherValue";
            var result = data[key];
            Assert.AreEqual("otherValue", result);
        }

        /// <summary>
        /// Test retrieving a value using a key cased differently to that used
        /// to set the value. Passing in a case sensitive dictionary to use 
        /// means that the same value should not be returned.
        /// </summary>
        [TestMethod]
        public void ElementData_CaseSensitiveKey()
        {
            TestElementData data = new TestElementData(
                _pipeline.Object,
                new Dictionary<string, object>());
            string key = "key";
            data[key] = "value";
            var result = data[key];
            var otherResult = data["Key"];

            Assert.AreEqual("value", result);
            Assert.IsNull(otherResult);
        }

        /// <summary>
        /// Test setting a value using a key cased differently to that used
        /// to set the initial value. Passing in a case sensitive dictionary to
        /// use means that the value should not be overwritten.
        /// </summary>
        [TestMethod]
        public void ElementData_CaseSensitiveKeySet()
        {
            TestElementData data = new TestElementData(
                _pipeline.Object,
                new Dictionary<string, object>());
            string key = "key";
            data[key] = "value";
            data["Key"] = "otherValue";
            var result = data[key];
            Assert.AreEqual("value", result);
        }

        /// <summary>
        /// Test storing string data and retrieving it through the 
        /// <see cref="IElementData"/> interface
        /// </summary>
        [TestMethod]
        public void ElementData_AsIElementData()
        {
            TestElementData data = new TestElementData(_pipeline.Object);
            string key = "key";
            data[key] = "value";
            var dataAsInterface = data as IElementData;
            var result = dataAsInterface[key];

            Assert.AreEqual("value", result);
        }
        /// <summary>
        /// Test storing string data and retrieving it through the 
        /// AsDictionary method
        /// </summary>
        [TestMethod]
        public void ElementData_AsDictionary()
        {
            TestElementData data = new TestElementData(_pipeline.Object);
            string key = "key";
            data[key] = "value";
            var dataAsDict = data.AsDictionary();

            var result = dataAsDict[key];

            Assert.AreEqual("value", result);
        }

        /// <summary>
        /// Test what happens when the specified key is not present in the
        /// data dictionary.
        /// </summary>
        [TestMethod]
        public void ElementData_NoData()
        {
            TestElementData data = new TestElementData(_pipeline.Object);
            string key = "key";

            var result = data[key];

            Assert.IsNull(result);
        }

        /// <summary>
        /// Test that the expected exception is thrown when the key 
        /// parameter is null.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ElementData_NullKey()
        {
            TestElementData data = new TestElementData(_pipeline.Object);
            string key = null;

            var result = data[key];
        }

        /// <summary>
        /// Test that the populate from dictionary function works as 
        /// expected.
        /// </summary>
        [TestMethod]
        public void ElementData_PopulateFromDictionary_SingleValue()
        {
            TestElementData data = new TestElementData(_pipeline.Object);
            Dictionary<string, object> newData = new Dictionary<string, object>();
            newData.Add("key", "value");
            data.PopulateFromDictionary(newData);

            Assert.AreEqual("value", data["key"]);
        }

        /// <summary>
        /// Test that the populate from dictionary function works as 
        /// expected.
        /// </summary>
        [TestMethod]
        public void ElementData_PopulateFromDictionary_SingleValueWithProperty()
        {
            TestElementData data = new TestElementData(_pipeline.Object);
            Dictionary<string, object> newData = new Dictionary<string, object>();
            newData.Add("result", "value");
            data.PopulateFromDictionary(newData);

            Assert.AreEqual("value", data.Result);
        }

        /// <summary>
        /// Test that the populate from dictionary function works as 
        /// expected.
        /// </summary>
        [TestMethod]
        public void ElementData_PopulateFromDictionary_TwoValues()
        {
            TestElementData data = new TestElementData(_pipeline.Object);
            Dictionary<string, object> newData = new Dictionary<string, object>();
            newData.Add("key1", "value1");
            newData.Add("key2", "value2");
            data.PopulateFromDictionary(newData);

            Assert.AreEqual("value1", data["key1"]);
            Assert.AreEqual("value2", data["key2"]);
        }

        /// <summary>
        /// Test that the populate from dictionary function works as 
        /// expected.
        /// </summary>
        [TestMethod]
        public void ElementData_PopulateFromDictionary_Overwrite()
        {
            TestElementData data = new TestElementData(_pipeline.Object);
            Dictionary<string, object> newData = new Dictionary<string, object>();
            data["key1"] = "valueA";
            newData.Add("key1", "valueB");
            data.PopulateFromDictionary(newData);

            Assert.AreEqual("valueB", data["key1"]);
        }
    }
}
