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
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Core.Tests.Data
{
    [TestClass]
    public class AccessiblePropertyMetaDataTests
    {
        private Mock<IFlowElement> _flowElement;

        [TestInitialize]
        public void Init()
        {
            _flowElement = new Mock<IFlowElement>();
        }

        private string TypeNameFor(Type t)
        {
            var source = new ElementPropertyMetaData(
                _flowElement.Object, "p", t, true, "", null);
            return new PropertyMetaData(source).Type;
        }

        [TestMethod]
        public void TypeName_String()
        {
            Assert.AreEqual("String", TypeNameFor(typeof(string)));
        }

        [TestMethod]
        public void TypeName_AspectPropertyValueOfString()
        {
            Assert.AreEqual(
                "String",
                TypeNameFor(typeof(IAspectPropertyValue<string>)));
        }

        [TestMethod]
        public void TypeName_PlainListReportsArray()
        {
            Assert.AreEqual(
                "Array",
                TypeNameFor(typeof(IAspectPropertyValue<IReadOnlyList<string>>)));
        }

        [TestMethod]
        public void TypeName_WeightedStringList()
        {
            Assert.AreEqual(
                "WeightedString",
                TypeNameFor(typeof(IAspectPropertyValue<IReadOnlyList<IWeightedValue<string>>>)));
        }

        [TestMethod]
        public void TypeName_WeightedInt32List()
        {
            Assert.AreEqual(
                "WeightedInt32",
                TypeNameFor(typeof(IAspectPropertyValue<IReadOnlyList<IWeightedValue<int>>>)));
        }

        [TestMethod]
        public void TypeName_WeightedSingleList()
        {
            Assert.AreEqual(
                "WeightedSingle",
                TypeNameFor(typeof(IAspectPropertyValue<IReadOnlyList<IWeightedValue<float>>>)));
        }

        [TestMethod]
        public void TypeName_WeightedListAlsoWorksWithListAndIList()
        {
            Assert.AreEqual(
                "WeightedString",
                TypeNameFor(typeof(IAspectPropertyValue<List<IWeightedValue<string>>>)));
            Assert.AreEqual(
                "WeightedString",
                TypeNameFor(typeof(IAspectPropertyValue<IList<IWeightedValue<string>>>)));
        }

        [TestMethod]
        public void TypeName_WktString()
        {
            Assert.AreEqual(
                "WktString",
                TypeNameFor(typeof(IAspectPropertyValue<WktString>)));
        }
    }
}
