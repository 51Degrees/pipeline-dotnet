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

using FiftyOne.Pipeline.CloudRequestEngine.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.Pipeline.CloudRequestEngine.Tests
{
    [TestClass]
    public class CloudJsonConverterTests
    {
        private static JsonConverter[] JSON_CONVERTERS = new JsonConverter[]
        {
            new CloudJsonConverter()
        };

        [TestMethod]
        public void Test_Simple()
        {
            string json = @"{ ""booleanProperty"": ""True"", ""stringProperty"": ""ABC"", ""listProperty"": [ ""Item1"", ""Item2"" ] }";

            var output = JsonConvert.DeserializeObject<Dictionary<string, object>>(json,
                new JsonSerializerSettings()
                {
                    Converters = JSON_CONVERTERS,
                });

            Assert.AreEqual(3, output.Count);
            Assert.IsTrue(output.Any(i => i.Key == "booleanProperty"));
            Assert.IsTrue(output.Any(i => i.Key == "stringProperty"));
            Assert.IsTrue(output.Any(i => i.Key == "listProperty"));
            Assert.AreEqual("True", output["booleanProperty"]);
            Assert.AreEqual("ABC", output["stringProperty"]);
            Assert.IsInstanceOfType(output["listProperty"], typeof(IEnumerable<string>));
            var list = output["listProperty"] as IEnumerable<string>;
            Assert.IsTrue(list.Any(i => i == "Item1"));
            Assert.IsTrue(list.Any(i => i == "Item2"));
        }
    }
}
