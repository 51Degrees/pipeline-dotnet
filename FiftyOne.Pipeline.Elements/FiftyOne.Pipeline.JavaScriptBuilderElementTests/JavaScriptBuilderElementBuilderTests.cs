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

using FiftyOne.Common.TestHelpers;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.JavaScriptBuilder.FlowElement;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace FiftyOne.Pipeline.JavaScript.Tests
{
    [TestClass]
    public class JavaScriptBuilderElementBuilderTests
    {
        private TestLoggerFactory _loggerFactory = new TestLoggerFactory();

        /// <summary>
        /// Test that an invalid object name provided in configuration throws an
        /// exception.
        /// </summary>
        /// <param name="objName"></param>
        [DataTestMethod]
        [DataRow("22j2n2")]
        [DataRow("%2j2n2")]
        [DataRow("+asaaa")]
        [DataRow("\asd23")]
        public void JavaScriptBuilderElement_Builder_SetObjectName_InvalidName(string objName)
        {
            bool thrown = false;
            try
            {
                var engine = new JavaScriptBuilderElementBuilder(_loggerFactory)
                    .SetObjectName(objName)
                    .Build();
            }
            catch (PipelineConfigurationException)
            {
                thrown = true;
            }

            Assert.IsTrue(thrown);
        }

        /// <summary>
        /// Test that no exceptions are thrown if a valid object name is 
        /// provided in the configuration.
        /// </summary>
        /// <param name="objName"></param>
        [DataTestMethod]
        [DataRow("fod")]
        [DataRow("fifty1Degrees")]
        [DataRow("data")]
        [DataRow("data2")]
        public void JavaScriptBuilderElement_Builder_SetObjectName_ValidName(string objName)
        {
            bool thrown = false;
            try
            {
                var engine = new JavaScriptBuilderElementBuilder(_loggerFactory)
                    .SetObjectName(objName)
                    .Build();
            }
            catch (PipelineConfigurationException)
            {
                thrown = true;
            }

            Assert.IsFalse(thrown);
        }

        /// <summary>
        /// Test that a warning is logged if an invalid protocol is provided in
        /// the configuration.
        /// </summary>
        /// <param name="protocol"></param>
        [DataTestMethod]
        [DataRow("htp")]
        [DataRow("htps")]
        [DataRow("ftp")]
        [DataRow("tcp")]
        [ExpectedException(typeof(PipelineConfigurationException))]
        public void JavaScriptBuilderElement_Builder_SetDefaultProtocol_InvalidProtocol(string protocol)
        {
            TestLoggerFactory loggerFactory = new TestLoggerFactory();
            var engine = new JavaScriptBuilderElementBuilder(loggerFactory)
                .SetProtocol(protocol)
                .Build();

            Assert.Fail("Expected exception was not thrown");
        }

        /// <summary>
        /// Test that no warnings are logged if a valid protocol is provided in 
        /// the configuration.
        /// </summary>
        /// <param name="protocol"></param>
        [DataTestMethod]
        [DataRow("http")]
        [DataRow("https")]
        public void JavaScriptBuilderElement_Builder_SetDefaultProtocol_ValidProtocol(string protocol)
        {
            TestLoggerFactory loggerFactory = new TestLoggerFactory();
            var engine = new JavaScriptBuilderElementBuilder(loggerFactory)
                .SetProtocol(protocol)
                .Build();

            var loggers = loggerFactory.Loggers.Where(x => x.WarningEntries.Count() > 0);

            Assert.AreEqual(loggers.Count(), 0);
        }
    }
}
