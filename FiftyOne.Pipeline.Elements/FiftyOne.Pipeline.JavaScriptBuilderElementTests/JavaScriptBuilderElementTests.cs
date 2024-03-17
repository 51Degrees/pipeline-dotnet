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
using FiftyOne.Pipeline.Core.TypedMap;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Linq;
using FiftyOne.Pipeline.JsonBuilder.Data;
using FiftyOne.Pipeline.JavaScriptBuilder.Data;
using FiftyOne.Pipeline.JavaScriptBuilder.FlowElement;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines;
using FiftyOne.Pipeline.Core.Exceptions;
using OpenQA.Selenium;
using FiftyOne.Pipeline.Engines.TestHelpers;
using System.Net;

namespace FiftyOne.Pipeline.JavaScript.Tests
{
    /// <summary>
    /// These tests check the various functions of the generated JavaScript 
    /// include using WebDrivers to simulate a browser environment.
    /// </summary>
    [TestClass]
    public class JavaScriptBuilderElementTests: JavaScriptBuilderElementTestsBase
    {
        private JavaScriptBuilderElement _javaScriptBuilderElement;

        private HttpClient httpClient;
        private CancellationTokenSource clientServerTokenSource;
        private HttpListener _clientServer;

        /// <summary>
        /// Initialise the test.
        /// </summary>
        [TestInitialize]
        public override async Task Init()
        {
            httpClient = new HttpClient();

            await base.Init();

            // Start the client server
            clientServerTokenSource = new CancellationTokenSource();
            var token = clientServerTokenSource.Token;
            // We need the context of a page to be able to test the JavaScript 
            // correctly so create a simple HttpListener which serves some 
            // static HTML.
            _clientServer = TestHttpListener.SimpleListener(ClientServerUrl, token);

            // Navigate to the client site.
            Driver.Navigate().GoToUrl(ClientServerUrl);
        }

        /// <summary>
        /// This method tests the accessors functionality of the JavaScript 
        /// include.
        /// 
        /// For the supplied properties values, check that these properties can 
        /// be accessed via the 'fod' object in the JavaScript include.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="property"></param>
        /// <param name="value"></param>
        [DataTestMethod]
        [DataRow(false, "device", "ismobile", true)]
        [DataRow(false, "device", "ismobile", false)]
        [DataRow(false, "device", "browsername", "Chrome")]
        [DataRow(false, "device", "browsername", null)]
        [DataRow(true, "device", "ismobile", true)]
        [DataRow(true, "device", "ismobile", false)]
        [DataRow(true, "device", "browsername", "Chrome")]
        [DataRow(true, "device", "browsername", null)]
        public void JavaScriptBuilderElement_JavaScript(bool minify, string key, string property, object value)
        {
            _javaScriptBuilderElement =
                new JavaScriptBuilderElementBuilder(LoggerFactory)
                .SetMinify(minify)
                .Build();

            dynamic json = new JObject();

            if (value == null)
            {
                json[key] = new JObject(new JProperty(property, value), new JProperty(property + "nullreason", "No value set"));
                value = "No value set";
            } else
            {
                json[key] = new JObject(new JProperty(property, value));
            }

            var flowData = new Mock<IFlowData>();
            Configure(flowData,  json);

            IJavaScriptBuilderElementData result = null;
            flowData.Setup(d => d.GetOrAdd(
                It.IsAny<ITypedKey<IJavaScriptBuilderElementData>>(),
                It.IsAny<Func<IPipeline, IJavaScriptBuilderElementData>>()))
                .Returns<ITypedKey<IJavaScriptBuilderElementData>, Func<IPipeline, IJavaScriptBuilderElementData>>((k, f) =>
                {
                    result = f(flowData.Object.Pipeline);
                    return result;
                });

            _javaScriptBuilderElement.Process(flowData.Object);

            Assert.IsTrue(IsValidFodObject(result.JavaScript, key, property, value));
        }

