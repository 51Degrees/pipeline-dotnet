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
using FiftyOne.Pipeline.JsonBuilder.Converters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.JsonBuilderElementTests
{
    [TestClass]
    public class AspectPropertyValueConverterTests
    {
        private static AspectPropertyValueConverter _converter = new AspectPropertyValueConverter();

        [TestMethod]
        public void AspectPropertyValueConverter_WriteString()
        {
            AspectPropertyValue<string> value = new AspectPropertyValue<string>("testing");

            var result = JsonConvert.SerializeObject(value, _converter);

            string expected = @"""testing""";
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void AspectPropertyValueConverter_WriteString_NoValue()
        {
            AspectPropertyValue<string> value = new AspectPropertyValue<string>();

            var result = JsonConvert.SerializeObject(value, _converter);

            Assert.AreEqual($"null", result);
        }

        [TestMethod]
        public void AspectPropertyValueConverter_WriteInt()
        {
            AspectPropertyValue<int> value = new AspectPropertyValue<int>(3);

            var result = JsonConvert.SerializeObject(value, _converter);

            Assert.AreEqual("3", result);
        }

        [TestMethod]
        public void AspectPropertyValueConverter_WriteInt_NoValue()
        {
            AspectPropertyValue<int> value = new AspectPropertyValue<int>();

            var result = JsonConvert.SerializeObject(value, _converter);

            Assert.AreEqual($"null", result);
        }

        [TestMethod]
        public void AspectPropertyValueConverter_WriteObjectInDictionary()
        {
            AspectPropertyValue<string> value1 = new AspectPropertyValue<string>("testing");
            AspectPropertyValue<int> value2 = new AspectPropertyValue<int>(3);
            AspectPropertyValue<string> value3 = new AspectPropertyValue<string>();
            Dictionary<string, object> data = new Dictionary<string, object>()
            {
                { "v1", value1 },
                { "v2", value2 },
                { "v3", value3 }
            };

            var result = JsonConvert.SerializeObject(data, _converter);

            string expected = @"{""v1"":""testing"",""v2"":3,""v3"":null}";
            Assert.AreEqual(expected, result);
        }
    }
}
