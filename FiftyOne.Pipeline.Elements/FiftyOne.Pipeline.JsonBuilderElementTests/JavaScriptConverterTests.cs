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

using FiftyOne.Pipeline.Core.Data.Types;
using FiftyOne.Pipeline.JsonBuilder.Converters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.JsonBuilderElementTests
{

    [TestClass]
    public class JavaScriptConverterTests
    {
        private static JavaScriptConverter _converter = new JavaScriptConverter();

        [TestMethod]
        public void JavaScriptConverter_WriteObject()
        {
            JavaScript js = new JavaScript("testing");

            var result = JsonConvert.SerializeObject(js, _converter);

            string expected = @"""testing""";
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void JavaScriptConverter_WriteObjectInDictionary()
        {
            JavaScript js1 = new JavaScript("item 1");
            JavaScript js2 = new JavaScript("item 2");
            Dictionary<string, object> data = new Dictionary<string, object>()
            {
                { "js1", js1 },
                { "js2", js2 }
            };

            var result = JsonConvert.SerializeObject(data, _converter);

            string expected = @"{""js1"":""item 1"",""js2"":""item 2""}";
            Assert.AreEqual(expected, result);
        }
    }
}
