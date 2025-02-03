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

using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.TypedMap;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Core.Tests.TypedMap
{
    [TestClass]
    public class TypedKeyMapTestNonConcurrent : TypedKeyMapTestBase
    {
        [TestInitialize]
        public void Init()
        {
            _map = new TypedKeyMap();
        }

        /// <summary>
        /// Add an item to the map and retrieve it.
        /// </summary>
        [TestMethod]
        public void TypedMap_NonConcurrent_AddRetrieve() { TypedMap_AddRetrieve(); }

        /// <summary>
        /// Add a complex value type to the map and retrieve it.
        /// </summary>
        [TestMethod]
        public void TypedMap_NonConcurrent_ComplexValueType() { TypedMap_ComplexValueType(); }

        /// <summary>
        /// Add a complex reference type to the map and retrieve it.
        /// </summary>
        [TestMethod]
        public void TypedMap_NonConcurrent_ComplexReferenceType() { TypedMap_ComplexReferenceType(); }

        /// <summary>
        /// Add two items to the map with different types and different keys.
        /// Retrieve them both and check that the values are as expected.
        /// </summary>
        [TestMethod]
        public void TypedMap_NonConcurrent_MultipleDataObjects() { TypedMap_MultipleDataObjects(); }

        /// <summary>
        /// Add an object to the map and then overwrite it using a key with 
        /// the same name but a different type.
        /// Retrieve the object using the key and check that it contains
        /// the object that was used to overwrite the initial data.
        /// </summary>
        [TestMethod]
        public void TypedMap_NonConcurrent_Overwrite() { TypedMap_Overwrite(); }

        /// <summary>
        /// Check that the map will throw an exception if there is no 
        /// data for the given key.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(KeyNotFoundException))]
        public void TypedMap_NonConcurrent_NoData() { TypedMap_NoData(); }

        /// <summary>
        /// Check that an <see cref="InvalidCastException"/> is thrown if the 
        /// data is not of the expected type.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidCastException))]
        public void TypedMap_NonConcurrent_WrongKeyType() { TypedMap_WrongKeyType(); }

        /// <summary>
        /// Check that a null value can be stored and retrieved successfully.
        /// </summary>
        [TestMethod]
        public void TypedMap_NonConcurrent_NullValue() { TypedMap_NullValue(); }

        /// <summary>
        /// Check that the Get&lt;T&gt;() method works as expected.
        /// </summary>
        [TestMethod]
        public void TypedMap_NonConcurrent_GetByType() { TypedMap_GetByType(); }

        /// <summary>
        /// Check that the Get&lt;T&gt;() method throws an exception
        /// when it contains no data of the requested type.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(PipelineDataException))]
        public void TypedMap_NonConcurrent_GetByTypeNoMatch() { TypedMap_GetByTypeNoMatch(); }

        /// <summary>
        /// Check that the Get&lt;T&gt;() method throws an exception
        /// when it contains multiple instances of the requested type.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(PipelineDataException))]
        public void TypedMap_NonConcurrent_GetByTypeMultiMatch() { TypedMap_GetByTypeMultiMatch(); }

        /// <summary>
        /// Check that the Get&lt;T&gt;() method works as expected when
        /// the requested type is an interface that the stored type implements.
        /// </summary>
        [TestMethod]
        public void TypedMap_NonConcurrent_GetByTypeInterface() { TypedMap_GetByTypeInterface(); }
        
        // Test the TryGetValue method in various scenarios.
        // See base class for full comments on each method.
        [TestMethod]
        public void TypedMap_NonConcurrent_TryGetValue_GoodKey_String() { TypedMap_TryGetValue_GoodKey_String(); }
        [TestMethod]
        public void TypedMap_NonConcurrent_TryGetValue_BadKeyName_String() { TypedMap_TryGetValue_BadKeyName_String(); }
        [TestMethod]
        public void TypedMap_NonConcurrent_TryGetValue_BadKeyType_String() { TypedMap_TryGetValue_BadKeyType_String(); }
        [TestMethod]
        public void TypedMap_NonConcurrent_TryGetValue_GoodKey_ComplexType() { TypedMap_TryGetValue_GoodKey_ComplexType(); }
        [TestMethod]
        public void TypedMap_NonConcurrent_TryGetValue_GoodKeyInterface_ComplexType() { TypedMap_TryGetValue_GoodKeyInterface_ComplexType(); }
        [TestMethod]
        public void TypedMap_NonConcurrent_TryGetValue_BadKeyName_ComplexType() { TypedMap_TryGetValue_BadKeyName_ComplexType(); }
        [TestMethod]
        public void TypedMap_NonConcurrent_TryGetValue_BadKeyType_ComplexType() { TypedMap_TryGetValue_BadKeyType_ComplexType(); }
    }
}
