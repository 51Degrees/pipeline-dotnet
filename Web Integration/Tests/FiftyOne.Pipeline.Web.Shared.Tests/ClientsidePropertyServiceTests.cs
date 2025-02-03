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
using FiftyOne.Pipeline.JavaScriptBuilder.Data;
using FiftyOne.Pipeline.JavaScriptBuilder.FlowElement;
using FiftyOne.Pipeline.JsonBuilder.Data;
using FiftyOne.Pipeline.JsonBuilder.FlowElement;
using FiftyOne.Pipeline.Web.Shared.Adapters;
using FiftyOne.Pipeline.Web.Shared.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Web.Shared.Tests
{
    [TestClass]
    public class ClientsidePropertyServiceTests
    {
        private ClientsidePropertyService _service;
        private TestLogger<ClientsidePropertyService> _logger;

        private Mock<IContextAdapter> _context;
        private Mock<IRequestAdapter> _request;
        private Mock<IResponseAdapter> _response;

        private Mock<IFlowData> _flowData;
        private Mock<IPipeline> _pipeline;
        private Mock<IJsonBuilderElementData> _jsonData;
        private Mock<IJavaScriptBuilderElementData> _jsData;

        private DataKey _defaultDataKey;

        private const string JS_CONTENT = @"JAVASCRIPT CONTENT";
        private const string JSON_CONTENT = @"JSON CONTENT";
        private const string JS_CONTENT_TYPE = @"application/x-javascript";
        private const string JSON_CONTENT_TYPE = @"application/json";

        private string _writtenContent = "";

        [TestInitialize]
        public void Init()
        {
            // Create the mock HTTP context objects.
            _context = new Mock<IContextAdapter>();
            _request = new Mock<IRequestAdapter>();
            _response = new Mock<IResponseAdapter>();
            _context.Setup(c => c.Request).Returns(_request.Object);
            _context.Setup(c => c.Response).Returns(_response.Object);
            _response.Setup(r => r.Write(It.IsAny<string>()))
                .Callback((string content) =>
            {
                _writtenContent = content;
            });
            // Configure properties on the response to work normally.
            _response.SetupAllProperties();

            // Create the mock pipeline and flow data.
            _pipeline = new Mock<IPipeline>();
            _flowData = new Mock<IFlowData>();
            _flowData.Setup(d => d.Pipeline).Returns(_pipeline.Object);

            // Configure the mocks to return the data values we want.
            var js = new JavaScriptBuilderElement(null, null, null, null, false, false);
            _pipeline.Setup(p => p.GetElement<JavaScriptBuilderElement>()).Returns(js);
            _jsData = new Mock<IJavaScriptBuilderElementData>();
            _flowData.Setup(d => d.GetFromElement(js)).Returns(_jsData.Object);

            var json = new JsonBuilderElement(null, new List<JsonConverter>(), null);
            _pipeline.Setup(p => p.GetElement<JsonBuilderElement>()).Returns(json);
            _jsonData = new Mock<IJsonBuilderElementData>();
            _flowData.Setup(d => d.GetFromElement(json)).Returns(_jsonData.Object);
        }

        /// <summary>
        /// Verify the response is as expected for a JavaScript request
        /// </summary>
        [TestMethod]
        public void Javascript_Success()
        {
            Configure();

            _service.ServeJavascript(_context.Object, _flowData.Object);

            ValidateResponse(JS_CONTENT, 
                JS_CONTENT.Length.ToString(), 
                JS_CONTENT_TYPE, 200, 
                _defaultDataKey.GetHashCode().ToString(),
                "", 
                null);
        }

        /// <summary>
        /// Verify the response is as expected for a JavaScript request
        /// where the  'If-None-Match' header matches the current 
        /// flow data.
        /// In this case a 304 should be returned
        /// </summary>
        [TestMethod]
        public void Javascript_NotModified()
        {
            Configure();
            // Configure the 'If-None-Match' header in the request to have
            // the same value that will be returned by the data key.
            _request.Setup(r => r.GetHeaderValue("If-None-Match"))
                .Returns(_defaultDataKey.GetHashCode().ToString());

            _service.ServeJavascript(_context.Object, _flowData.Object);

            ValidateResponse(null, null, null, 304, null, null, null);
        }

        /// <summary>
        /// Verify that the Vary header is set as expected for a variety of scenarios
        /// </summary>
        [DataTestMethod]
        [DynamicData("VaryHeader_DATA", DynamicDataSourceType.Method)]
        public void JavaScript_VaryHeader(List<string> evidenceHeaders1, 
            List<string> evidenceHeaders2, string expectedVary)
        {
            Configure(ConfigureElements(
                evidenceHeaders1,
                evidenceHeaders2));

            _service.ServeJavascript(_context.Object, _flowData.Object);

            ValidateResponse(JS_CONTENT,
                JS_CONTENT.Length.ToString(),
                JS_CONTENT_TYPE, 200,
                _defaultDataKey.GetHashCode().ToString(),
                expectedVary, 
                null);
        }

        /// <summary>
        /// Verify the response is as expected for a Json request
        /// </summary>
        [TestMethod]
        public void Json_Success()
        {
            Configure();

            _service.ServeJson(_context.Object, _flowData.Object);

            ValidateResponse(JSON_CONTENT,
                JSON_CONTENT.Length.ToString(),
                JSON_CONTENT_TYPE, 200,
                _defaultDataKey.GetHashCode().ToString(),
                "",
                null);
        }

        /// <summary>
        /// Verify the response is as expected for a JavaScript request
        /// where the 'If-None-Match' header matches the current flow data.
        /// In this case a 304 should be returned
        /// </summary>
        [TestMethod]
        public void Json_NotModified()
        {
            Configure();
            // Configure the 'If-None-Match' header in the request to have
            // the same value that will be returned by the data key.
            _request.Setup(r => r.GetHeaderValue("If-None-Match"))
                .Returns(_defaultDataKey.GetHashCode().ToString());

            _service.ServeJson(_context.Object, _flowData.Object);

            ValidateResponse(null, null, null, 304, null, null, null);
        }

        /// <summary>
        /// Verify that the Vary header is set as expected for a 
        /// variety of scenarios
        /// </summary>
        [DataTestMethod]
        [DynamicData("VaryHeader_DATA", DynamicDataSourceType.Method)]
        public void Json_VaryHeader(List<string> evidenceHeaders1, 
            List<string> evidenceHeaders2, string expectedVary)
        {
            Configure(ConfigureElements(
                evidenceHeaders1,
                evidenceHeaders2));

            _service.ServeJson(_context.Object, _flowData.Object);

            ValidateResponse(JSON_CONTENT,
                JSON_CONTENT.Length.ToString(),
                JSON_CONTENT_TYPE, 200,
                _defaultDataKey.GetHashCode().ToString(),
                expectedVary,
                null);
        }

        /// <summary>
        /// If the Origin header is set to none null then the Access-Control-Allow-Origin header should be present in
        /// the response with the same value as the request.
        /// </summary>
        [TestMethod]
        public void Json_Cors()
        {
            const string ORIGIN_VALUE = "https://localhost";
            Configure();
            _request.Setup(r => r.GetHeaderValue("Origin"))
                .Returns(ORIGIN_VALUE);
            _service.ServeJson(_context.Object, _flowData.Object);
            ValidateResponse(JSON_CONTENT,
                JSON_CONTENT.Length.ToString(),
                JSON_CONTENT_TYPE, 200,
                _defaultDataKey.GetHashCode().ToString(),
                "",
                ORIGIN_VALUE);
        }

        /// <summary>
        /// If the Origin header is set to  null then the Access-Control-Allow-Origin header should not be present in
        /// the response.
        /// </summary>
        [TestMethod]
        public void Json_Cors_Null()
        {
            const string ORIGIN_VALUE = "null";
            Configure();
            _request.Setup(r => r.GetHeaderValue("Origin"))
                .Returns(ORIGIN_VALUE);
            _service.ServeJson(_context.Object, _flowData.Object);
            ValidateResponse(JSON_CONTENT,
                JSON_CONTENT.Length.ToString(),
                JSON_CONTENT_TYPE, 200,
                _defaultDataKey.GetHashCode().ToString(),
                "",
            null);
        }

        /// <summary>
        /// If the Origin header is set to none null then the Access-Control-Allow-Origin header should be present in
        /// the response with the same value as the request.
        /// </summary>
        [TestMethod]
        public void Javascript_Cors()
        {
            const string ORIGIN_VALUE = "https://localhost";
            Configure();
            _request.Setup(r => r.GetHeaderValue("Origin"))
                .Returns(ORIGIN_VALUE);
            _service.ServeJavascript(_context.Object, _flowData.Object);
            ValidateResponse(JS_CONTENT,
                JS_CONTENT.Length.ToString(),
                JS_CONTENT_TYPE, 200,
                _defaultDataKey.GetHashCode().ToString(),
                "",
                ORIGIN_VALUE);
        }

        /// <summary>
        /// If the Origin header is set to  null then the Access-Control-Allow-Origin header should not be present in
        /// the response.
        /// </summary>
        [TestMethod]
        public void Javascript_Cors_Null()
        {
            const string ORIGIN_VALUE = "null";
            Configure();
            _request.Setup(r => r.GetHeaderValue("Origin"))
                .Returns(ORIGIN_VALUE);
            _service.ServeJavascript(_context.Object, _flowData.Object);
            ValidateResponse(JS_CONTENT,
                JS_CONTENT.Length.ToString(),
                JS_CONTENT_TYPE, 200,
                _defaultDataKey.GetHashCode().ToString(),
                "",
                null);
        }

        /// <summary>
        /// Generate the test input data for the Vary header tests
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<object[]> VaryHeader_DATA()
        {
            var EVIDENCE_NONE = new List<string>() { };
            var EVIDENCE_UA = new List<string>() { "Header.User-Agent" };
            var EVIDENCE_CHUA = new List<string>() { "Header.Sec-ch-ua" };
            var EVIDENCE_UA_AND_CHUA = new List<string>() { "Header.User-Agent", "Header.Sec-ch-ua" };
            var EVIDENCE_PSEUDO = new List<string>() { "Header.sec-ch-uasec-ch-ua-full-version" };
            var EVIDENCE_UA_AND_PSEUDO = new List<string>() { "Header.User-Agent", "Header.sec-ch-uasec-ch-ua-full-version" };

            // Tests with a single flow element
            yield return new object[] { EVIDENCE_NONE, null, null };
            yield return new object[] { EVIDENCE_UA, null, "User-Agent" };
            yield return new object[] { EVIDENCE_UA_AND_CHUA, null, "User-Agent,Sec-ch-ua" };
            yield return new object[] { EVIDENCE_PSEUDO, null, null };
            yield return new object[] { EVIDENCE_UA_AND_PSEUDO, null, "User-Agent" };

            // Tests with multiple flow elements
            yield return new object[] { EVIDENCE_NONE, EVIDENCE_UA, "User-Agent" };
            yield return new object[] { EVIDENCE_UA, EVIDENCE_UA, "User-Agent" };
            yield return new object[] { EVIDENCE_UA, EVIDENCE_CHUA, "User-Agent,Sec-ch-ua" };
            yield return new object[] { EVIDENCE_UA_AND_CHUA, EVIDENCE_UA, "User-Agent,Sec-ch-ua" };
            yield return new object[] { EVIDENCE_PSEUDO, EVIDENCE_UA, "User-Agent" };
        }


        private List<IFlowElement> ConfigureElements(params List<string>[] evidenceHeaders)
        {
            var flowElements = new List<IFlowElement>();

            foreach(var entry in evidenceHeaders)
            {
                if (entry != null)
                {
                    Mock<IFlowElement> mockElement = new Mock<IFlowElement>();
                    IEvidenceKeyFilter filter = new EvidenceKeyFilterWhitelist(entry);
                    mockElement.Setup(e => e.EvidenceKeyFilter).Returns(filter);

                    flowElements.Add(mockElement.Object);
                }
            }

            return flowElements;
        }


        /// <summary>
        /// Verify that the response is as expected
        /// </summary>
        /// <param name="expectedContent"></param>
        /// <param name="contentLength"></param>
        /// <param name="contentType"></param>
        /// <param name="expectedStatusCode"></param>
        /// <param name="expectedETag"></param>
        /// <param name="expectedVary"></param>
        /// <param name="expectedOrigin"></param>
        private void ValidateResponse(
            string expectedContent,
            string contentLength, 
            string contentType,
            int expectedStatusCode,
            string expectedETag,
            string expectedVary,
            string expectedOrigin)
        {
            if (expectedContent != null)
            {
                _response.Verify(r => r.Write(expectedContent),
                    $"The expected value ({expectedContent}) was not written " +
                    $"to the response ({_writtenContent}).");
            }
            else
            {
                _response.Verify(r => r.Write(It.IsAny<string>()), Times.Never);
            }

            Assert.AreEqual(expectedStatusCode, _response.Object.StatusCode);

            if(contentType != null) 
            {             
                _response.Verify(r => r.SetHeader("Content-Type", contentType));
            }
            else
            {
                _response.Verify(r => r.SetHeader("Content-Type", It.IsAny<string>()), Times.Never);
            }
            if (contentLength != null)
            {
                _response.Verify(r => r.SetHeader("Content-Length", contentLength));
            }
            else
            {
                _response.Verify(r => r.SetHeader("Content-Length", It.IsAny<string>()), Times.Never);
            }
            if (expectedStatusCode == 200)
            {
                _response.Verify(r => r.SetHeader("Cache-Control", $"private,max-age=1800"));
            }
            else
            {
                _response.Verify(r => r.SetHeader("Cache-Control", It.IsAny<string>()), Times.Never);
            }
            if (string.IsNullOrEmpty(expectedVary) == false)
            {
                _response.Verify(r => r.SetHeader("Vary", expectedVary));
            }
            else
            {
                _response.Verify(r => r.SetHeader("Vary", It.IsAny<string>()), Times.Never);
            }
            if (expectedETag != null)
            {
                _response.Verify(r => r.SetHeader("ETag", expectedETag));
            }
            else
            {
                _response.Verify(r => r.SetHeader("Vary", It.IsAny<string>()), Times.Never);
            }
            if (expectedOrigin != null)
            {
                _response.Verify(r => r.SetHeader("Access-Control-Allow-Origin", expectedOrigin));
            }
            else
            {
                _response.Verify(r => r.SetHeader("Access-Control-Allow-Origin", It.IsAny<string>()), Times.Never);
            }
        }

        private void Configure(List<IFlowElement> flowElements = null)
        {
            // Configure pipeline to return an empty list of flow elements
            // by default.
            // This is used to determine which evidence values can impact 
            // json or javascript results and is not relevant for most tests.
            if (flowElements == null)
            {
                flowElements = new List<IFlowElement>();
            }
            _pipeline.Setup(p => p.FlowElements)
                .Returns(flowElements);
            _pipeline.Setup(p => p.EvidenceKeyFilter)
                .Returns(new EvidenceKeyFilterWhitelist(new List<string>()));
            // Configure the key for this flow data to contain a fake value
            // that we can use to test the cached response handling.
            _defaultDataKey = new DataKeyBuilder().Add(1, "test", "value").Build();
            _flowData.Setup(f => f.GenerateKey(It.IsAny<IEvidenceKeyFilter>()))
                .Returns(_defaultDataKey);

            // Set the flow element data objects to return placeholder 
            // text that we can check for.
            _jsData.Setup(j => j.JavaScript).Returns(JS_CONTENT);
            _jsonData.Setup(j => j.Json).Returns(JSON_CONTENT);

            CreateService();
        }

        private void CreateService()
        {
            _logger = new TestLogger<ClientsidePropertyService>();
            _service = new ClientsidePropertyService(_pipeline.Object, _logger);
        }
    }
}