        /// <summary>
        /// Verify that the JavaScript contains the Session ID and Sequence
        /// values.
        /// </summary>
        [TestMethod]
        public void JavaScriptBuilder_VerifySession()
        {
            _javaScriptBuilderElement =
                new JavaScriptBuilderElementBuilder(LoggerFactory).SetMinify(false).Build();
            var flowData = new Mock<IFlowData>();
            Configure(flowData);

            IJavaScriptBuilderElementData result = null;
            flowData.Setup(d => d.GetOrAdd(
                It.IsAny<ITypedKey<IJavaScriptBuilderElementData>>(),
                It.IsAny<Func<IPipeline, IJavaScriptBuilderElementData>>()))
                .Returns<ITypedKey<IJavaScriptBuilderElementData>, Func<IPipeline, IJavaScriptBuilderElementData>>((k, f) =>
                {
                    result = f(flowData.Object.Pipeline);
                    return result;
                });

            _javaScriptBuilderElement.Process(flowData.Object);

            Assert.IsTrue(result.JavaScript.Contains("abcdefg-hijklmn-opqrst-uvwxyz"),
                $"JavaScript does not contain expected session id 'abcdefg-hijklmn-opqrst-uvwxyz'.");
            Assert.IsTrue(result.JavaScript.Contains("var sequence = 1;"),
                $"JavaScript does not contain expected sequence '1'.");
        }

        /// <summary>
        /// Check that the callback URL is generated correctly.
        /// </summary>
        [TestMethod]
        public void JavaScriptBuilder_VerifyFallbackResponse()
        {
            _javaScriptBuilderElement =
                new JavaScriptBuilderElementBuilder(LoggerFactory)
                .SetEndpoint("/json")
                .Build();

            var flowData = new Mock<IFlowData>();

            flowData.Setup(d => d.Get<IJsonBuilderElementData>())
                .Throws<KeyNotFoundException>();
            var evidence = new Evidence(LoggerFactory.CreateLogger<Evidence>()); 
            flowData.Setup(d => d.GetEvidence())
                .Returns(evidence);

            var jsonData = new Mock<IJsonBuilderElementData>();
            jsonData.Setup(j => j.Json)
                .Returns("{}");

            IJavaScriptBuilderElementData result = _javaScriptBuilderElement.GetFallbackResponse(flowData.Object, jsonData.Object);

            Assert.IsFalse(string.IsNullOrWhiteSpace(result.JavaScript));
        }

        /// <summary>
        /// Check that the callback URL is generated correctly.
        /// </summary>
        /// <remarks>
        /// </remarks>
        [TestMethod]
        public void JavaScriptBuilder_VerifyUrl()
        {
            _javaScriptBuilderElement =
                new JavaScriptBuilderElementBuilder(LoggerFactory)
                .SetEndpoint("/json")
                .Build();
            
            var flowData = new Mock<IFlowData>();
            Configure(flowData);

            IJavaScriptBuilderElementData result = null;
            flowData.Setup(d => d.GetOrAdd(
                It.IsAny<ITypedKey<IJavaScriptBuilderElementData>>(),
                It.IsAny<Func<IPipeline, IJavaScriptBuilderElementData>>()))
                .Returns<ITypedKey<IJavaScriptBuilderElementData>, Func<IPipeline, IJavaScriptBuilderElementData>>((k, f) =>
                {
                    result = f(flowData.Object.Pipeline);
                    return result;
                });

            _javaScriptBuilderElement.Process(flowData.Object);

            string expectedUrl = "https://localhost/json";
            Assert.IsTrue(result.JavaScript.Contains(expectedUrl),
                $"JavaScript does not contain expected URL '{expectedUrl}'.");
        }

        /// <summary>
        /// Verify that parameters are set in the JavaScript payload and if the
        /// query parameters are in the evidence
        /// </summary>
        [DataTestMethod]
        [DataRow("iPhone", "51.12345", "-1.92173272")]
        [DataRow("Samsung", "1.09199", "2.1121121")]
        [DataRow("Sony", "3.123455", "44.1123111")]
        public void JavaScriptBuilder_VerifyParameters(string userAgent, string lat, string lon)
        {
            _javaScriptBuilderElement =
                new JavaScriptBuilderElementBuilder(LoggerFactory)
                .SetEndpoint("/json")
                .Build();

            var flowData = new Mock<IFlowData>();
            Configure(flowData, null, "localhost", "https", userAgent, lat, lon);

            IJavaScriptBuilderElementData result = null;
            flowData.Setup(d => d.GetOrAdd(
                It.IsAny<ITypedKey<IJavaScriptBuilderElementData>>(),
                It.IsAny<Func<IPipeline, IJavaScriptBuilderElementData>>()))
                .Returns<ITypedKey<IJavaScriptBuilderElementData>, Func<IPipeline, IJavaScriptBuilderElementData>>((k, f) =>
                {
                    result = f(flowData.Object.Pipeline);
                    return result;
                });

            _javaScriptBuilderElement.Process(flowData.Object);

            Assert.IsTrue(result.JavaScript.Contains(userAgent),
                $"JavaScript does not contain expected user agent query parameter '{userAgent}'.");
            Assert.IsTrue(result.JavaScript.Contains(lat),
                $"JavaScript does not contain expected user agent query parameter '{lat}'.");
            Assert.IsTrue(result.JavaScript.Contains(lon),
                $"JavaScript does not contain expected user agent query parameter '{lon}'.");
        }


