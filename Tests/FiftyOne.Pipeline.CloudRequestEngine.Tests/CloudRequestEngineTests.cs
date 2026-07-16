/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2026 51 Degrees Mobile Experts Limited, Davidson House,
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
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.CloudRequestEngine.Tests
{
    [TestClass]
    public class CloudRequestEngineTests
    {
        HttpClient _httpClient;
        private static ILoggerFactory _loggerFactory = new LoggerFactory();
        private Mock<HttpMessageHandler> _handlerMock;

        private Uri expectedUri = new Uri("https://cloud.51degrees.com/api/v4/json");

        private string _jsonResponse = "{'device':{'value':'1'}}";
        private HttpStatusCode _jsonResponseStatus = HttpStatusCode.OK;
        private string _evidenceKeysResponse = "['query.User-Agent']";
        private HttpStatusCode _evidenceKeysResponseStatus = HttpStatusCode.OK;
        private string _accessiblePropertiesResponse =
            "{'Products': {'device': {'DataTier': 'tier','Properties': [{'Name': 'value','Type': 'String','Category': 'Device'}]}}}";
        private HttpStatusCode _accessiblePropertiesResponseStatus = HttpStatusCode.OK;


        /// <summary>
        /// Test cloud request engine adds correct information to post request
        /// and returns the response in the ElementData
        /// </summary>
        [TestMethod]
        public void Process()
        {
            string resourceKey = "resource_key";
            string userAgent = "iPhone";
            ConfigureMockedClient(r =>
                r.Content.ReadAsStringAsync(TestContext.CancellationToken).Result.Contains($"resource={resourceKey}") // content contains resource key
                && r.Content.ReadAsStringAsync(TestContext.CancellationToken).Result.Contains($"User-Agent={userAgent}") // content contains licenseKey
            );

            var engine = new CloudRequestEngineBuilder(
                _loggerFactory, 
                _httpClient)
                .SetResourceKey(resourceKey)
                .Build();

            using (var pipeline = new PipelineBuilder(_loggerFactory).AddFlowElement(engine).Build())
            {
                var data = pipeline.CreateFlowData();
                data.AddEvidence("query.User-Agent", userAgent);

                data.Process();

                var result = data.GetFromElement(engine).JsonResponse;
                Assert.AreEqual("{'device':{'value':'1'}}", result);

                dynamic obj = JValue.Parse(result);
                Assert.AreEqual(1, (int)obj.device.value);
            }

            _handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(1), // we expected a single external request
               ItExpr.Is<HttpRequestMessage>(req =>
                  req.Method == HttpMethod.Post  // we expected a POST request
                  && req.RequestUri == expectedUri // to this uri
               ),
               ItExpr.IsAny<CancellationToken>()
            );
        }

        /// <summary>
        /// Verify that nested evidence keys like 'query.id.usage' preserve
        /// the full suffix 'id.usage' rather than just the last segment 'usage'.
        /// </summary>
        [TestMethod]
        public void EvidenceNestedKey_PreservesFullSuffix()
        {
            string resourceKey = "resource_key";
            string nestedKey = "query.id.usage";
            string value = "test123";
            string capturedContent = null;

            // Set up mock to capture the request content
            _handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            
            // Set up the JSON response - capture content for later assertion
            _handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.Is<HttpRequestMessage>(r =>
                      r.RequestUri.AbsolutePath.ToLower().EndsWith("json")),
                  ItExpr.IsAny<CancellationToken>()
               )
               .ReturnsAsync((HttpRequestMessage req, CancellationToken ct) =>
               {
                   // Capture the content for assertion
                   capturedContent = req.Content.ReadAsStringAsync(TestContext.CancellationToken).Result;
                   return new HttpResponseMessage()
                   {
                       StatusCode = _jsonResponseStatus,
                       Content = new StringContent(_jsonResponse),
                   };
               })
               .Verifiable();

            // Set up the evidencekeys response
            _handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.Is<HttpRequestMessage>(r =>
                      r.RequestUri.AbsolutePath.ToLower().EndsWith("evidencekeys")),
                  ItExpr.IsAny<CancellationToken>()
               )
               .ReturnsAsync(() => new HttpResponseMessage()
               {
                   StatusCode = _evidenceKeysResponseStatus,
                   Content = new StringContent(_evidenceKeysResponse),
               })
               .Verifiable();

            // Set up the accessibleproperties response
            _handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.Is<HttpRequestMessage>(r =>
                      r.RequestUri.AbsolutePath.ToLower().EndsWith("accessibleproperties")),
                  ItExpr.IsAny<CancellationToken>()
               )
               .ReturnsAsync(() => new HttpResponseMessage()
               {
                   StatusCode = _accessiblePropertiesResponseStatus,
                   Content = new StringContent(_accessiblePropertiesResponse),
               })
               .Verifiable();

            _httpClient = new HttpClient(_handlerMock.Object);

            var engine = new CloudRequestEngineBuilder(
                _loggerFactory,
                _httpClient)
                .SetResourceKey(resourceKey)
                .Build();

            using (var pipeline = new PipelineBuilder(_loggerFactory).AddFlowElement(engine).Build())
            {
                var data = pipeline.CreateFlowData();
                data.AddEvidence(nestedKey, value);
                data.Process();
            }

            // The key should be "id.usage" (everything after the first dot), not just "usage"
            Assert.Contains(
                "id.usage=test123",
                capturedContent,
                $"The nested key 'query.id.usage' should produce suffix 'id.usage', not just 'usage'.");
        }

        /// <summary>
        /// Test cloud request engine adds correct information to post request
        /// following the order of precedence when processing evidence and 
        /// returns the response in the ElementData. Evidence parameters 
        /// should be added in descending order of precedence.
        /// </summary>
        [TestMethod]
        [DataRow(false, "query.User-Agent=iPhone", "header.User-Agent=iPhone")]
        [DataRow(false, "query.User-Agent=iPhone", "cookie.User-Agent=iPhone")]
        [DataRow(true, "header.User-Agent=iPhone", "cookie.User-Agent=iPhone")]
        [DataRow(false, "query.value=1", "a.value=1")]
        [DataRow(true, "a.value=1", "b.value=1")]
        [DataRow(true, "e.value=1", "f.value=1")]
        public void EvidencePrecedence(bool warn, string evidence1, string evidence2)
        {
            var evidence1Parts = evidence1.Split("=");
            var evidence2Parts = evidence2.Split("=");

            string resourceKey = "resource_key";
            ConfigureMockedClient(r =>
                  r.Content.ReadAsStringAsync(TestContext.CancellationToken).Result.Contains(evidence1.Split('.').Last())
            );

            var loggerFactory = new TestLoggerFactory();

            var engine = new CloudRequestEngineBuilder(
                loggerFactory, 
                _httpClient)
                .SetResourceKey(resourceKey)
                .Build();

            using (var pipeline = new PipelineBuilder(loggerFactory).AddFlowElement(engine).Build())
            {
                var data = pipeline.CreateFlowData();

                data.AddEvidence(evidence1Parts[0], evidence1Parts[1]);
                data.AddEvidence(evidence2Parts[0], evidence2Parts[1]);

                data.Process();
            }

            // Get loggers.
            var loggers = loggerFactory.Loggers
                .Where(l => l.Category == typeof(FlowElements.CloudRequestEngine).FullName);
            var logger = loggers.First();

            // If warn is expected then check for warnings from cloud request 
            // engine.
            if (warn) 
            {
                logger.AssertMaxWarnings(1);
                logger.AssertMaxErrors(0);
                Assert.AreEqual(1, logger.WarningEntries.Count());
                var warning = logger.WarningEntries.Single();
                Assert.Contains($"'{evidence1}' evidence conflicts with '{evidence2}'", warning);
            } 
            else
            {
                logger.AssertMaxWarnings(0);
                logger.AssertMaxErrors(0);
            }

            _handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(1), // we expected a single external request
               ItExpr.Is<HttpRequestMessage>(req =>
                  req.Method == HttpMethod.Post  // we expected a POST request
                  && req.RequestUri == expectedUri // to this uri
               ),
               ItExpr.IsAny<CancellationToken>()
            );
        }

        /// <summary>
        /// Test cloud request engine adds correct information to post request
        /// following the order of precedence when processing multiple pieces of
        /// conflicting evidence and returns the response in the ElementData. 
        /// Evidence parameters should be added in descending order of precedence.
        /// </summary>
        [TestMethod]
        [DataRow("header.User-Agent=iPhone", "cookie.User-Agent=iPhone")]
        [DataRow("header.User-Agent=iPhone", "cookie.User-Agent=iPhone", "a.User-Agent=Samsung")]
        [DataRow("header.User-Agent=iPhone", "cookie.User-Agent=iPhone", "a.User-Agent=Samsung", "b.User-Agent=Samsung")]
        [DataRow("a.value=1", "b.value=1")]
        [DataRow("a.value=1", "b.value=2")]
        [DataRow("a.value=1", "b.value=2", "c.value=3")]
        [DataRow("a.value=1", "b.value=1", "c.value=1")]
        [DataRow("e.value=1", "f.value=1", "g.value=1", "h.value=1")]
        public void EvidencePrecedenceMultipleConflicts(params string[] evidence)
        {
            string resourceKey = "resource_key";

            // Get a list of evidence that should not be in the result.
            var excludedEvidence = evidence
                .Select(e => e.Split('.').Last())
                .Distinct()
                .Where(e => e != evidence[0].Split('.').Last());

            ConfigureMockedClient(r =>
            {
                var valid = true || excludedEvidence.Count() > 0;
                // Check that excluded evidence is not in the result.
                foreach(var item in excludedEvidence)
                {
                    valid = r.Content.ReadAsStringAsync(TestContext.CancellationToken).Result.Contains(item) == false;
                    if (valid == false)
                    {
                        break;
                    }
                }

                // Check that the expected evidence is in the result.
                return r.Content.ReadAsStringAsync(TestContext.CancellationToken).Result.Contains(evidence[0].Split('.').Last()) && valid;
            });

            var loggerFactory = new TestLoggerFactory();

            var engine = new CloudRequestEngineBuilder(
                loggerFactory, 
                _httpClient)
                .SetResourceKey(resourceKey)
                .Build();

            using (var pipeline = new PipelineBuilder(loggerFactory).AddFlowElement(engine).Build())
            {
                var data = pipeline.CreateFlowData();

                foreach (var item in evidence)
                {
                    var evidenceParts = item.Split("=");
                    data.AddEvidence(evidenceParts[0], evidenceParts[1]);
                }

                data.Process();
            }

            // Get loggers.
            var loggers = loggerFactory.Loggers
                .Where(l => l.Category == typeof(FlowElements.CloudRequestEngine).FullName);
            var logger = loggers.First();

            // Check that the expected number of warnings has been logged.
            logger.AssertMaxWarnings(evidence.Length - 1);
            logger.AssertMaxErrors(0);
            Assert.AreEqual(evidence.Length - 1, logger.WarningEntries.Count(), 
                $"The number of warnings logged ({logger.WarningEntries.Count()}) " +
                $"did not match what was expected ({evidence.Length - 1})");
            // Check that only conflict warnings have been logged.
            foreach (var warning in logger.WarningEntries)
            {
                Assert.Contains("evidence conflicts", warning);
            }

            _handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(1), // we expected a single external request
               ItExpr.Is<HttpRequestMessage>(req =>
                  req.Method == HttpMethod.Post  // we expected a POST request
                  && req.RequestUri == expectedUri // to this uri
               ),
               ItExpr.IsAny<CancellationToken>()
            );
        }

        /// <summary>
        /// Test cloud request engine adds correct information to post request
        /// and returns the response in the ElementData
        /// </summary>
        [TestMethod]
        public void Process_LicenseKey()
        {
            string resourceKey = "resource_key";
            string userAgent = "iPhone";
            string licenseKey = "ABCDEFG";
            ConfigureMockedClient(r =>
                  r.Content.ReadAsStringAsync(TestContext.CancellationToken).Result.Contains($"resource={resourceKey}") // content contains resource key
                  && r.Content.ReadAsStringAsync(TestContext.CancellationToken).Result.Contains($"license={licenseKey}") // content contains licenseKey
                  && r.Content.ReadAsStringAsync(TestContext.CancellationToken).Result.Contains($"User-Agent={userAgent}") // content contains user agent
            );

#pragma warning disable CS0618 // Type or member is obsolete
            // SetLicensekey is obsolete but we still want to test that
            // it works as intended.
            var engine = new CloudRequestEngineBuilder(_loggerFactory, _httpClient)
                .SetResourceKey(resourceKey)
                .SetLicenseKey(licenseKey)
#pragma warning restore CS0618 // Type or member is obsolete
                .Build();

            using (var pipeline = new PipelineBuilder(_loggerFactory).AddFlowElement(engine).Build())
            {
                var data = pipeline.CreateFlowData();
                data.AddEvidence("query.User-Agent", userAgent);

                data.Process();

                var result = data.GetFromElement(engine).JsonResponse;
                Assert.AreEqual("{'device':{'value':'1'}}", result);

                dynamic obj = JValue.Parse(result);
                Assert.AreEqual(1, (int)obj.device.value);
            }

            _handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(1), // we expected a single external request
               ItExpr.Is<HttpRequestMessage>(req =>
                  req.Method == HttpMethod.Post  // we expected a POST request
                  && req.RequestUri == expectedUri // to this uri
               ),
               ItExpr.IsAny<CancellationToken>()
            );
        }

        /// <summary>
        /// Verify that the CloudRequestEngine can correctly parse a 
        /// response from the accessible properties endpoint that contains
        /// meta-data for sub-properties.
        /// </summary>
        [TestMethod]
        public void SubProperties()
        {
            _accessiblePropertiesResponse = @"
{
    ""Products"": {
        ""device"": {
            ""DataTier"": ""CloudV4TAC"",
            ""Properties"": [
                {
                    ""Name"": ""IsMobile"",
                    ""Type"": ""Boolean"",
                    ""Category"": ""Device""
                },
                {
                    ""Name"": ""IsTablet"",
                    ""Type"": ""Boolean"",
                    ""Category"": ""Device""
                }
            ]
        },
        ""devices"": {
            ""DataTier"": ""CloudV4TAC"",
            ""Properties"": [
                {
                    ""Name"": ""Devices"",
                    ""Type"": ""Array"",
                    ""Category"": ""Unspecified"",
                    ""ItemProperties"": [
                        {
                            ""Name"": ""IsMobile"",
                            ""Type"": ""Boolean"",
                            ""Category"": ""Device""
                        },
                        {
                            ""Name"": ""IsTablet"",
                            ""Type"": ""Boolean"",
                            ""Category"": ""Device""
                        }
                    ]
                }
            ]
        }
    }
}";
            ConfigureMockedClient(r => true);

            var engine = new CloudRequestEngineBuilder(
                _loggerFactory, 
                _httpClient)
                .SetResourceKey("key")
                .Build();

            Assert.HasCount(2, engine.PublicProperties);
            var deviceProperties = engine.PublicProperties["device"];
            Assert.HasCount(2, deviceProperties.Properties);
            Assert.IsTrue(deviceProperties.Properties.Any(p => p.Name.Equals("IsMobile")));
            Assert.IsTrue(deviceProperties.Properties.Any(p => p.Name.Equals("IsTablet")));
            var devicesProperties = engine.PublicProperties["devices"];
            Assert.HasCount(1, devicesProperties.Properties);
            Assert.AreEqual("Devices", devicesProperties.Properties[0].Name);
            Assert.IsTrue(devicesProperties.Properties[0].ItemProperties.Any(p => p.Name.Equals("IsMobile")));
            Assert.IsTrue(devicesProperties.Properties[0].ItemProperties.Any(p => p.Name.Equals("IsTablet")));
        }


        /// <summary>
        /// Test cloud request engine handles errors from the cloud service 
        /// as expected.
        /// A PipelineException should be thrown by the cloud request engine
        /// and the pipeline is configured to throw any exceptions up 
        /// the stack in an AggregateException.
        /// We also check that the exception message includes the content 
        /// from the JSON response.
        /// </summary>
        [TestMethod]
        public void ValidateErrorHandling()
        {
            string resourceKey = "resource_key";
            string userAgent = "iPhone";
            _jsonResponse = @"{ ""errors"": [ ""This resource key is not authorized for use with this domain: . Please visit https://configure.51degrees.com to update your resource key.""] }";

            ConfigureMockedClient(r =>
                r.Content.ReadAsStringAsync(TestContext.CancellationToken).Result.Contains($"resource={resourceKey}") // content contains resource key
                && r.Content.ReadAsStringAsync(TestContext.CancellationToken).Result.Contains($"User-Agent={userAgent}") // content contains licenseKey
            );

            var engine = new CloudRequestEngineBuilder(
                _loggerFactory, 
                _httpClient)
                .SetResourceKey(resourceKey)
                .Build();

            Exception exception = null;

            try
            {
                using var pipeline = new PipelineBuilder(_loggerFactory).AddFlowElement(engine).Build();
                var data = pipeline.CreateFlowData();
                data.AddEvidence("query.User-Agent", userAgent);

                data.Process();
            }
            catch(Exception ex)
            {
                exception = ex;
            }

            Assert.IsNotNull(exception, "Expected exception to occur");
            Assert.IsInstanceOfType(exception, typeof(AggregateException));
            var aggEx = exception as AggregateException;
            Assert.HasCount(1, aggEx.InnerExceptions);
            var realEx = aggEx.InnerExceptions[0];
            Assert.IsInstanceOfType(realEx, typeof(PipelineException));
            Assert.Contains("This resource key is not authorized for use with this domain",
                        realEx.Message, "Exception message did not contain the expected text.");
        }


        /// <summary>
        /// Test cloud request engine handles errors from the cloud service
        /// as expected.
        /// Build performs the discovery requests against the cloud service,
        /// so a CloudRequestException containing the errors from the cloud
        /// service should be thrown by Build.
        /// We also check that the exception message includes the content
        /// from the JSON response.
        /// </summary>
        [TestMethod]
        public void ValidateErrorHandling_InvalidResourceKey()
        {
            string resourceKey = "resource_key";
            string userAgent = "iPhone";
            _accessiblePropertiesResponse = @"{ ""errors"":[""resource_key not a valid resource key""]}";
            _accessiblePropertiesResponseStatus = HttpStatusCode.BadRequest;

            ConfigureMockedClient(r =>
                r.Content.ReadAsStringAsync(TestContext.CancellationToken).Result.Contains($"resource={resourceKey}") // content contains resource key
                && r.Content.ReadAsStringAsync(TestContext.CancellationToken).Result.Contains($"User-Agent={userAgent}") // content contains licenseKey
            );

            Exception exception = null;

            try
            {
                var engine = new CloudRequestEngineBuilder(
                    _loggerFactory,
                    _httpClient)
                    .SetResourceKey(resourceKey)
                    .Build();
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.IsNotNull(exception, "Expected exception to occur");
            Assert.IsInstanceOfType(exception, typeof(CloudRequestException));
            var cloudEx = exception as CloudRequestException;
            Assert.Contains("resource_key not a valid resource key",
cloudEx.Message, "Exception message did not contain the expected text.");
        }


        /// <summary>
        /// Test cloud request engine handles multiple errors from the cloud 
        /// service as expected.
        /// An AggregateException should be thrown by the cloud request engine
        /// and the pipeline is configured to throw any exceptions up 
        /// the stack as another AggregateException.
        /// We also check that the exception messages include the content 
        /// from the JSON response.
        /// </summary>
        [TestMethod]
        public void ValidateErrorHandling_MultipleErrors()
        {
            string resourceKey = "resource_key";
            string userAgent = "iPhone";
            _jsonResponse = @"{ ""errors"": [""This resource key is not authorized for use with this domain: . Please visit https://configure.51degrees.com to update your resource key."",""Some other error""] }";

            ConfigureMockedClient(r =>
                r.Content.ReadAsStringAsync(TestContext.CancellationToken).Result
                .Contains($"resource={resourceKey}") 
                && r.Content.ReadAsStringAsync(TestContext.CancellationToken).Result
                .Contains($"User-Agent={userAgent}")
            );

            var engine = new CloudRequestEngineBuilder(
                _loggerFactory, 
                _httpClient)
                .SetResourceKey(resourceKey)
                .Build();

            Exception exception = null;

            try
            {
                using var pipeline = new PipelineBuilder(_loggerFactory).AddFlowElement(engine).Build();
                var data = pipeline.CreateFlowData();
                data.AddEvidence("query.User-Agent", userAgent);

                data.Process();
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.IsNotNull(exception, "Expected exception to occur");
            Assert.IsInstanceOfType<AggregateException>(exception);
            Assert.IsInstanceOfType<CloudRequestException>(exception.InnerException);
            Assert.IsInstanceOfType<AggregateException>(exception.InnerException.InnerException);
            var aggEx = (exception.InnerException.InnerException as AggregateException).Flatten();
            Assert.HasCount(2, aggEx.InnerExceptions);
            Assert.IsInstanceOfType<CloudRequestException>(aggEx.InnerExceptions[0]);
            Assert.IsInstanceOfType<CloudRequestException>(aggEx.InnerExceptions[1]);
            Assert.IsTrue(aggEx.InnerExceptions.Any(e => e.Message.Contains(
                "This resource key is not authorized for use with this domain")),
                "Exception message did not contain the expected text.");
            Assert.IsTrue(aggEx.InnerExceptions.Any(e => e.Message.Contains(
                "Some other error")),
                "Exception message did not contain the expected text.");
        }

        /// <summary>
        /// Test cloud request engine handles a lack of data from the
        /// cloud service as expected.
        /// An exception should be thrown by the cloud request engine
        /// and the pipeline is configured to throw any exceptions up
        /// the stack as an AggregateException.
        /// </summary>
        [TestMethod]
        public void ValidateErrorHandling_NoData()
        {
            string resourceKey = "resource_key";
            string userAgent = "iPhone";
            _jsonResponse = @"{ }";

            ConfigureMockedClient(r =>
                r.Content.ReadAsStringAsync(TestContext.CancellationToken).Result
                .Contains($"resource={resourceKey}") && 
                r.Content.ReadAsStringAsync(TestContext.CancellationToken).Result
                .Contains($"User-Agent={userAgent}")
            );

            var engine = new CloudRequestEngineBuilder(
                _loggerFactory,
                _httpClient)
                .SetResourceKey(resourceKey)
                .Build();

            using var pipeline = new PipelineBuilder(_loggerFactory).AddFlowElement(engine).Build();
            var data = pipeline.CreateFlowData();
            data.AddEvidence("query.User-Agent", userAgent);

            Assert.ThrowsExactly<AggregateException>(() => data.Process());
        }

        /// <summary>
        /// Test cloud request engine handles a lack of data from the
        /// cloud service as expected.
        /// Build performs the discovery requests against the cloud service,
        /// so it should throw a CloudRequestException describing the
        /// failed properties request.
        /// </summary>
        [TestMethod]
        public void ValidateErrorHandling_NotJson()
        {
            string resourceKey = "resource_key";

            _jsonResponse = "Status code: 404, '*json' method not found";
            _jsonResponseStatus = HttpStatusCode.NotFound;

            _evidenceKeysResponse = "Status code: 404, '*evidencekeys' method not found";
            _evidenceKeysResponseStatus = HttpStatusCode.NotFound;

            _accessiblePropertiesResponse = "Status code: 404, '*accessibleproperties' method not found";
            _accessiblePropertiesResponseStatus = HttpStatusCode.NotFound;

            ConfigureMockedClient(_ => true);

            var ex = Assert.ThrowsExactly<CloudRequestException>(() =>
            {
                var engine = new CloudRequestEngineBuilder(
                    _loggerFactory,
                    _httpClient)
                    .SetResourceKey(resourceKey)
                    .Build();
            });

            Assert.AreEqual(404, ex.HttpStatusCode, "Status code should be 404");
            Assert.IsNotNull(ex.ResponseHeaders, "Response headers not populated");
            Assert.IsNotNull(ex.InnerException, "Inner exception not populated");
            Assert.IsInstanceOfType<JsonReaderException>(ex.InnerException, $"Inner exception is not an instance of {nameof(JsonReaderException)}");
        }

        /// <summary>
        /// Test cloud request engine handles a lack of data from the 
        /// cloud service as expected.
        /// An exception should be thrown by the cloud request engine
        /// and the pipeline is configured to throw any exceptions up 
        /// the stack as an AggregateException.
        /// </summary>
        [TestMethod]
        public void ValidateErrorHandling_RetryAfterNotJson()
        {
            string resourceKey = "resource_key";
            string userAgent = "iPhone";

            ConfigureMockedClient(_ => true);

            var engine = new CloudRequestEngineBuilder(
                _loggerFactory, 
                _httpClient)
                .SetResourceKey(resourceKey)
                .Build();

            using var pipeline = new PipelineBuilder(_loggerFactory).AddFlowElement(engine).Build();
            var defaultResponses = new string[]
            {
                    _jsonResponse,
                    _evidenceKeysResponse,
                    _accessiblePropertiesResponse,
            };

            _jsonResponse = "Status code: 404, '*json' method not found";
            _jsonResponseStatus = HttpStatusCode.NotFound;

            _evidenceKeysResponse = "Status code: 404, '*evidencekeys' method not found";
            _evidenceKeysResponseStatus = HttpStatusCode.NotFound;

            _accessiblePropertiesResponse = "Status code: 404, '*accessibleproperties' method not found";
            _accessiblePropertiesResponseStatus = HttpStatusCode.NotFound;

            using (var flowData = pipeline.CreateFlowData())
            {
                flowData.AddEvidence("query.User-Agent", userAgent);
                try
                {
                    flowData.Process();
                    Assert.Fail("Expected exception did not occur");
                }
                catch (AggregateException)
                {
                    // nop
                }
            }
            _jsonResponse = defaultResponses[0];
            _jsonResponseStatus = HttpStatusCode.OK;

            _evidenceKeysResponse = defaultResponses[1];
            _evidenceKeysResponseStatus = HttpStatusCode.OK;

            _accessiblePropertiesResponse = defaultResponses[2];
            _accessiblePropertiesResponseStatus = HttpStatusCode.OK;

            using (var flowData = pipeline.CreateFlowData())
            {
                flowData.AddEvidence("query.User-Agent", userAgent);
                flowData.Process();
                var result = flowData.GetFromElement(engine).JsonResponse;
                Assert.AreEqual("{'device':{'value':'1'}}", result);
            }
        }

        [TestMethod]
        public void ValidateErrorHandling_RetryAfterNotJson_WithinRecovery()
        {
            string resourceKey = "resource_key";
            string userAgent = "iPhone";

            ConfigureMockedClient(_ => true);

            var engine = new CloudRequestEngineBuilder(
                _loggerFactory,
                _httpClient)
                .SetResourceKey(resourceKey)
                .SetRecoverySeconds(0.1)
                .SetFailuresToEnterRecovery(1)
                .Build();

            using var pipeline = new PipelineBuilder(_loggerFactory).AddFlowElement(engine).Build();
            var defaultResponses = new string[]
            {
                    _jsonResponse,
                    _evidenceKeysResponse,
                    _accessiblePropertiesResponse,
            };

            _jsonResponse = "Status code: 404, '*json' method not found";
            _jsonResponseStatus = HttpStatusCode.NotFound;

            _evidenceKeysResponse = "Status code: 404, '*evidencekeys' method not found";
            _evidenceKeysResponseStatus = HttpStatusCode.NotFound;

            _accessiblePropertiesResponse = "Status code: 404, '*accessibleproperties' method not found";
            _accessiblePropertiesResponseStatus = HttpStatusCode.NotFound;

            using (var flowData = pipeline.CreateFlowData())
            {
                flowData.AddEvidence("query.User-Agent", userAgent);
                try
                {
                    flowData.Process();
                    Assert.Fail("Expected exception did not occur");
                }
                catch (AggregateException)
                {
                    // nop
                }
            }
            _jsonResponse = defaultResponses[0];
            _jsonResponseStatus = HttpStatusCode.OK;

            _evidenceKeysResponse = defaultResponses[1];
            _evidenceKeysResponseStatus = HttpStatusCode.OK;

            _accessiblePropertiesResponse = defaultResponses[2];
            _accessiblePropertiesResponseStatus = HttpStatusCode.OK;

            using (var flowData = pipeline.CreateFlowData())
            {
                flowData.AddEvidence("query.User-Agent", userAgent);
                try
                {
                    flowData.Process();
                    Assert.Fail($"{nameof(Process)} didn't throw.");
                }
                catch (AggregateException ex)
                {
                    Assert.IsInstanceOfType<PipelineTemporarilyUnavailableException>(
                        ex.InnerException);
                }
            }
        }

        [TestMethod]
        public void ValidateErrorHandling_RetryAfterNotJson_AfterRecovery()
        {
            string resourceKey = "resource_key";
            string userAgent = "iPhone";

            ConfigureMockedClient(_ => true);

            var engine = new CloudRequestEngineBuilder(
                _loggerFactory,
                _httpClient)
                .SetResourceKey(resourceKey)
                .SetRecoverySeconds(0.1)
                .SetFailuresToEnterRecovery(1)
                .Build();

            using var pipeline = new PipelineBuilder(_loggerFactory).AddFlowElement(engine).Build();
            var defaultResponses = new string[]
            {
                    _jsonResponse,
                    _evidenceKeysResponse,
                    _accessiblePropertiesResponse,
            };

            _jsonResponse = "Status code: 404, '*json' method not found";
            _jsonResponseStatus = HttpStatusCode.NotFound;

            _evidenceKeysResponse = "Status code: 404, '*evidencekeys' method not found";
            _evidenceKeysResponseStatus = HttpStatusCode.NotFound;

            _accessiblePropertiesResponse = "Status code: 404, '*accessibleproperties' method not found";
            _accessiblePropertiesResponseStatus = HttpStatusCode.NotFound;

            using (var flowData = pipeline.CreateFlowData())
            {
                flowData.AddEvidence("query.User-Agent", userAgent);
                try
                {
                    flowData.Process();
                    Assert.Fail("Expected exception did not occur");
                }
                catch (AggregateException)
                {
                    // nop
                }
            }

            Thread.Sleep(millisecondsTimeout: 200);

            _jsonResponse = defaultResponses[0];
            _jsonResponseStatus = HttpStatusCode.OK;

            _evidenceKeysResponse = defaultResponses[1];
            _evidenceKeysResponseStatus = HttpStatusCode.OK;

            _accessiblePropertiesResponse = defaultResponses[2];
            _accessiblePropertiesResponseStatus = HttpStatusCode.OK;

            using (var flowData = pipeline.CreateFlowData())
            {
                flowData.AddEvidence("query.User-Agent", userAgent);
                flowData.Process();
                var result = flowData.GetFromElement(engine).JsonResponse;
                Assert.AreEqual("{'device':{'value':'1'}}", result);
            }
        }

        [TestMethod]
        public void ValidateErrorHandling_RetryAfterNotJson_RetryWhileRecovering()
        {
            string resourceKey = "resource_key";
            string userAgent = "iPhone";

            ConfigureMockedClient(_ => true);

            DateTime startTime = DateTime.Now;
            DateTime unlockTime = startTime.AddMilliseconds(300);

            var engine = new CloudRequestEngineBuilder(
                _loggerFactory,
                _httpClient)
                .SetResourceKey(resourceKey)
                .SetRecoverySeconds(0.3)
                .SetFailuresToEnterRecovery(1)
                .Build();

            using var pipeline = new PipelineBuilder(_loggerFactory).AddFlowElement(engine).Build();
            var defaultResponses = new string[]
            {
                    _jsonResponse,
                    _evidenceKeysResponse,
                    _accessiblePropertiesResponse,
            };

            _jsonResponse = "Status code: 404, '*json' method not found";
            _jsonResponseStatus = HttpStatusCode.NotFound;

            _evidenceKeysResponse = "Status code: 404, '*evidencekeys' method not found";
            _evidenceKeysResponseStatus = HttpStatusCode.NotFound;

            _accessiblePropertiesResponse = "Status code: 404, '*accessibleproperties' method not found";
            _accessiblePropertiesResponseStatus = HttpStatusCode.NotFound;

            using (var flowData = pipeline.CreateFlowData())
            {
                flowData.AddEvidence("query.User-Agent", userAgent);
                try
                {
                    flowData.Process();
                    Assert.Fail("Expected exception did not occur");
                }
                catch (AggregateException)
                {
                    // nop
                }
            }

            _jsonResponse = defaultResponses[0];
            _jsonResponseStatus = HttpStatusCode.OK;

            _evidenceKeysResponse = defaultResponses[1];
            _evidenceKeysResponseStatus = HttpStatusCode.OK;

            _accessiblePropertiesResponse = defaultResponses[2];
            _accessiblePropertiesResponseStatus = HttpStatusCode.OK;

            bool didFinish = false;
            int failures = 0;
            for (int i = 0; i < 10 && !didFinish; ++i)
            {
                Thread.Sleep(millisecondsTimeout: 50);

                using var flowData = pipeline.CreateFlowData();
                flowData.AddEvidence("query.User-Agent", userAgent);
                try
                {
                    flowData.Process();
                    var result = flowData.GetFromElement(engine).JsonResponse;
                    Assert.AreEqual("{'device':{'value':'1'}}", result);
                    Assert.IsTrue(DateTime.Now >= unlockTime,
                        "New request succeeded within RecoveryPeriod"
                        + $" Iteration: {i}, "
                        + $" Offset: {(DateTime.Now - unlockTime).TotalSeconds}s");
                    didFinish = true;
                }
                catch (AggregateException ex)
                {
                    ++failures;
                    Assert.IsInstanceOfType<PipelineTemporarilyUnavailableException>(
                        ex.InnerException,
                        "Unexpected error during RecoveryPeriod.");
                }
            }
            Assert.IsTrue(didFinish, "No request succeeded since first failure.");
            Assert.IsGreaterThan(0, failures, "First attempt was successful.");
        }

        /// <summary>
        /// Reproduce the full circuit-breaker cycle on the per-request
        /// (Process) path with the exponential backoff recovery strategy
        /// enabled, which is the configuration a consumer turns on to
        /// shorten the recovery blackout.
        ///
        /// <para>
        /// Simulate a cloud failure, confirm one failure trips the
        /// breaker, confirm that a request made while the breaker is open
        /// is suppressed (throws without making any HTTP call, which is
        /// what stops a single trip flooding logs and parking threads),
        /// and confirm the engine comes back alive once the backoff delay
        /// has elapsed and the cloud is healthy again.
        /// </para>
        /// </summary>
        [TestMethod]
        public void ExponentialBackoff_TripsThenRecoversAfterDelay()
        {
            string resourceKey = "resource_key";
            string userAgent = "iPhone";
            const double initialDelaySeconds = 0.5;

            ConfigureMockedClient(_ => true);

            // Warmup discovery succeeds, so the engine builds cleanly and
            // only the per-request json call fails below.
            var engine = new CloudRequestEngineBuilder(
                _loggerFactory,
                _httpClient)
                .SetResourceKey(resourceKey)
                .SetUseExponentialBackoff(true)
                .SetExponentialBackoffInitialDelay(initialDelaySeconds)
                .SetExponentialBackoffMaxDelay(30)
                .SetExponentialBackoffMultiplier(2)
                .SetFailuresToEnterRecovery(1)
                .Build();

            using var pipeline = new PipelineBuilder(_loggerFactory)
                .AddFlowElement(engine).Build();

            void Process()
            {
                using var flowData = pipeline.CreateFlowData();
                flowData.AddEvidence("query.User-Agent", userAgent);
                flowData.Process();
            }

            // The cloud starts failing every per-request json call.
            _jsonResponse = "Status code: 503, service unavailable";
            _jsonResponseStatus = HttpStatusCode.ServiceUnavailable;

            // First request is attempted, fails, and trips the breaker.
            var firstError = Assert.ThrowsExactly<AggregateException>(Process);
            Assert.IsNotInstanceOfType<PipelineTemporarilyUnavailableException>(
                firstError.InnerException,
                "The first failure should be the real cloud error, not a " +
                "suppression, because the breaker is not open yet.");
            VerifyJsonPosts(Times.Exactly(1));

            // While the breaker is open, a request is short-circuited: it
            // throws PipelineTemporarilyUnavailableException and makes no
            // HTTP call (the json POST count stays at one).
            var suppressed = Assert.ThrowsExactly<AggregateException>(Process);
            Assert.IsInstanceOfType<PipelineTemporarilyUnavailableException>(
                suppressed.InnerException,
                "A request during the recovery window should be suppressed.");
            VerifyJsonPosts(Times.Exactly(1));

            // The cloud recovers, but the engine stays suppressed until the
            // backoff delay elapses.
            _jsonResponse = "{'device':{'value':'1'}}";
            _jsonResponseStatus = HttpStatusCode.OK;

            Thread.Sleep(TimeSpan.FromSeconds(initialDelaySeconds + 0.3));

            // After the delay the engine comes back alive: the request is
            // attempted again (a second json POST) and succeeds.
            Process();
            VerifyJsonPosts(Times.Exactly(2));
        }

        private void VerifyJsonPosts(Times times)
        {
            _handlerMock.Protected().Verify(
               "SendAsync",
               times,
               ItExpr.Is<HttpRequestMessage>(req =>
                  req.Method == HttpMethod.Post
                  && req.RequestUri.AbsolutePath.ToLower().EndsWith("json")
               ),
               ItExpr.IsAny<CancellationToken>()
            );
        }

        /// <summary>
        /// Verify that the 'DelayExecution' and 'EvidenceProperties'
        /// properties are populated correctly by the CloudRequestEngine.
        /// </summary>
        [TestMethod]
        public void ValidateDelayedExecutionProperties()
        {
            _accessiblePropertiesResponse =
                "{'Products': {'location': {'DataTier': 'tier','Properties': [" +
                    "{'Name': 'javascript','Type': 'JavaScript','Category': 'Unspecified','DelayExecution':true}," +
                    "{'Name': 'postcode','Type': 'String','Category': 'Unspecified','EvidenceProperties':[ 'location.javascript' ]}]}}}";

            ConfigureMockedClient(r => true);

            var engine = new CloudRequestEngineBuilder(
                _loggerFactory, 
                _httpClient)
                .SetResourceKey("key")
                .Build();

            Assert.HasCount(1, engine.PublicProperties);
            var locationProperties = engine.PublicProperties["location"];
            Assert.HasCount(2, locationProperties.Properties);
            var javascript = locationProperties.Properties.Where(p => p.Name.Equals("javascript")).Single();
            var postcode = locationProperties.Properties.Where(p => p.Name.Equals("postcode")).Single();
            Assert.IsTrue(javascript.DelayExecution);
            Assert.HasCount(1, postcode.EvidenceProperties);
            Assert.AreEqual("location.javascript", postcode.EvidenceProperties.Single());
        }

        /// <summary>
        /// For a resource key with access only to device detection properties,
        /// test that two requests are made using the same user-agent and no 
        /// other device detection evidence results in a cache miss, followed 
        /// by a cache hit.
        /// </summary>
        [TestMethod]
        public void ValidateCacheHitOrMiss_SameUserAgent()
        {
            string resourceKey = "resource_key";
            string userAgent = "iPhone";
            
            _jsonResponse = @"{ ""device"": { ""ismobile"": true } }";
            ConfigureMockedClient(r => true);

            var engine = new CloudRequestEngineBuilder(
                _loggerFactory, 
                _httpClient)
            .SetResourceKey(resourceKey)
            .SetCacheSize(10)
            .SetCacheHitOrMiss(true)
            .Build();

            using var pipeline = new PipelineBuilder(_loggerFactory).AddFlowElement(engine).Build();
            var data1 = pipeline.CreateFlowData();
            data1.AddEvidence("query.User-Agent", userAgent);

            data1.Process();

            Assert.IsFalse(data1.GetFromElement(engine).CacheHit, "cache miss should occur.");

            var data2 = pipeline.CreateFlowData();
            data2.AddEvidence("query.User-Agent", userAgent);

            data2.Process();

            Assert.IsTrue(data2.GetFromElement(engine).CacheHit, "cache hit should occur.");
        }

        /// <summary>
        /// For a resource key with access only to device detection properties,
        /// test two requests made using the same user-agent. The second has a
        /// x-operamini-phone-ua header. Both requests should be cache misses.
        /// </summary>
        [TestMethod]
        public void ValidateCacheHitOrMiss_SameUserAgent_AdditionalHeaders()
        {
            string resourceKey = "resource_key";
            string userAgent = "iPhone";
            string xOperaMiniUA1 = "SonyEricsson/W810i";
            string xOperaMiniUA2 = "Nokia/3310";

            _evidenceKeysResponse = "[ 'query.User-Agent', 'header.X-OperaMini-Phone-UA' ]";

            _jsonResponse = @"{ ""device"": { ""ismobile"": true } }";
            ConfigureMockedClient(r => true);

            var engine = new CloudRequestEngineBuilder(
                _loggerFactory, 
                _httpClient)
            .SetResourceKey(resourceKey)
            .SetCacheSize(10)
            .SetCacheHitOrMiss(true)
            .Build();

            using var pipeline = new PipelineBuilder(_loggerFactory).AddFlowElement(engine).Build();
            var data1 = pipeline.CreateFlowData();
            data1.AddEvidence("query.User-Agent", userAgent);
            data1.AddEvidence("header.X-OperaMini-Phone-UA", xOperaMiniUA1);

            data1.Process();

            Assert.IsFalse(data1.GetFromElement(engine).CacheHit, "cache miss should occur.");

            var data2 = pipeline.CreateFlowData();
            data2.AddEvidence("query.User-Agent", userAgent);
            data2.AddEvidence("header.X-OperaMini-Phone-UA", xOperaMiniUA2);

            data2.Process();

            Assert.IsFalse(data2.GetFromElement(engine).CacheHit, "cache miss should occur.");
        }

        /// <summary>
        /// For a resource key with differing levels of access, test two 
        /// requests made using the same user-agent but with different lat/lon
        /// values
        /// </summary>
        [TestMethod]
        // Access to device detection only
        [DataRow(true, "query.User-Agent")]
        // Access to device detection and geo-location
        [DataRow(false, "query.User-Agent", "query.51d_pos_latitude", "query.51d_pos_longitude")]
        // Access to geo-location only
        [DataRow(false, "query.51d_pos_latitude", "query.51d_pos_longitude")]
        public void ValidateCacheHitOrMiss_SameUserAgent_DifferentLocation(bool hit, params string[] evidenceKeys)
        {
            string resourceKey = "resource_key";
            string userAgent = "iPhone";
            string latlon1 = "51";
            string latlon2 = "1";

            _evidenceKeysResponse = $"[ '{string.Join("', '", evidenceKeys)}' ]";

            _jsonResponse = @"{ ""device"": { ""ismobile"": true } }";
            ConfigureMockedClient(r => true);

            var engine = new CloudRequestEngineBuilder(_loggerFactory, _httpClient)
            .SetResourceKey(resourceKey)
            .SetCacheSize(10)
            .SetCacheHitOrMiss(true)
            .Build();

            using var pipeline = new PipelineBuilder(_loggerFactory).AddFlowElement(engine).Build();
            var data1 = pipeline.CreateFlowData();
            data1.AddEvidence("query.User-Agent", userAgent);
            data1.AddEvidence("query.51d_pos_latitude", latlon1);
            data1.AddEvidence("query.51d_pos_longitude", latlon1);
            data1.Process();

            Assert.IsFalse(data1.GetFromElement(engine).CacheHit, "cache miss should occur.");

            var data2 = pipeline.CreateFlowData();
            data2.AddEvidence("query.User-Agent", userAgent);
            data2.AddEvidence("query.51d_pos_latitude", latlon2);
            data2.AddEvidence("query.51d_pos_longitude", latlon2);

            data2.Process();

            Assert.AreEqual(hit, data2.GetFromElement(engine).CacheHit, $"cache hit {(hit ? "should" : "shouldn't")} occur.");
        }

        /// <summary>
        /// For a resource key with differing levels of access, test two 
        /// requests made using a different user-agent but the same lat/lon
        /// values.
        /// </summary>
        [TestMethod]
        // Access to device detection only
        [DataRow(false, "query.User-Agent")]
        // Access to device detection and geo-location
        [DataRow(false, "query.User-Agent", "query.51d_pos_latitude", "query.51d_pos_longitude")]
        // Access to geo-location only
        [DataRow(true, "query.51d_pos_latitude", "query.51d_pos_longitude")]
        public void ValidateCacheHitOrMiss_DifferentUserAgent_SameLocation(bool hit, params string[] evidenceKeys)
        {
            string resourceKey = "resource_key";
            string userAgent1 = "iPhone";
            string userAgent2 = "Samsung";
            string latlon = "51";

            _evidenceKeysResponse = $"[ '{string.Join("', '", evidenceKeys)}' ]";

            _jsonResponse = @"{ ""device"": { ""ismobile"": true } }";
            ConfigureMockedClient(r => true);

            var engine = new CloudRequestEngineBuilder(
                _loggerFactory, 
                _httpClient)
            .SetResourceKey(resourceKey)
            .SetCacheSize(10)
            .SetCacheHitOrMiss(true)
            .Build();

            using var pipeline = new PipelineBuilder(_loggerFactory).AddFlowElement(engine).Build();
            var data1 = pipeline.CreateFlowData();
            data1.AddEvidence("query.User-Agent", userAgent1);
            data1.AddEvidence("query.51d_pos_latitude", latlon);
            data1.AddEvidence("query.51d_pos_longitude", latlon);
            data1.Process();

            Assert.IsFalse(data1.GetFromElement(engine).CacheHit, "cache miss should occur.");

            var data2 = pipeline.CreateFlowData();
            data2.AddEvidence("query.User-Agent", userAgent2);
            data2.AddEvidence("query.51d_pos_latitude", latlon);
            data2.AddEvidence("query.51d_pos_longitude", latlon);

            data2.Process();

            Assert.AreEqual(hit, data2.GetFromElement(engine).CacheHit, $"cache hit {(hit ? "should" : "shouldn't")} occur.");
        }

        /// <summary>
        /// Check that a cache hit must short-circuit the
        /// HTTP call entirely. Two identical Process() calls must result in
        /// exactly ONE request reaching the cloud json endpoint. A refactor that
        /// kept the flag but dropped the short-circuit would pass the existing
        /// tests but fail this one.
        /// </summary>
        [TestMethod]
        public void ValidateCacheHitOnly_NoHttpRequestOnRepeat()
        {
            string resourceKey = "resource_key";
            string userAgent = "iPhone";

            _jsonResponse = @"{ ""device"": { ""ismobile"": true } }";
            ConfigureMockedClient(r => true);

            var engine = new CloudRequestEngineBuilder(_loggerFactory, _httpClient)
                .SetResourceKey(resourceKey)
                .SetCacheSize(10)
                .SetCacheHitOrMiss(true)
                .Build();

            using (var pipeline = new PipelineBuilder(_loggerFactory)
                .AddFlowElement(engine).Build())
            {
                var data1 = pipeline.CreateFlowData();
                data1.AddEvidence("query.User-Agent", userAgent);
                data1.Process();
                Assert.IsFalse(
                    data1.GetFromElement(engine).CacheHit,
                    "first request should miss the cache.");

                var data2 = pipeline.CreateFlowData();
                data2.AddEvidence("query.User-Agent", userAgent);
                data2.Process();
                Assert.IsTrue(
                    data2.GetFromElement(engine).CacheHit,
                    "second request should hit the cache.");
            }

            // The cache hit must short-circuit the HTTP call: only the first
            // request reaches the cloud json endpoint.
            VerifyJsonRequestCount(1);
        }

        /// <summary>
        /// Check that Evidence key names are matched case-insensitively,
        /// so 'query.User-Agent' and 'query.user-agent' with the same value 
        /// must produce the same cache key. The second request should hit the 
        /// cache and make no HTTP call.
        /// </summary>
        [TestMethod]
        public void ValidateCacheKeyCaseInsensitive()
        {
            string resourceKey = "resource_key";
            string userAgent = "iPhone";

            _jsonResponse = @"{ ""device"": { ""ismobile"": true } }";
            ConfigureMockedClient(r => true);

            var engine = new CloudRequestEngineBuilder(_loggerFactory, _httpClient)
                .SetResourceKey(resourceKey)
                .SetCacheSize(10)
                .SetCacheHitOrMiss(true)
                .Build();

            using (var pipeline = new PipelineBuilder(_loggerFactory)
                .AddFlowElement(engine).Build())
            {
                var data1 = pipeline.CreateFlowData();
                data1.AddEvidence("query.User-Agent", userAgent);
                data1.Process();
                Assert.IsFalse(
                    data1.GetFromElement(engine).CacheHit, 
                    "first request should miss the cache.");

                var data2 = pipeline.CreateFlowData();
                // Same key, differing only in case.
                data2.AddEvidence("query.user-agent", userAgent);
                data2.Process();
                Assert.IsTrue(data2.GetFromElement(engine).CacheHit,
                    "evidence key differing only in case should hit the cache.");
            }

            VerifyJsonRequestCount(1);
        }

        /// <summary>
        /// Check that the cache key is built from a sorted projection of the 
        /// evidence (FlowData.GenerateKey orders by filter order, then key),
        /// so the insertion order of evidence must not change the key. 
        /// Submitting the same two evidence values in opposite order should 
        /// hit the cache.
        /// </summary>
        [TestMethod]
        public void ValidateCacheKeyOrderInsensitive()
        {
            string resourceKey = "resource_key";
            string userAgent = "iPhone";
            string latitude = "51";

            _evidenceKeysResponse = "[ 'query.User-Agent', 'query.51d_pos_latitude' ]";
            _jsonResponse = @"{ ""device"": { ""ismobile"": true } }";
            ConfigureMockedClient(r => true);

            var engine = new CloudRequestEngineBuilder(_loggerFactory, _httpClient)
                .SetResourceKey(resourceKey)
                .SetCacheSize(10)
                .SetCacheHitOrMiss(true)
                .Build();

            using (var pipeline = new PipelineBuilder(_loggerFactory)
                .AddFlowElement(engine).Build())
            {
                var data1 = pipeline.CreateFlowData();
                data1.AddEvidence("query.User-Agent", userAgent);
                data1.AddEvidence("query.51d_pos_latitude", latitude);
                data1.Process();
                Assert.IsFalse(
                    data1.GetFromElement(engine).CacheHit,
                    "first request should miss the cache.");

                var data2 = pipeline.CreateFlowData();
                // Same values, reverse insertion order.
                data2.AddEvidence("query.51d_pos_latitude", latitude);
                data2.AddEvidence("query.User-Agent", userAgent);
                data2.Process();
                Assert.IsTrue(data2.GetFromElement(engine).CacheHit,
                    "evidence insertion order should not affect the cache key.");
            }

            VerifyJsonRequestCount(1);
        }

        /// <summary>
        /// Submitting far more distinct evidence sets than the 
        /// cache size must evict some of them, so re-submitting them all 
        /// cannot be all-hits and must generate fresh HTTP requests.
        /// Check that no regressions that disable eviction (unbounded cache)
        /// have been introduced.
        /// </summary>
        [TestMethod]
        public void ValidateCacheEviction()
        {
            string resourceKey = "resource_key";
            const int cacheSize = 2;
            // Far more distinct entries than the cache bound, so eviction is
            // guaranteed regardless of the approximate per-list behaviour.
            var userAgents = Enumerable.Range(0, 50).Select(i => $"UA-{i}").ToArray();

            _jsonResponse = @"{ ""device"": { ""ismobile"": true } }";
            ConfigureMockedClient(r => true);

            var engine = new CloudRequestEngineBuilder(_loggerFactory, _httpClient)
                .SetResourceKey(resourceKey)
                .SetCacheSize(cacheSize)
                .SetCacheHitOrMiss(true)
                .Build();

            using (var pipeline = new PipelineBuilder(_loggerFactory)
                    .AddFlowElement(engine).Build())
            {
                // First pass: distinct evidence, all miss, overflowing the cache.
                foreach (var ua in userAgents)
                {
                    var data = pipeline.CreateFlowData();
                    data.AddEvidence("query.User-Agent", ua);
                    data.Process();
                    Assert.IsFalse(data.GetFromElement(engine).CacheHit,
                        $"first sighting of '{ua}' should miss the cache.");
                }

                // Second pass: count survivors. Some must have been evicted.
                int hits = 0;
                foreach (var ua in userAgents)
                {
                    var data = pipeline.CreateFlowData();
                    data.AddEvidence("query.User-Agent", ua);
                    data.Process();
                    if (data.GetFromElement(engine).CacheHit)
                    {
                        hits++;
                    }
                }

                Assert.IsLessThan(
                    userAgents.Length,
                    hits, 
                    $"cache retained all {userAgents.Length} entries with size " +
                    $"{cacheSize}; eviction did not occur.");
            }

            // First pass has 50 misses and the second pass adds at least one 
            // more miss, so the json endpoint should see more than 50 requests.
            VerifyJsonRequestCountAtLeast(userAgents.Length + 1);
        }

        /// <summary>
        /// Check that adding one extra evidence key to an otherwise-cached
        /// request changes the cache key, so it must miss and make a second 
        /// HTTP request.
        /// </summary>
        [TestMethod]
        public void ValidateCacheMiss_OneExtraEvidenceKey()
        {
            string resourceKey = "resource_key";
            string userAgent = "iPhone";
            string latitude = "51";

            _evidenceKeysResponse = "[ 'query.User-Agent', 'query.51d_pos_latitude' ]";
            _jsonResponse = @"{ ""device"": { ""ismobile"": true } }";
            ConfigureMockedClient(r => true);

            var engine = new CloudRequestEngineBuilder(_loggerFactory, _httpClient)
                .SetResourceKey(resourceKey)
                .SetCacheSize(10)
                .SetCacheHitOrMiss(true)
                .Build();

            using (var pipeline = new PipelineBuilder(_loggerFactory)
                    .AddFlowElement(engine).Build())
            {
                var data1 = pipeline.CreateFlowData();
                data1.AddEvidence("query.User-Agent", userAgent);
                data1.Process();
                Assert.IsFalse(data1.GetFromElement(engine)
                .CacheHit, "first request should miss the cache.");

                var data2 = pipeline.CreateFlowData();
                data2.AddEvidence("query.User-Agent", userAgent);
                // One additional whitelisted evidence key.
                data2.AddEvidence("query.51d_pos_latitude", latitude);
                data2.Process();
                Assert.IsFalse(data2.GetFromElement(engine).CacheHit,
                    "adding an extra evidence key should miss the cache.");
            }

            VerifyJsonRequestCount(2);
        }

        /// <summary>
        /// Check that the same evidence key with a different value misses 
        /// the cache and makes a second HTTP request.
        /// </summary>
        [TestMethod]
        public void ValidateCacheMiss_SameKeyDifferentValue()
        {
            string resourceKey = "resource_key";

            _jsonResponse = @"{ ""device"": { ""ismobile"": true } }";
            ConfigureMockedClient(r => true);

            var engine = new CloudRequestEngineBuilder(_loggerFactory, _httpClient)
                .SetResourceKey(resourceKey)
                .SetCacheSize(10)
                .SetCacheHitOrMiss(true)
                .Build();

            using (var pipeline = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(engine).Build())
            {
                var data1 = pipeline.CreateFlowData();
                data1.AddEvidence("query.User-Agent", "iPhone");
                data1.Process();
                Assert.IsFalse(data1.GetFromElement(engine)
                .CacheHit, "first request should miss the cache.");

                var data2 = pipeline.CreateFlowData();
                data2.AddEvidence("query.User-Agent", "Samsung");
                data2.Process();
                Assert.IsFalse(data2.GetFromElement(engine).CacheHit,
                    "same key with a different value should miss the cache.");
            }

            VerifyJsonRequestCount(2);
        }

        /// <summary>
        /// Verify that the cloud json endpoint received exactly
        /// <paramref name="times"/> POST requests. The mock distinguishes the
        /// three cloud endpoints by URL suffix; only '...json' requests are
        /// counted here, so the 'evidencekeys'/'accessibleproperties' calls
        /// made during Build() are excluded.
        /// </summary>
        private void VerifyJsonRequestCount(int times)
        {
            _handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(times),
               ItExpr.Is<HttpRequestMessage>(req =>
                  req.Method == HttpMethod.Post
                  && req.RequestUri.AbsolutePath.ToLower().EndsWith("json")),
               ItExpr.IsAny<CancellationToken>()
            );
        }

        /// <summary>
        /// As <see cref="VerifyJsonRequestCount(int)"/> but asserts a lower
        /// bound, for cases where the exact count is non-deterministic.
        /// </summary>
        private void VerifyJsonRequestCountAtLeast(int times)
        {
            _handlerMock.Protected().Verify(
               "SendAsync",
               Times.AtLeast(times),
               ItExpr.Is<HttpRequestMessage>(req =>
                  req.Method == HttpMethod.Post
                  && req.RequestUri.AbsolutePath.ToLower().EndsWith("json")),
               ItExpr.IsAny<CancellationToken>()
            );
        }

        /// <summary>
        /// Verify that the request to the cloud service will contain
        /// the configured origin header value.
        /// </summary>
        [TestMethod]
        public void OriginHeader()
        {
            string resourceKey = "resource_key";
            string origin = "51degrees.com";
            string userAgent = "test";

            ConfigureMockedClient(r => true);
            var engine = new CloudRequestEngineBuilder(_loggerFactory, _httpClient)
                .SetResourceKey(resourceKey)
                .SetCloudRequestOrigin(origin)
                .Build();

            using (var pipeline = new PipelineBuilder(_loggerFactory).AddFlowElement(engine).Build())
            {
                var data = pipeline.CreateFlowData();
                data.AddEvidence("query.User-Agent", userAgent);

                data.Process();
            }

            _handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(1), // we expected a single external request
               ItExpr.Is<HttpRequestMessage>(req =>
                  req.Method == HttpMethod.Post  // we expected a POST request
                  // The origin header must contain the expected value
                  && ((req.Content.Headers.Contains(Constants.ORIGIN_HEADER_NAME)
                      && req.Content.Headers.GetValues(Constants.ORIGIN_HEADER_NAME).Contains(origin)) ||
                      (req.Headers.Contains(Constants.ORIGIN_HEADER_NAME)
                      && req.Headers.GetValues(Constants.ORIGIN_HEADER_NAME).Contains(origin)))
               ),
               ItExpr.IsAny<CancellationToken>()
            );
        }

        /// <summary>
        /// Check that errors from the cloud service will cause the
        /// appropriate data to be set in the CloudRequestException.
        /// Build performs the discovery requests against the cloud
        /// service, so the exception is expected from Build.
        /// </summary>
        [TestMethod]
        public void ValidateErrorHandling_HttpDataSetInException()
        {
            string resourceKey = "resource_key";

            try
            {
                var engine = new CloudRequestEngineBuilder(
                    _loggerFactory,
                    new HttpClient())
                    .SetResourceKey(resourceKey)
                    .Build();
                Assert.Fail("Expected exception did not occur");
            }
            catch (CloudRequestException ex)
            {
                Assert.IsGreaterThan(0, ex.HttpStatusCode, "Status code should not be 0");
                Assert.IsNotNull(ex.ResponseHeaders, "Response headers not populated");
                Assert.IsNotEmpty(ex.ResponseHeaders, "Response headers not populated");
            }
        }

        /// <summary>
        /// Verify that an exception throw by the task that is returned by HttpClient.SendAsync
        /// will be handled and wrapped in nice informative CloudRequestException. 
        /// </summary>
        [TestMethod]
        public void ValidateErrorHandling_ExceptionInRequestTask()
        {
            string resourceKey = "resource_key";
            string userAgent = "iPhone";
            Exception exception = null;

            ConfigureMockedClient(r => true, true);
            var engine = new CloudRequestEngineBuilder(
                _loggerFactory,
                _httpClient)
                .SetResourceKey(resourceKey)
                .Build();

            try
            {
                using var pipeline = new PipelineBuilder(_loggerFactory).AddFlowElement(engine).Build();
                var data = pipeline.CreateFlowData();
                data.AddEvidence("query.User-Agent", userAgent);
                data.Process();
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.IsNotNull(exception, "Expected exception to occur");
            Assert.IsInstanceOfType(exception, typeof(AggregateException));
            var aggEx = exception as AggregateException;
            Assert.HasCount(1, aggEx.InnerExceptions);
            var realEx = aggEx.InnerExceptions[0];
            Assert.IsInstanceOfType(realEx, typeof(CloudRequestException));
        }

        /// <summary>
        /// Verify that an exception throw by the task that is returned by HttpClient.SendAsync
        /// will be handled and wrapped in nice informative CloudRequestException. 
        /// </summary>
        [TestMethod]
        public void ValidateErrorHandling_ExceptionInRequestTaskSuppressed()
        {
            string resourceKey = "resource_key";
            string userAgent = "iPhone";

            ConfigureMockedClient(r => true, true);
            var engine = new CloudRequestEngineBuilder(
                _loggerFactory, 
                _httpClient)
                .SetResourceKey(resourceKey)
                .Build();


            using var pipeline = new PipelineBuilder(_loggerFactory).AddFlowElement(engine).SetSuppressProcessExceptions(true).Build();
            var data = pipeline.CreateFlowData();
            data.AddEvidence("query.User-Agent", userAgent);
            data.Process();

            Assert.IsNotNull(data.Errors);
            Assert.HasCount(1, data.Errors);
            var error = data.Errors[0];
            Assert.IsInstanceOfType<FlowElements.CloudRequestEngine>(error.FlowElement);
            var exception = error.ExceptionData;

            Assert.IsNotNull(exception, "Expected exception to occur");
            Assert.IsInstanceOfType<CloudRequestException>(exception);
        }

        /// <summary>
        /// Verify that Build fetches the accessible properties and evidence
        /// keys from the cloud service exactly once, and that processing
        /// does not trigger any further discovery requests.
        /// </summary>
        [TestMethod]
        public void Build_PerformsDiscovery_ProcessMakesNoFurtherDiscoveryRequests()
        {
            string resourceKey = "resource_key";
            ConfigureMockedClient(r => true);

            var engine = new CloudRequestEngineBuilder(
                _loggerFactory,
                _httpClient)
                .SetResourceKey(resourceKey)
                .Build();

            // Build must have called each discovery endpoint exactly once.
            VerifyDiscoveryRequests(Times.Exactly(1));

            using (var pipeline = new PipelineBuilder(_loggerFactory).AddFlowElement(engine).Build())
            {
                var data = pipeline.CreateFlowData();
                data.AddEvidence("query.User-Agent", "iPhone");

                data.Process();
            }

            // Processing must not have triggered any further discovery
            // requests.
            VerifyDiscoveryRequests(Times.Exactly(1));
            _handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(1),
               ItExpr.Is<HttpRequestMessage>(req =>
                  req.Method == HttpMethod.Post
                  && req.RequestUri == expectedUri
               ),
               ItExpr.IsAny<CancellationToken>()
            );
        }

        /// <summary>
        /// Verify that a transient failure (5xx) from the discovery
        /// endpoints does not cause Build to throw, and that the
        /// discovery requests are retried and succeed when the engine
        /// is used after the cloud service has recovered.
        /// </summary>
        [TestMethod]
        public void Build_TransientDiscoveryError_ReturnsEngine_AndRetriesOnFirstUse()
        {
            string resourceKey = "resource_key";

            _accessiblePropertiesResponseStatus = HttpStatusCode.ServiceUnavailable;
            _evidenceKeysResponseStatus = HttpStatusCode.ServiceUnavailable;

            ConfigureMockedClient(r => true);

            // Build must succeed despite the cloud service being
            // temporarily unavailable.
            var engine = new CloudRequestEngineBuilder(
                _loggerFactory,
                _httpClient)
                .SetResourceKey(resourceKey)
                .Build();

            Assert.IsNotNull(engine);
            Assert.IsFalse(engine.IsDisposed);
            VerifyDiscoveryRequests(Times.Exactly(1));

            // The cloud service comes back up.
            _accessiblePropertiesResponseStatus = HttpStatusCode.OK;
            _evidenceKeysResponseStatus = HttpStatusCode.OK;

            // First use must retry the discovery requests and succeed.
            // Note that Process does not use EvidenceKeyFilter itself
            // (GetContent sends all evidence), so access both values the
            // way a consumer such as the web integration would.
            Assert.IsTrue(engine.PublicProperties.Count > 0);
            Assert.IsNotNull(engine.EvidenceKeyFilter);
            using (var pipeline = new PipelineBuilder(_loggerFactory).AddFlowElement(engine).Build())
            {
                var data = pipeline.CreateFlowData();
                data.AddEvidence("query.User-Agent", "iPhone");

                data.Process();

                var result = data.GetFromElement(engine).JsonResponse;
                Assert.AreEqual("{'device':{'value':'1'}}", result);
            }

            // One failed attempt at Build plus one successful retry.
            VerifyDiscoveryRequests(Times.Exactly(2));
        }

        /// <summary>
        /// Verify that Build does not throw when the cloud service is
        /// unreachable at the network level (for example a DNS failure or
        /// connection refused - the scenario from issue #44), so that a
        /// temporary cloud outage cannot prevent application startup.
        /// </summary>
        [TestMethod]
        public void Build_CloudUnreachable_ReturnsEngine()
        {
            string resourceKey = "resource_key";

            _handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            _handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get),
                  ItExpr.IsAny<CancellationToken>()
               )
               .ThrowsAsync(new HttpRequestException(
                   "The remote name could not be resolved: 'cloud.51degrees.com'"));
            _httpClient = new HttpClient(_handlerMock.Object);

            var engine = new CloudRequestEngineBuilder(
                _loggerFactory,
                _httpClient)
                .SetResourceKey(resourceKey)
                .Build();

            Assert.IsNotNull(engine);
            Assert.IsFalse(engine.IsDisposed);
        }

        /// <summary>
        /// Document the interaction between a transient warmup failure and
        /// an aggressive recovery configuration: the failed warmup attempts
        /// count towards the failures-to-enter-recovery threshold, so first
        /// use within the recovery period throws
        /// <see cref="CloudRequestEngineTemporarilyUnavailableException"/>
        /// rather than retrying, and the engine heals once the recovery
        /// period has passed.
        /// </summary>
        [TestMethod]
        public void Build_TransientError_AggressiveRecovery_HealsAfterRecoveryPeriod()
        {
            string resourceKey = "resource_key";

            _accessiblePropertiesResponseStatus = HttpStatusCode.ServiceUnavailable;
            _evidenceKeysResponseStatus = HttpStatusCode.ServiceUnavailable;

            ConfigureMockedClient(r => true);

            var engine = new CloudRequestEngineBuilder(
                _loggerFactory,
                _httpClient)
                .SetResourceKey(resourceKey)
                .SetRecoverySeconds(0.2)
                .SetFailuresToEnterRecovery(1)
                .Build();

            // The cloud service comes back up immediately...
            _accessiblePropertiesResponseStatus = HttpStatusCode.OK;
            _evidenceKeysResponseStatus = HttpStatusCode.OK;

            // ...but the engine is still within the recovery period, so
            // use throws rather than retrying.
            Assert.ThrowsExactly<CloudRequestEngineTemporarilyUnavailableException>(() =>
            {
                var _ = engine.PublicProperties;
            });

            // Once the recovery period has passed, the engine heals.
            Thread.Sleep(millisecondsTimeout: 400);
            Assert.IsTrue(engine.PublicProperties.Count > 0);
        }

        /// <summary>
        /// Verify that a definitive failure (4xx) from the evidence keys
        /// endpoint also causes Build to throw, not just a failure from
        /// the accessible properties endpoint.
        /// </summary>
        [TestMethod]
        public void Build_EvidenceKeysError_Throws()
        {
            string resourceKey = "resource_key";

            _evidenceKeysResponse = "Status code: 404, '*evidencekeys' method not found";
            _evidenceKeysResponseStatus = HttpStatusCode.NotFound;

            ConfigureMockedClient(_ => true);

            Assert.ThrowsExactly<CloudRequestException>(() =>
            {
                var engine = new CloudRequestEngineBuilder(
                    _loggerFactory,
                    _httpClient)
                    .SetResourceKey(resourceKey)
                    .Build();
            });
        }

        /// <summary>
        /// Verify that when the discovery requests fail definitively (4xx),
        /// the engine that Build created internally is disposed before the
        /// exception is thrown to the caller.
        /// </summary>
        [TestMethod]
        public void Build_Failure_DisposesEngine()
        {
            string resourceKey = "resource_key";

            _accessiblePropertiesResponse = @"{ ""errors"":[""resource_key not a valid resource key""]}";
            _accessiblePropertiesResponseStatus = HttpStatusCode.BadRequest;

            ConfigureMockedClient(_ => true);

            var builder = new EngineCapturingBuilder(
                _loggerFactory,
                _httpClient);

            Assert.ThrowsExactly<CloudRequestException>(() =>
            {
                var engine = builder
                    .SetResourceKey(resourceKey)
                    .Build();
            });

            Assert.IsNotNull(builder.LastEngine,
                "The engine should have been created before warmup failed.");
            Assert.IsTrue(builder.LastEngine.IsDisposed,
                "The engine should have been disposed when warmup failed.");
        }

        /// <summary>
        /// Builder that captures the engine instance it creates so that
        /// tests can inspect the engine even when Build throws.
        /// </summary>
        private class EngineCapturingBuilder : CloudRequestEngineBuilder
        {
            public FlowElements.CloudRequestEngine LastEngine { get; private set; }

            public EngineCapturingBuilder(
                ILoggerFactory loggerFactory,
                HttpClient httpClient)
                : base(loggerFactory, httpClient)
            {
            }

            protected override FlowElements.CloudRequestEngine NewEngine(
                List<string> properties)
            {
                LastEngine = base.NewEngine(properties);
                return LastEngine;
            }
        }

        /// <summary>
        /// Verify the number of requests made to each of the two
        /// discovery endpoints.
        /// </summary>
        private void VerifyDiscoveryRequests(Times times)
        {
            _handlerMock.Protected().Verify(
               "SendAsync",
               times,
               ItExpr.Is<HttpRequestMessage>(req =>
                  req.Method == HttpMethod.Get
                  && req.RequestUri.AbsolutePath.ToLower().EndsWith("accessibleproperties")
               ),
               ItExpr.IsAny<CancellationToken>()
            );
            _handlerMock.Protected().Verify(
               "SendAsync",
               times,
               ItExpr.Is<HttpRequestMessage>(req =>
                  req.Method == HttpMethod.Get
                  && req.RequestUri.AbsolutePath.ToLower().EndsWith("evidencekeys")
               ),
               ItExpr.IsAny<CancellationToken>()
            );
        }

        /// <summary>
        /// Setup _httpClient to respond with the configured messages.
        /// </summary>
        private void ConfigureMockedClient(
            Func<HttpRequestMessage, bool> expectedJsonParameters,
            bool throwExceptionOnJsonRequest = false)
        {
            // ARRANGE
            _handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            // Set up the JSON response.
            var setup = _handlerMock
               .Protected()
               // Setup the PROTECTED method to mock
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.Is<HttpRequestMessage>(r => expectedJsonParameters(r)
                      && r.RequestUri.AbsolutePath.ToLower().EndsWith("json")),
                  ItExpr.IsAny<CancellationToken>()
               );
                        
            if (throwExceptionOnJsonRequest)
            {
                // Configure the call to the json endpoint to throw an exception.
                var task = new Task<HttpResponseMessage>(() => throw new Exception("TEST"));
                // We have to start the task or it will never actually run!
                task.Start();
                setup.Returns(task);
            } 
            else 
            { 
               // Prepare the expected response of the mocked http call
               setup.ReturnsAsync(() => new HttpResponseMessage()
                {
                    StatusCode = _jsonResponseStatus,
                    Content = new StringContent(_jsonResponse),
                })
               .Verifiable();
            }

            // Set up the evidencekeys response.
            _handlerMock
               .Protected()
               // Setup the PROTECTED method to mock
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.Is<HttpRequestMessage>(r =>
                      r.RequestUri.AbsolutePath.ToLower().EndsWith("evidencekeys")),
                  ItExpr.IsAny<CancellationToken>()
               )
               // prepare the expected response of the mocked http call
               .ReturnsAsync(() => new HttpResponseMessage()
               {
                   StatusCode = _evidenceKeysResponseStatus,
                   Content = new StringContent(_evidenceKeysResponse),
               })
               .Verifiable();
            // Set up the accessibleproperties response.
            _handlerMock
               .Protected()
               // Setup the PROTECTED method to mock
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.Is<HttpRequestMessage>(r =>
                      r.RequestUri.AbsolutePath.ToLower().EndsWith("accessibleproperties")),
                  ItExpr.IsAny<CancellationToken>()
               )
               // prepare the expected response of the mocked http call
               .ReturnsAsync(() => new HttpResponseMessage()
               {
                   StatusCode = _accessiblePropertiesResponseStatus,
                   Content = new StringContent(_accessiblePropertiesResponse),
               })
               .Verifiable();

            // use real http client with mocked handler here
            _httpClient = new HttpClient(_handlerMock.Object);
        }

        public TestContext TestContext { get; set; }
    }
}
