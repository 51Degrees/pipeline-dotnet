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

using FiftyOne.Pipeline.Core.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FiftyOne.Pipeline.Core.Tests.Data
{
    /// <summary>
    /// Tests for the DataKey class
    /// </summary>
    [TestClass]
    public class DataKeyTests
    {
        /// <summary>
        /// Inner class that is used as a representative reference type
        /// for tests that require it.
        /// </summary>
        private class EqualityTest
        {
            public string Value { get; set; }
            public override bool Equals(object obj)
            {
                EqualityTest other = obj as EqualityTest;
                return other == null ? false : Value == other.Value;
            }
            public override int GetHashCode()
            {
                return Value.GetHashCode();
            }
        }

        /// <summary>
        /// Check that hash codes match and the keys are considered
        /// equal if they have the same key and string data values.
        /// </summary>
        [TestMethod]
        public void DataKey_Equality_Strings()
        {
            DataKey key = new DataKeyBuilder().Add(1, "abc", "123").Build();
            DataKey key2 = new DataKeyBuilder().Add(1, "abc", "123").Build();

            Assert.AreEqual(key.GetHashCode(), key2.GetHashCode());
            Assert.IsTrue(key.Equals(key2));
        }

        /// <summary>
        /// Check that hash codes match and the keys are considered
        /// equal if they have the same key and integer data values.
        /// </summary>
        [TestMethod]
        public void DataKey_Equality_Integers()
        {
            DataKey key = new DataKeyBuilder().Add(1, "abc", 123).Build();
            DataKey key2 = new DataKeyBuilder().Add(1, "abc", 123).Build();

            Assert.AreEqual(key.GetHashCode(), key2.GetHashCode());
            Assert.IsTrue(key.Equals(key2));
        }

        /// <summary>
        /// Check that hash codes match and the keys are considered
        /// equal if they have the same key and reference data values.
        /// </summary>
        [TestMethod]
        public void DataKey_Equality_ReferenceType()
        {
            DataKey key = new DataKeyBuilder()
                .Add(1, "abc", new EqualityTest() { Value = "123" }).Build();
            DataKey key2 = new DataKeyBuilder()
                .Add(1, "abc", new EqualityTest() { Value = "123" }).Build();

            Assert.AreEqual(key.GetHashCode(), key2.GetHashCode());
            Assert.IsTrue(key.Equals(key2));
        }

        /// <summary>
        /// Check that hash codes match and the keys are considered
        /// equal if they multiple matching key/value pairs
        /// </summary>
        [TestMethod]
        public void DataKey_Equality_MixedTypes()
        {
            DataKey key = new DataKeyBuilder().Add(1, "abc", 123)
                .Add(1, "abc", "123").Build();
            DataKey key2 = new DataKeyBuilder().Add(1, "abc", 123)
                .Add(1, "abc", "123").Build();

            Assert.AreEqual(key.GetHashCode(), key2.GetHashCode());
            Assert.IsTrue(key.Equals(key2));
        }

        /// <summary>
        /// Check that keys are not considered equal if they have the 
        /// same key with different values
        /// (Note that hash code is not checked as hash collisions are 
        /// possible for non-equal values)
        /// </summary>
        [TestMethod]
        public void DataKey_Equality_DifferentValues()
        {
            DataKey key = new DataKeyBuilder().Add(1, "abc", 123).Build();
            DataKey key2 = new DataKeyBuilder().Add(1, "abc", 12).Build();

            Assert.IsFalse(key.Equals(key2));
        }

        /// <summary>
        /// Check that keys are not considered equal if they have 
        /// the same keys with different non-matching reference type values
        /// (Note that hash code is not checked as hash collisions are 
        /// possible for non-equal values)
        /// </summary>
        [TestMethod]
        public void DataKey_Equality_ReferenceTypeNoMatch()
        {
            DataKey key = new DataKeyBuilder()
                .Add(1, "abc", new EqualityTest() { Value = "123" }).Build();
            DataKey key2 = new DataKeyBuilder()
                .Add(1, "abc", new EqualityTest() { Value = "12" }).Build();

            Assert.IsFalse(key.Equals(key2));
        }

        /// <summary>
        /// Check that keys are considered equal if they have 
        /// different key names.
        /// All that should matter is the order parameter and the key values.
        /// </summary>
        [TestMethod]
        public void DataKey_Equality_DespiteDifferentKeyNames()
        {
            DataKey key = new DataKeyBuilder()
                .Add(1, "abc", 123).Build();
            DataKey key2 = new DataKeyBuilder()
                .Add(1, "ab", 123).Build();

            Assert.IsTrue(key.Equals(key2));
        }

        /// <summary>
        /// Check that keys are considered equal if they have 
        /// different key names.
        /// All that should matter is the order parameter and the key values.
        /// </summary>
        [TestMethod]
        public void DataKey_Equality_DespiteOrderOfAdding()
        {
            DataKey key = new DataKeyBuilder()
                .Add(1, "abc", 123).Add(2, "xyz", 789).Build();
            DataKey key2 = new DataKeyBuilder()
                .Add(2, "xyz", 789).Add(1, "abc", 123).Build();

            Assert.IsTrue(key.Equals(key2));
        }

        /// <summary>
        /// Check that keys are considered equal if they have 
        /// different key names.
        /// Demonstrate that key name is used to order keys if the 
        /// order value is the same.
        /// </summary>
        [TestMethod]
        public void DataKey_Equality_DespiteSameOrderValueAndOrderOfAdding()
        {
            DataKey key = new DataKeyBuilder()
                .Add(1, "abc", 123).Add(1, "xyz", 789).Build();
            DataKey key2 = new DataKeyBuilder()
                .Add(1, "xyz", 789).Add(1, "abc", 123).Build();

            Assert.IsTrue(key.Equals(key2));
        }

        /// <summary>
        /// Check that GetHashCode does not crash if a value is null
        /// </summary>
        [TestMethod]
        public void DataKey_NullValue_GetHashCode()
        {
            DataKey key = new DataKeyBuilder().Add(1, "abc", null).Build();
            key.GetHashCode();
        }

        /// <summary>
        /// Check that two values will be considered equal if both are null 
        /// </summary>
        [TestMethod]
        public void DataKey_NullValue_Equality()
        {
            DataKey key1 = new DataKeyBuilder().Add(1, "abc", null).Build();
            DataKey key2 = new DataKeyBuilder().Add(1, "abc", null).Build();
            Assert.IsTrue(key1.Equals(key2));
        }

        /// <summary>
        /// Check that two values will not be considered equal if one is null 
        /// and the other is not
        /// </summary>
        [TestMethod]
        public void DataKey_NullValue_Inequality()
        {
            DataKey key1 = new DataKeyBuilder().Add(1, "abc", null).Build();
            DataKey key2 = new DataKeyBuilder().Add(1, "abc", "value").Build();
            Assert.IsFalse(key1.Equals(key2));
        }
    }
}