        public enum ExceptionType
        {
            PropertyMissingException,
            PipelineDataException,
            InvalidCastException,
            KeyNotFoundException,
            Exception,
            None
        }

        /// <summary>
        /// Check that accessing the 'Promise' property works as intended 
        /// in a range of scenarios
        /// </summary>
        [DataTestMethod]
        [DataRow(ExceptionType.PropertyMissingException, false)]
        [DataRow(ExceptionType.Exception, true)]
        [DataRow(ExceptionType.InvalidCastException, false)]
        [DataRow(ExceptionType.KeyNotFoundException, false)]
        [DataRow(ExceptionType.None, false)]
        [DataRow(ExceptionType.PipelineDataException, false)]
        public void JavaScriptBuilderElement_Promise(ExceptionType exceptionThrownByPromiseProperty, bool exceptionExpected)
        {
            _javaScriptBuilderElement =
                new JavaScriptBuilderElementBuilder(LoggerFactory).Build();

            var flowData = new Mock<IFlowData>();
            Configure(flowData);

            IJavaScriptBuilderElementData result = null;
            flowData.Setup(d => d.GetOrAdd(
                It.IsAny<ITypedKey<IJavaScriptBuilderElementData>>(),
                It.IsAny<Func<IPipeline, IJavaScriptBuilderElementData>>()))
                .Returns<ITypedKey<IJavaScriptBuilderElementData>, Func<IPipeline, IJavaScriptBuilderElementData>>((k, f) =>
                {
                    result = f(flowData.Object.Pipeline);
                    return result;
                });

            switch (exceptionThrownByPromiseProperty)
            {
                case ExceptionType.PropertyMissingException:
                    flowData.Setup(d => d.GetAs<IAspectPropertyValue<string>>("Promise"))
                        .Throws(new PropertyMissingException("Problem!"));
                    break;
                case ExceptionType.PipelineDataException:
                    flowData.Setup(d => d.GetAs<IAspectPropertyValue<string>>("Promise"))
                        .Throws(new PipelineDataException("Problem!"));
                    break;
                case ExceptionType.InvalidCastException:
                    flowData.Setup(d => d.GetAs<IAspectPropertyValue<string>>("Promise"))
                        .Throws(new InvalidCastException("Problem!"));
                    break;
                case ExceptionType.KeyNotFoundException:
                    flowData.Setup(d => d.GetAs<IAspectPropertyValue<string>>("Promise"))
                        .Throws(new KeyNotFoundException("Problem!"));
                    break;
                case ExceptionType.Exception:
                    flowData.Setup(d => d.GetAs<IAspectPropertyValue<string>>("Promise"))
                        .Throws(new Exception("Problem!"));
                    break;
                case ExceptionType.None:
                    flowData.Setup(d => d.GetAs<IAspectPropertyValue<string>>("Promise"))
                        .Returns(new AspectPropertyValue<string>("Full"));
                    break;
                default:
                    break;
            }

            Exception thrown = null;

            try
            {
                _javaScriptBuilderElement.Process(flowData.Object);
            }
            catch(Exception ex)
            {
                thrown = ex;
            }

            if (exceptionExpected)
            {
                Assert.IsNotNull(thrown, "Expected an exception to be " +
                    "visible externally but it was not.");
            }
            else
            {
                Assert.IsNull(thrown, "Did not expect an exception " +
                    "to be visible externally but one was.");
                Assert.IsNotNull(result.JavaScript, "Expected JavaScript " +
                    "output to be populated but it was not.");
                Assert.AreNotEqual("", result.JavaScript, "Expected " +
                    "JavaScript output to be populated but it was not.");
            }
        }

