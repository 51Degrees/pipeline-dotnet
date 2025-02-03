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

using FiftyOne.Pipeline.Core.TypedMap;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Core.Tests.TypedMap
{
    /// <summary>
    /// These tests validate the behavior of the <see cref="TypedKeyMap"/>
    /// </summary>
    public abstract class TypedKeyMapTestBase
    {
        internal TypedKeyMap _map;

        /// <summary>
        /// Add an item to the map and retrieve it.
        /// </summary>
        protected void TypedMap_AddRetrieve()
        {
            string dataToStore = "testdata";
            ITypedKey<string> key = new TypedKey<string>("datakey");
            _map.Add(key, dataToStore);

            var result = _map.Get(key);

            Assert.AreEqual(dataToStore, result);
        }

        /// <summary>
        /// Add a complex value type to the map and retrieve it.
        /// </summary>
        protected void TypedMap_ComplexValueType()
        {
            KeyValuePair<int, string> dataToStore = new KeyValuePair<int, string>(1, "testdata");
            ITypedKey<KeyValuePair<int, string>> key = new TypedKey<KeyValuePair<int, string>>("datakey");
            _map.Add(key, dataToStore);

            var result = _map.Get(key);

            Assert.IsTrue(result.Key == dataToStore.Key &&
                result.Value == dataToStore.Value);
        }

        /// <summary>
        /// Add a complex reference type to the map and retrieve it.
        /// </summary>
        protected void TypedMap_ComplexReferenceType()
        {
            List<string> dataToStore = new List<string>() { "a", "b", "c" };
            ITypedKey<List<string>> key = new TypedKey<List<string>>("datakey");
            _map.Add(key, dataToStore);

            var result = _map.Get(key);

            Assert.AreEqual(3, result.Count);
            Assert.IsTrue(result.Contains("a"));
            Assert.IsTrue(result.Contains("b"));
            Assert.IsTrue(result.Contains("c"));
        }

        /// <summary>
        /// Add two items to the map with different types and different keys.
        /// Retrieve them both and check that the values are as expected.
        /// </summary>
        protected void TypedMap_MultipleDataObjects()
        {
            KeyValuePair<int, string> dataToStore1 = new KeyValuePair<int, string>(1, "testdata");
            ITypedKey<KeyValuePair<int, string>> key1 = new TypedKey<KeyValuePair<int, string>>("datakey1");
            _map.Add(key1, dataToStore1);
            string dataToStore2 = "testdata";
            ITypedKey<string> key2 = new TypedKey<string>("datakey2");
            _map.Add(key2, dataToStore2);

            var result1 = _map.Get(key1);
            var result2 = _map.Get(key2);

            Assert.IsTrue(result1.Key == dataToStore1.Key &&
                result1.Value == dataToStore1.Value);
            Assert.AreEqual(dataToStore2, result2);
        }

        /// <summary>
        /// Add an object to the map and then overwrite it using a key with 
        /// the same name but a different type.
        /// Retrieve the object using the key and check that it contains
        /// the object that was used to overwrite the initial data.
        /// </summary>
        protected void TypedMap_Overwrite()
        {
            KeyValuePair<int, string> dataToStore1 = new KeyValuePair<int, string>(1, "testdata");
            ITypedKey<KeyValuePair<int, string>> key1 = new TypedKey<KeyValuePair<int, string>>("datakey");
            _map.Add(key1, dataToStore1);
            string dataToStore2 = "testdata";
            ITypedKey<string> key2 = new TypedKey<string>("datakey");
            _map.Add(key2, dataToStore2);

            var result = _map.Get(key2);

            Assert.AreEqual(dataToStore2, result);
        }

        /// <summary>
        /// Check that the map will throw an exception if there is no 
        /// data for the given key.
        /// </summary>
        protected void TypedMap_NoData()
        {
            ITypedKey<string> key = new TypedKey<string>("datakey");

            var result = _map.Get(key);
        }

        /// <summary>
        /// Check that an <see cref="InvalidCastException"/> is thrown if the 
        /// data is not of the expected type.
        /// </summary>
        protected void TypedMap_WrongKeyType()
        {
            KeyValuePair<int, string> dataToStore1 = new KeyValuePair<int, string>(1, "testdata");
            ITypedKey<KeyValuePair<int, string>> key1 = new TypedKey<KeyValuePair<int, string>>("datakey");
            _map.Add(key1, dataToStore1);
            ITypedKey<string> key2 = new TypedKey<string>("datakey");

            var result = _map.Get(key2);
        }

        /// <summary>
        /// Check that a null value can be stored and retrieved successfully.
        /// </summary>
        protected void TypedMap_NullValue()
        {
            List<string> dataToStore = null;
            ITypedKey<List<string>> key = new TypedKey<List<string>>("datakey");
            _map.Add(key, dataToStore);

            var result = _map.Get(key);

            Assert.IsNull(result);
        }

        /// <summary>
        /// Check that the Get&lt;T&gt;() method works as expected.
        /// </summary>
        protected void TypedMap_GetByType()
        {
            string dataToStore = "TEST";
            ITypedKey<string> key = new TypedKey<string>("datakey");
            _map.Add(key, dataToStore);

            var result = _map.Get<string>();

            Assert.AreEqual("TEST", result);
        }

        /// <summary>
        /// Check that the Get&lt;T&gt;() method throws an exception
        /// when it contains no data of the requested type.
        /// </summary>
        protected void TypedMap_GetByTypeNoMatch()
        {
            string dataToStore = "TEST";
            ITypedKey<string> key = new TypedKey<string>("datakey");
            _map.Add(key, dataToStore);

            var result = _map.Get<int>();
        }

        /// <summary>
        /// Check that the Get&lt;T&gt;() method throws an exception
        /// when it contains multiple instances of the requested type.
        /// </summary>
        protected void TypedMap_GetByTypeMultiMatch()
        {
            string dataToStore = "TEST";
            ITypedKey<string> key1 = new TypedKey<string>("datakey1");
            ITypedKey<string> key2 = new TypedKey<string>("datakey2");
            _map.Add(key1, dataToStore);
            _map.Add(key2, dataToStore);

            var result = _map.Get<string>();
        }

        /// <summary>
        /// Check that the Get&lt;T&gt;() method works as expected when
        /// the requested type is an interface that the stored type implements.
        /// </summary>
        protected void TypedMap_GetByTypeInterface()
        {
            List<string> dataToStore = new List<string>() { "TEST" };
            ITypedKey<List<string>> key = new TypedKey<List<string>>("datakey");
            _map.Add(key, dataToStore);

            var result = _map.Get<IReadOnlyList<string>>();

            Assert.AreEqual(1, result.Count);
        }

        /// <summary>
        /// Test that the TryGetValue method returns true and outputs 
        /// the associated value when a valid key is supplied.
        /// </summary>
        protected void TypedMap_TryGetValue_GoodKey_String()
        {
            string dataToStore = "TEST";
            ITypedKey<string> key1 = new TypedKey<string>("datakey1");
            _map.Add(key1, dataToStore);

            string value = "";
            var result = _map.TryGetValue(key1, out value);

            Assert.IsTrue(result);
            Assert.AreEqual(dataToStore, value);
        }

        /// <summary>
        /// Test that the TryGetValue method returns false and outputs 
        /// the default value when a key is supplied with a name that 
        /// does not exist in the map.
        /// </summary>
        protected void TypedMap_TryGetValue_BadKeyName_String()
        {
            string dataToStore = "TEST";
            ITypedKey<string> key1 = new TypedKey<string>("datakey1");
            ITypedKey<string> key2 = new TypedKey<string>("datakey2");
            _map.Add(key1, dataToStore);

            string value = "";
            var result = _map.TryGetValue(key2, out value);

            Assert.IsFalse(result);
            Assert.AreEqual(null, value);
        }

        /// <summary>
        /// Test that the TryGetValue method returns false and outputs 
        /// the default value when a key is supplied with a name that 
        /// does exist in the map but with an mismatched type.
        /// </summary>
        protected void TypedMap_TryGetValue_BadKeyType_String()
        {
            string dataToStore = "TEST";
            ITypedKey<string> key1 = new TypedKey<string>("datakey1");
            ITypedKey<int> key2 = new TypedKey<int>("datakey1");
            _map.Add(key1, dataToStore);

            int value;
            var result = _map.TryGetValue(key2, out value);

            Assert.IsFalse(result);
            Assert.AreEqual(0, value);
        }

        /// <summary>
        /// Test that the TryGetValue method returns true and outputs 
        /// the associated value when a valid key is supplied and
        /// the key corresponds to a complex type.
        /// </summary>
        protected void TypedMap_TryGetValue_GoodKey_ComplexType()
        {
            List<string> dataToStore = new List<string>() { "TEST" };
            ITypedKey<List<string>> key1 = new TypedKey<List<string>>("datakey1");
            _map.Add(key1, dataToStore);

            List<string> value;
            var result = _map.TryGetValue(key1, out value);

            Assert.IsTrue(result);
            Assert.AreEqual(dataToStore[0], value[0]);
        }

        /// <summary>
        /// Test that the TryGetValue method returns true and outputs 
        /// the associated value when a valid key is supplied and
        /// the key corresponds to an interface that the value can
        /// be cast to.
        /// </summary>
        protected void TypedMap_TryGetValue_GoodKeyInterface_ComplexType()
        {
            List<string> dataToStore = new List<string>() { "TEST" };
            ITypedKey<List<string>> key1 = new TypedKey<List<string>>("datakey1");
            ITypedKey<IList<string>> key2 = new TypedKey<IList<string>>("datakey1");
            _map.Add(key1, dataToStore);

            IList<string> value;
            var result = _map.TryGetValue(key2, out value);

            Assert.IsTrue(result);
            Assert.AreEqual(dataToStore[0], value[0]);
        }

        /// <summary>
        /// Test that the TryGetValue method returns false and outputs 
        /// the default value when a key is supplied with a name that 
        /// does not exist in the map.
        /// </summary>
        protected void TypedMap_TryGetValue_BadKeyName_ComplexType()
        {
            List<string> dataToStore = new List<string>() { "TEST" };
            ITypedKey<List<string>> key1 = new TypedKey<List<string>>("datakey1");
            ITypedKey<List<string>> key2 = new TypedKey<List<string>>("datakey2");
            _map.Add(key1, dataToStore);

            List<string> value;
            var result = _map.TryGetValue(key2, out value);

            Assert.IsFalse(result);
            Assert.AreEqual(null, value);
        }

        /// <summary>
        /// Test that the TryGetValue method returns false and outputs 
        /// the default value when a key is supplied with a name that 
        /// does exist in the map but with an mismatched type.
        /// </summary>
        protected void TypedMap_TryGetValue_BadKeyType_ComplexType()
        {
            List<string> dataToStore = new List<string>() { "TEST" };
            ITypedKey<List<string>> key1 = new TypedKey<List<string>>("datakey1");
            ITypedKey<Dictionary<string, int>> key2 = new TypedKey<Dictionary<string, int>>("datakey1");
            _map.Add(key1, dataToStore);

            Dictionary<string, int> value;
            var result = _map.TryGetValue(key2, out value);

            Assert.IsFalse(result);
            Assert.AreEqual(null, value);
        }
    }
}
