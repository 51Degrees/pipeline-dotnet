/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2023 51 Degrees Mobile Experts Limited, Davidson House,
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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.Pipeline.Core.Tests.Data
{
    /// <summary>
    /// Tests for the Evidence class
    /// </summary>
    [TestClass]
    public class EvidenceTests
    {
        /// <summary>
        /// The characters that can be used in evidence keys and values.
        /// </summary>
        private const string ALLOWED_CHARS = 
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz0123456789!@$?_-.";

        /// <summary>
        /// Length of the key in the evidence.
        /// </summary>
        private const int KEY_LENGTH = 200;

        /// <summary>
        /// Length of the value in the evidence.
        /// </summary>
        private const int VALUE_LENGTH = 40;

        /// <summary>
        /// Number of entries to add to the evidence.
        /// </summary>
        private const int EVIDENCE_ITEMS = 100;

        private Mock<ILogger<Evidence>> _logger;

        [TestInitialize]
        public void Init()
        {
            _logger = new Mock<ILogger<Evidence>>();
        }

        [DataTestMethod]
        [DynamicData(nameof(Evidence_Maps_TestCases), DynamicDataSourceType.Method)]
        public void Evidence_Maps(Dictionary<string, object> map)
        {
            var evidence = new Evidence(_logger.Object);
            evidence.PopulateFrom(map);
            foreach(var expected in map)
            {
                var actual = (StringValues)evidence[expected.Key];
                Assert.AreEqual((StringValues)expected.Value, actual);
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(Evidence_Maps_TestCases), DynamicDataSourceType.Method)]
        public void Evidence_Maps_Replace(Dictionary<string, object> map)
        {
            const string newValue = "Value";
            var evidence = new Evidence(_logger.Object);
            evidence.PopulateFrom(map);
            var first = map.First();
            evidence[first.Key] = newValue;
            Assert.AreEqual(evidence[first.Key], newValue);
        }

        /// <summary>
        /// Creates test data maps of evidence key value pairs.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<object[]> Evidence_Maps_TestCases()
        {
            var random = new Random(0);
            var map = new Dictionary<string, object>();
            for(var k = 0; k < EVIDENCE_ITEMS; k++)
            {
                var key = CreateRandomString(random, KEY_LENGTH);
                var value = CreateRandomString(random, VALUE_LENGTH);
                map.Add(key, new StringValues(value));
            }
            yield return new object[] { map };
        }

        /// <summary>
        /// Creates a random string to use as a key or value in the evidence 
        /// test.
        /// </summary>
        /// <param name="random"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        private static string CreateRandomString(Random random, int length)
        {
            char[] chars = new char[length];
            for (int i = 0; i < length; i++)
            {
                chars[i] = ALLOWED_CHARS[random.Next(0, ALLOWED_CHARS.Length)];
            }
            return new string(chars);
        }
    }
}
