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
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.JavaScriptBuilder.Data;
using FiftyOne.Pipeline.JavaScriptBuilder.FlowElement;
using FiftyOne.Pipeline.JsonBuilder.FlowElement;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FiftyOne.Pipeline.JavaScriptBuilderElementTests
{
    [TestClass]
    public class CookiesTests
    {
        private TestLoggerFactory _loggerFactory = new TestLoggerFactory();

        /// <summary>
        /// Test element data that contains only javascript.
        /// </summary>
        private class CookieData : ElementDataBase
        {
            public CookieData(ILogger<ElementDataBase> logger, IPipeline pipeline) : base(logger, pipeline)
            {
            }

            public string JavaScript => base.GetAs<string>("javascript");
        }

        /// <summary>
        /// Test element that just adds some javascript to set a cookie.
        /// </summary>
        private class CookieElement : FlowElementBase<CookieData, ElementPropertyMetaData>
        {
            private readonly ILoggerFactory _loggerFactory;

            public CookieElement(ILoggerFactory loggerFactory)
                : base(loggerFactory.CreateLogger<FlowElementBase<CookieData, ElementPropertyMetaData>>())
            {
                _loggerFactory = loggerFactory;
            }

            public override string ElementDataKey => "cookie";

            public override IEvidenceKeyFilter EvidenceKeyFilter =>
                new EvidenceKeyFilterWhitelist(new List<string>());

            public override IList<ElementPropertyMetaData> Properties =>
                new List<ElementPropertyMetaData>()
                {
                    new ElementPropertyMetaData(
                        this,
                        "javascript",
                        typeof(string),
                        true)
                };

            protected override void ProcessInternal(IFlowData data)
            {
                var result = new CookieData(_loggerFactory.CreateLogger<CookieData>(), data.Pipeline);
                result["javascript"] = "document.cookie =  \"some cookie value\"";
                data.GetOrAdd(ElementDataKey, p => result);
            }

            protected override void ManagedResourcesCleanup()
            {
            }

            protected override void UnmanagedResourcesCleanup()
            {
            }
        }

        /// <summary>
        /// Test various configurations for enabling cookies to verify
        /// that cookies are/aren't written for each configuration.
        /// 
        /// The source JavaScript contains code to set a cookie. The JSBuilder
        /// element should replace this if the config says that cookies are not
        /// enabled.
        /// </summary>
        /// <param name="enableInConfig">
        /// True if cookies are enabled in the element configuration.
        /// </param>
        /// <param name="enableInEvidence">
        /// True if cookies are enabled in the evidence.
        /// </param>
        /// <param name="expectCookie">
        /// True if the test should expect cookies to be enabled for this configuration.
        /// </param>
        [DataRow(false, false, false)]
        [DataRow(true, false, false)]
        [DataRow(false, true, true)]
        [DataRow(true, true, true)]
        [DataTestMethod]
        public void TestJavaScriptCookies(bool enableInConfig, bool enableInEvidence, bool expectCookie)
        {
            // Arrange
            var cookieElement = new CookieElement(_loggerFactory);
            var sequenceElement = new SequenceElementBuilder(_loggerFactory)
                .Build();
            var jsonElement = new JsonBuilderElementBuilder(_loggerFactory)
                .Build();
            var jsElement = new JavaScriptBuilderElementBuilder(_loggerFactory)
                .SetEnableCookies(enableInConfig)
                .Build();
            var pipeline = new PipelineBuilder(_loggerFactory)
                .AddFlowElement(cookieElement)
                .AddFlowElement(sequenceElement)
                .AddFlowElement(jsonElement)
                .AddFlowElement(jsElement)
                .Build();

            // Act
            string javaScript = null;
            using (var flowData = pipeline.CreateFlowData())
            {
                flowData.AddEvidence(
                    JavaScriptBuilder.Constants.EVIDENCE_ENABLE_COOKIES,
                    enableInEvidence.ToString());
                flowData.Process();
                javaScript = flowData.Get<IJavaScriptBuilderElementData>().JavaScript;
            }

            // Assert
            var cookieRegex = new Regex("document\\.cookie");
            var matches = cookieRegex.Matches(javaScript);
            if (expectCookie)
            {
                Assert.AreEqual(
                    2,
                    matches.Count,
                    "The original script to set cookies should not have been replaced.");
            }
            else
            {
                Assert.AreEqual(
                    1,
                    matches.Count,
                    "The original script to set cookies should have been replaced.");
            }
        }
    }
}
