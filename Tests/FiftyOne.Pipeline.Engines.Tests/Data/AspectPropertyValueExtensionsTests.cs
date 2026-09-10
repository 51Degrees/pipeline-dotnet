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

using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;

namespace FiftyOne.Pipeline.Engines.Tests.Data
{
    [TestClass]
    public class AspectPropertyValueExtensionsTests
    {
        /// <summary>
        /// Element data interface used to exercise the selector overloads,
        /// mirroring the shape of an engine's typed data (for example
        /// device data exposing HardwareName).
        /// </summary>
        public interface ITestData : IElementData
        {
            IAspectPropertyValue<string> Name { get; }
        }

        /// <summary>
        /// Check that a populated property returns its value.
        /// </summary>
        [TestMethod]
        public void SafeValue_HasValue_ReturnsValue()
        {
            var property = new AspectPropertyValue<string>("abc");
            Assert.AreEqual("abc", property.SafeValue());
        }

        /// <summary>
        /// Check that a property without a value returns the fallback
        /// rather than throwing NoValueException.
        /// </summary>
        [TestMethod]
        public void SafeValue_NoValue_ReturnsFallback()
        {
            var property = new AspectPropertyValue<int>();
            Assert.AreEqual(5, property.SafeValue(5));
        }

        /// <summary>
        /// Check that a null property reference returns the fallback.
        /// </summary>
        [TestMethod]
        public void SafeValue_NullProperty_ReturnsFallback()
        {
            IAspectPropertyValue<string> property = null;
            Assert.AreEqual("fallback", property.SafeValue("fallback"));
        }

        /// <summary>
        /// Check that SafeHasValue reflects the underlying HasValue and
        /// tolerates a null reference.
        /// </summary>
        [TestMethod]
        public void SafeHasValue_States()
        {
            Assert.IsTrue(new AspectPropertyValue<int>(1).SafeHasValue());
            Assert.IsFalse(new AspectPropertyValue<int>().SafeHasValue());
            IAspectPropertyValue<int> property = null;
            Assert.IsFalse(property.SafeHasValue());
        }

        /// <summary>
        /// Check that the selector overload returns the value when the
        /// property resolves normally.
        /// </summary>
        [TestMethod]
        public void SafeValue_Selector_ReturnsValue()
        {
            var data = new Mock<ITestData>();
            data.SetupGet(d => d.Name)
                .Returns(new AspectPropertyValue<string>("abc"));

            Assert.AreEqual("abc", data.Object.SafeValue(d => d.Name));
        }

        /// <summary>
        /// Check that the selector overload returns the fallback when
        /// resolving the property reference itself throws
        /// PropertyMissingException, the case a direct SafeValue call on
        /// the property cannot guard because the accessor throws first.
        /// </summary>
        [TestMethod]
        public void SafeValue_SelectorThrowsPropertyMissing_ReturnsFallback()
        {
            var data = new Mock<ITestData>();
            data.SetupGet(d => d.Name)
                .Throws(new PropertyMissingException("Name is missing"));

            Assert.AreEqual(
                "fallback",
                data.Object.SafeValue(d => d.Name, "fallback"));
        }

        /// <summary>
        /// Check that the selector overload of SafeHasValue folds a
        /// PropertyMissingException from the accessor into false.
        /// </summary>
        [TestMethod]
        public void SafeHasValue_SelectorThrowsPropertyMissing_False()
        {
            var data = new Mock<ITestData>();
            data.SetupGet(d => d.Name)
                .Throws(new PropertyMissingException("Name is missing"));

            Assert.IsFalse(data.Object.SafeHasValue(d => d.Name));
        }

        /// <summary>
        /// Check that the selector overloads tolerate null element data.
        /// </summary>
        [TestMethod]
        public void Selector_NullData_Fallback()
        {
            ITestData data = null;
            Assert.AreEqual("fallback", data.SafeValue(d => d.Name, "fallback"));
            Assert.IsFalse(data.SafeHasValue(d => d.Name));
        }

        /// <summary>
        /// Check that a null selector is rejected loudly, as that is a
        /// programming error rather than a data condition.
        /// </summary>
        [TestMethod]
        public void Selector_NullSelector_Throws()
        {
            var data = new Mock<ITestData>();
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                data.Object.SafeValue(
                    (Func<ITestData, IAspectPropertyValue<string>>)null);
            });
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                data.Object.SafeHasValue(
                    (Func<ITestData, IAspectPropertyValue<string>>)null);
            });
        }
    }
}
