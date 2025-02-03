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
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Core.Tests.Data
{
    [TestClass]
    public class ElementPropertyMetaDataTests
    {
        private Mock<IFlowElement> _flowElement;

        [TestInitialize]
        public void Init()
        {
            _flowElement = new Mock<IFlowElement>();
        }

        /// <summary>
        /// Check that an property meta data instance with a null
        /// 'item properties' list will result in a null item
        /// property dictionary without any errors.
        /// </summary>
        [TestMethod]
        public void ElementPropertyMetaData_ItemPropertiesDictionaryNull()
        {
            ElementPropertyMetaData metaData =
                new ElementPropertyMetaData(_flowElement.Object, "test", typeof(string), true, "", null);

            Assert.IsNull(metaData.ItemPropertyDictionary);
        }

        /// <summary>
        /// Check that the dictionary returned by the 
        /// 'ItemPropertyDictionary' property is case insensitive.
        /// </summary>
        [TestMethod]
        public void ElementPropertyMetaData_ItemPropertiesDictionary()
        {
            List<IElementPropertyMetaData> itemProperties = new List<IElementPropertyMetaData>()
            {
                new ElementPropertyMetaData(_flowElement.Object, "prop1", typeof(string), true, "", null),
                new ElementPropertyMetaData(_flowElement.Object, "PROP2", typeof(string), true, "", null)
            };

            ElementPropertyMetaData metaData =
                new ElementPropertyMetaData(_flowElement.Object, "test", typeof(string), true, "", itemProperties);

            var dict = metaData.ItemPropertyDictionary;

            Assert.IsNotNull(dict);
            Assert.IsTrue(dict.ContainsKey("prop1"));
            Assert.IsTrue(dict.ContainsKey("prop2"));
            Assert.IsTrue(dict.ContainsKey("PROP1"));
            Assert.IsTrue(dict.ContainsKey("PROP2"));
        }

    }
}
