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

using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FiftyOne.Pipeline.Engines.Tests.Data
{
    [TestClass]
    public class AspectPropertyValueTests
    {
        /// <summary>
        /// Check that the instance is in the expected state after using
        /// the constructor that takes a value.
        /// </summary>
        [TestMethod]
        public void AspectPropertyValue_ValueConstructor()
        {
            AspectPropertyValue<int> value = new AspectPropertyValue<int>(1);
            Assert.AreEqual(1, value.Value);
            Assert.IsTrue(value.HasValue);
        }

        /// <summary>
        /// Check that the instance is in the expected state after using
        /// the default constructor.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(NoValueException))]
        public void AspectPropertyValue_DefaultConstructor()
        {
            AspectPropertyValue<int> value = new AspectPropertyValue<int>();
            Assert.IsFalse(value.HasValue);
            var result = value.Value;
        }

        /// <summary>
        /// Check that the instance is in the expected state after using
        /// the default constructor and then setting the value.
        /// </summary>
        [TestMethod]
        public void AspectPropertyValue_DefaultConstructor_SetValue()
        {
            AspectPropertyValue<int> value = new AspectPropertyValue<int>();
            value.Value = 1;
            Assert.AreEqual(1, value.Value);
            Assert.IsTrue(value.HasValue);
        }

        /// <summary>
        /// Check that the custom message works as expected.
        /// </summary>
        [TestMethod]
        public void AspectPropertyValue_CustomErrorMessage()
        {
            AspectPropertyValue<int> value = new AspectPropertyValue<int>();
            value.NoValueMessage = "CUSTOM MESSAGE";
            Assert.IsFalse(value.HasValue);
            try
            {
                var result = value.Value;
                Assert.Fail("Expected NoValueException to be thrown");
            }
            catch (NoValueException ex)
            {
                Assert.AreEqual("CUSTOM MESSAGE", ex.Message);
            }
        }
    }
}
