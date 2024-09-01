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
using FiftyOne.Pipeline.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace FiftyOne.Pipeline.Web.Tests
{

    [TestClass]
    public class WebRequestEvidenceServiceTest
    {
        private class MockCookieCollection : IRequestCookieCollection
        {
            private Dictionary<string, string> _dict;

            public MockCookieCollection(Dictionary<string, string> values)
            {
                _dict = values;
            }

            public string this[string key] => _dict[key];

            public int Count => _dict.Count;

            public ICollection<string> Keys => _dict.Keys;

            public bool ContainsKey(string key)
            {
                return _dict.ContainsKey(key);
            }

            public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
            {
                return _dict.GetEnumerator();
            }

            public bool TryGetValue(string key, out string value)
            {
                return _dict.TryGetValue(key, out value);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return _dict.GetEnumerator();
            }
        }


        private static string EXPECTED_VALUE = "expected";

        private static string REQUIRED_KEY = "requiredkey";
        private static string NOT_REQUIRED_KEY = "notrequiredkey";
        private static string FORM_KEY = "formValueKey";
        private static string FORM_VALUE = "formValueValue";

        private static IPAddress IP = new IPAddress(123321);

        private Mock<IFlowData> _flowData;
        private WebRequestEvidenceService _service;
        private Mock<HttpRequest> _request;

        /// <summary>
        /// Set up the test by creating a new service and initialising any
        /// required objects and keys.
        /// </summary>
        [TestInitialize]
        public void StartUp()
        {
            var values = new Dictionary<string, string>() {
                { REQUIRED_KEY, EXPECTED_VALUE },
                { "null", null } };
            var valuesV = new Dictionary<string, StringValues>() {
                { REQUIRED_KEY, new StringValues(EXPECTED_VALUE) },
                { "null", new StringValues((string)null) } };

            var headers = new HeaderDictionary(valuesV);
            var query = new QueryCollection(valuesV);
            var cookies = new MockCookieCollection(values);
            var formValues = new FormCollection(
                new Dictionary<string, StringValues>() {
                    { FORM_KEY, new StringValues(FORM_VALUE) }
                });

            _request = new Mock<HttpRequest>();
            _flowData = new Mock<IFlowData>();
            LoggerFactory factory = new LoggerFactory();
            _service = new WebRequestEvidenceService(factory.CreateLogger<WebRequestEvidenceService>());

            _request.SetupGet(r => r.Headers).Returns(headers);
            _request.SetupGet(r => r.Cookies).Returns(cookies);
            _request.SetupGet(r => r.Query).Returns(query);
            _request.SetupGet(r => r.Form).Returns(formValues);

            _request.SetupGet(r => r.HttpContext.Connection.RemoteIpAddress)
                .Returns(IP);
            _request.SetupGet(r => r.IsHttps).Returns(true);
            _request.SetupGet(r => r.ContentType)
                .Returns(Shared.Constants.CONTENT_TYPE_FORM[0]);
            _request.SetupGet(r => r.Method)
                .Returns(Shared.Constants.METHOD_POST);
        }

        /// <summary>
        /// Set a single required key in the flow data.
        /// </summary>
        /// <param name="key">required key to set</param>
        private void SetRequiredKey(string key)
        {
            _flowData.SetupGet(f => f.EvidenceKeyFilter)
                .Returns(new EvidenceKeyFilterWhitelist(
                    new List<string>() { key }));
        }

        /// <summary>
        /// Check that a null client IP will not cause any failures 
        /// </summary>
        [TestMethod]
        public void WebRequestEvidenceService_IpNull()
        {
            SetRequiredKey(Core.Constants.EVIDENCE_CLIENTIP_KEY);
            _request.SetupGet(r => r.HttpContext.Connection.RemoteIpAddress)
                .Returns((IPAddress)null);

            _service.AddEvidenceFromRequest(_flowData.Object, _request.Object);
            _flowData.Verify(f => f.AddEvidence(
                Core.Constants.EVIDENCE_CLIENTIP_KEY, It.IsAny<object>()),
                Times.Never);
        }

        /// <summary>
        /// Check that the client IP value is set in the evidence as expected. 
        /// </summary>
        [TestMethod]
        public void WebRequestEvidenceService_IpVerify()
        {
            SetRequiredKey(Core.Constants.EVIDENCE_CLIENTIP_KEY);

            _service.AddEvidenceFromRequest(_flowData.Object, _request.Object);
            _flowData.Verify(f => f.AddEvidence(
                Core.Constants.EVIDENCE_CLIENTIP_KEY, IP.ToString()),
                Times.Once);
        }

        /// <summary>
        /// Check thhat the request protocol is always added to the
        /// evidence.
        /// </summary>
        [TestMethod]

        public void WebRequestEvidenceService_ContainsProtocol()
        {
            SetRequiredKey(Core.Constants.EVIDENCE_PROTOCOL);
            _service.AddEvidenceFromRequest(_flowData.Object, _request.Object);
            _flowData.Verify(f => f.AddEvidence(
                It.IsAny<string>(), It.IsAny<string>()),
                Times.Once);
            _flowData.Verify(f => f.AddEvidence(
                Core.Constants.EVIDENCE_PROTOCOL, "https"),
                Times.Once);
        }

        /// <summary>
        /// Set the required key in the flow data to "prefix.requiredkey" where
        /// prefix is supplied, and the required key is a constant which exists
        /// in headers, cookies and query parameters.
        /// 
        /// Then test that the evidence for the required key (and only the
        /// required key) is added to the flow data evidence collection.
        /// </summary>
        /// <param name="prefix">prefix to test</param>
        private void CheckRequired(string prefix, string key, string expectedValue)
        {
            SetRequiredKey(prefix + "." + key);
            _service.AddEvidenceFromRequest(_flowData.Object, _request.Object);
            _flowData.Verify(f => f.AddEvidence(
                It.IsAny<string>(), It.IsAny<string>()),
                Times.Once);
            _flowData.Verify(f => f.AddEvidence(
                prefix + "." + key, expectedValue),
                Times.Once);
        }

        /// <summary>
        /// Set the required key in the flow data to "prefix.notrequiredkey"
        /// where prefix is supplied, and the not required key is a constant
        /// which does not exist anywhere in the request.
        /// 
        /// Then test that no evidence is added to the flow data evidence
        /// collection.
        /// </summary>
        /// <param name="prefix"></param>
        private void CheckNotRequired(string prefix)
        {
            SetRequiredKey(prefix + "." + NOT_REQUIRED_KEY);
            _service.AddEvidenceFromRequest(_flowData.Object, _request.Object);
            _flowData.Verify(f => f.AddEvidence(
                It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        /// <summary>
        /// Test that a client IP address required by a flow data is correctly
        /// added to its evidence collection.
        /// </summary>
        [TestMethod]
        public void WebRequestEvidenceService_AddClientIp()
        {
            var key = "server.client-ip";
            SetRequiredKey(key);
            _service.AddEvidenceFromRequest(_flowData.Object, _request.Object);
            _flowData.Verify(f => f.AddEvidence(
                It.IsAny<string>(), It.IsAny<string>()),
                Times.Once);
            _flowData.Verify(f => f.AddEvidence(key, IP.ToString()), Times.Once);
        }

        /// <summary>
        /// Test that an existing header required by a flow data is correctly
        /// added to its evidence collection.
        /// </summary>
        [TestMethod]
        public void WebRequestEvidenceService_AddRequiredHeader()
        {
            CheckRequired("header", REQUIRED_KEY, EXPECTED_VALUE);
        }

        /// <summary>
        /// Test that a non-existent header required by a flow data is not
        /// added to its evidence collection (and neither is anything else).
        /// </summary>
        [TestMethod]
        public void WebRequestEvidenceService_AddNotRequiredHeader()
        {
            CheckNotRequired("header");
        }

        /// <summary>
        /// Test that an existing cookie required by a flow data is correctly
        /// added to its evidence collection.
        /// </summary>
        [TestMethod]
        public void WebRequestEvidenceService_AddRequiredCookie()
        {
            CheckRequired("cookie", REQUIRED_KEY, EXPECTED_VALUE);
        }

        /// <summary>
        /// Test that a non-existent cookie required by a flow data is not
        /// added to its evidence collection (and neither is anything else).
        /// </summary>
        [TestMethod]
        public void WebRequestEvidenceService_AddNotRequiredCookie()
        {
            CheckNotRequired("cookie");
        }

        /// <summary>
        /// Test that an existing query parameter required by a flow data is
        /// correctly added to its evidence collection.
        /// </summary>
        [TestMethod]
        public void WebRequestEvidenceService_AddRequiredParam()
        {
            CheckRequired("query", REQUIRED_KEY, EXPECTED_VALUE);
        }

        /// <summary>
        /// Test that a non-existent query parameter  required by a flow data
        /// is not added to its evidence collection (and neither is anything
        /// else).
        /// </summary>
        [TestMethod]
        public void WebRequestEvidenceService_AddNotRequiredParam()
        {
            CheckNotRequired("query");
        }

        public static IEnumerable<object[]> GetContentTypes
        {
            get
            {
                foreach (var entry in Shared.Constants.CONTENT_TYPE_FORM)
                {
                    yield return new object[] { entry };
                }
            }
        }
        /// <summary>
        /// Test that evidence is added from form parameters for each 
        /// valid content type.
        /// </summary>
        [DataTestMethod]
        [DynamicData(nameof(GetContentTypes))]
        public void WebRequestEvidenceService_AddFormParam(string contentType)
        {
            _request.SetupGet(r => r.ContentType).Returns(contentType);
            CheckRequired("query", FORM_KEY, FORM_VALUE);
        }

        /// <summary>
        /// Test that form parameters will not be read if type is not post
        /// </summary>
        [TestMethod]
        public void WebRequestEvidenceService_AddFormParam_NotPost()
        {
            _request.SetupGet(r => r.Method).Returns("TEST");
            _request.SetupGet(r => r.Form).Throws(new System.Exception(
                "This test should not be trying to access form values"));
            CheckNotRequired("query");
        }

        /// <summary>
        /// Test that form parameters will not be read if content type
        /// is not url encoded form
        /// </summary>
        [TestMethod]
        public void WebRequestEvidenceService_AddFormParam_NotForm()
        {
            _request.SetupGet(r => r.ContentType).Returns("TEST");
            _request.SetupGet(r => r.Form).Throws(new System.Exception(
                "This test should not be trying to access form values"));
            CheckNotRequired("query");
        }

        [TestMethod]
        public void WebRequestEvidenceService_InvalidForm()
        {
            _request.SetupGet(r => r.Form).Throws(new InvalidDataException());
            // Check that this does not throw an exception.
            _service.AddEvidenceFromRequest(_flowData.Object, _request.Object);
            
        }

        /// <summary>
        /// Set the required key in the flow data to "prefix.null" where prefix
        /// if provided, and null is a null value which exists in headers,
        /// cookies and query parameters.
        /// 
        /// Then test that the evidence for the required key (and only the
        /// required key) is added to the flow data evidence collection as an
        /// empty string, and that a null reference exception does not occur.
        /// </summary>
        /// <param name="prefix">prefix to test</param>
        private void CheckNullValue(string prefix)
        {
            SetRequiredKey(prefix + ".null");
            _service.AddEvidenceFromRequest(_flowData.Object, _request.Object);
            _flowData.Verify(f => f.AddEvidence(
                It.IsAny<string>(), It.IsAny<string>()),
                Times.Once);
            _flowData.Verify(f => f.AddEvidence(
                prefix + ".null", ""),
                Times.Once);
        }

        /// <summary>
        /// Test that a null header value is added to the flow data evidence
        /// collection as an empty string, and that a null reference exception
        /// does not occur.
        /// </summary>
        [TestMethod]
        public void WebRequestEvidenceService_NullHeaderValue()
        {
            CheckNullValue("header");
        }

        /// <summary>
        /// Test that a null cookie value is added to the flow data evidence
        /// collection as an empty string, and that a null reference exception
        /// does not occur.
        /// </summary>
        [TestMethod]
        public void WebRequestEvidenceService_NullCookieValue()
        {
            CheckNullValue("cookie");
        }
        /// <summary>
        /// Test that a null query parameter value is added to the flow data
        /// evidence collection as an empty string, and that a null reference
        /// exception does not occur.
        /// </summary>
        [TestMethod]
        public void WebRequestEvidenceService_NullParamValue()
        {
            CheckNullValue("query");
        }
    }
}