        /// <summary>
        /// Verify that valid JavaScript is produced when there are
        /// delayed execution properties in the payload.
        /// </summary>
        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void JavaScriptBuilderElement_DelayExecution(bool minify)
        {
            _javaScriptBuilderElement = 
                new JavaScriptBuilderElementBuilder(LoggerFactory)
                .SetMinify(minify)
                .Build();
            
            dynamic json = new JObject();

            var locationData = new JObject();
            locationData.Add(new JProperty("postcode", null));
            locationData.Add(new JProperty("postcodenullreason", 
                "Evidence for this property has not been retrieved. Ensure the 'complete' method is called, passing the name of this property in the second parameter."));
            locationData.Add(new JProperty("postcodeevidenceproperties", new[] { "location.javascript" }));
            locationData.Add(new JProperty("javascript", "if (navigator.geolocation) { navigator.geolocation.getCurrentPosition(function() { // 51D replace this comment with callback function. }); }"));
            locationData.Add(new JProperty("javascriptdelayexecution", true));
            json["location"] = locationData;

            var flowData = new Mock<IFlowData>();
            Configure(flowData, json);

            IJavaScriptBuilderElementData result = null;
            flowData.Setup(d => d.GetOrAdd(
                It.IsAny<ITypedKey<IJavaScriptBuilderElementData>>(),
                It.IsAny<Func<IPipeline, IJavaScriptBuilderElementData>>()))
                .Returns<ITypedKey<IJavaScriptBuilderElementData>, Func<IPipeline, IJavaScriptBuilderElementData>>((k, f) =>
                {
                    result = f(flowData.Object.Pipeline);
                    return result;
                });

            _javaScriptBuilderElement.Process(flowData.Object);

            if (minify == false)
            {
                Assert.IsTrue(result.JavaScript.Contains("getEvidencePropertiesFromObject"),
                    "Expected the generated JavaScript to contain the " +
                    "'getEvidencePropertiesFromObject' function but it does not.");
            }
            IJavaScriptExecutor js = Driver;
            // Attempt to evaluate the JavaScript.
            js.ExecuteScript($"{result.JavaScript}; window.fod = fod;");
            var jsObject = js.ExecuteScript("return fod.sessionId;");
            Assert.IsNotNull(jsObject);
        }
        
        /// <summary>
        /// Check that the JavaScript object name can be 
        /// overridden successfully.
        /// </summary>
        [TestMethod]
        public void JavaScriptBuilder_VerifyObjName()
        {
            _javaScriptBuilderElement =
                new JavaScriptBuilderElementBuilder(LoggerFactory)
                .SetEndpoint("/json")
                .Build();

            var flowData = new Mock<IFlowData>();
            Configure(flowData, jsObjName: "testObj");

            IJavaScriptBuilderElementData result = null;
            flowData.Setup(d => d.GetOrAdd(
                It.IsAny<ITypedKey<IJavaScriptBuilderElementData>>(),
                It.IsAny<Func<IPipeline, IJavaScriptBuilderElementData>>()))
                .Returns<ITypedKey<IJavaScriptBuilderElementData>, Func<IPipeline, IJavaScriptBuilderElementData>>((k, f) =>
                {
                    result = f(flowData.Object.Pipeline);
                    return result;
                });

            _javaScriptBuilderElement.Process(flowData.Object);

            IJavaScriptExecutor js = Driver;

            // Run the JavaScript content from the cloud service and bind to 
            // window so we can check it later.
            js.ExecuteScript($"{result.JavaScript}; window.testObj = testObj;");
        }


        /// <summary>
        /// Test the JavaScript include by accessing the given property.
        /// </summary>
        /// <param name="javaScript"></param>
        /// <param name="key"></param>
        /// <param name="property"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool IsValidFodObject(string javaScript, string key, string property, object value)
        {
            IJavaScriptExecutor js = Driver;

            // Run the JavaScript content from the cloud service and bind to 
            // window so we can check it later.
            js.ExecuteScript($"{javaScript}; window.fod = fod;");

            var result = js.ExecuteScript($"return fod.{key}.{property};");

            if (result.ToString() == value.ToString())
                return true;
            else
                return false;
        }

        /// <summary>
        /// Cleanup the RemoteWebDriver and http listener.
        /// </summary>
        [TestCleanup]
        public override async Task Cleanup()
        {
            await base.Cleanup();

            // Stop the client server.
            clientServerTokenSource.Cancel();
            while (_clientServer.IsListening)
            {
                _clientServer.Stop();
                Thread.Sleep(1000);
            }
            // Close the listener
            _clientServer.Close();
        }
    }
}
