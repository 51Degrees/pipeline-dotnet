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

using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.CloudRequestEngine.Tests
{
    /// <summary>
    /// Tests for the cloud request engine builder.
    /// </summary>
    [TestClass]
    public class CloudRequestEngineBuilderTests
    {
        private Mock<HttpMessageHandler> _handlerMock;

        [TestInitialize]
        public void Init()
        {
            _handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            // Mock accessible properties endpoint.
            _handlerMock
               .Protected()
               // Setup the PROTECTED method to mock
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get  // we expected a GET request
                    && req.RequestUri.Segments.Last() == Constants.PROPERTIES_FILENAME),
                  ItExpr.IsAny<CancellationToken>()
               )
               // prepare the expected response of the mocked http call
               .ReturnsAsync(new HttpResponseMessage()
               {
                   StatusCode = HttpStatusCode.OK,
                   Content = new StringContent(@"{""Products"":{""device"":{""DataTier"":""CloudV4Free"",""Properties"":[{""Name"":""IsMobile"",""Type"":""Boolean"",""Category"":""Device"",""DelayExecution"":false},{""Name"":""JavascriptHardwareProfile"",""Type"":""JavaScript"",""Category"":""Javascript"",""DelayExecution"":false}]}}}"),
               })
               .Verifiable();

            // Mock evidence keys endpoint, this is not checked but we must 
            // return a response for the call from cloud request engine.
            _handlerMock
               .Protected()
               // Setup the PROTECTED method to mock
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get  // we expected a GET request
                    && req.RequestUri.Segments.Last() == Constants.EVIDENCE_KEYS_FILENAME),
                  ItExpr.IsAny<CancellationToken>()
               )
               // prepare the expected response of the mocked http call
               .ReturnsAsync(new HttpResponseMessage()
               {
                   StatusCode = HttpStatusCode.OK,
                   Content = new StringContent(@"[
  ""fiftyone.resource-key""
]"),
               })
               .Verifiable();

            // Un-set cloud URL environment variable.
            Environment.SetEnvironmentVariable(Constants.FOD_CLOUD_API_URL, "");
        }

        /// <summary>
        /// Test exception is thrown if no resource key is specified.
        /// </summary>
        [TestMethod]
        public void BuildEngine_ResourceKey_NotSet()
        {
            Assert.ThrowsExactly<PipelineConfigurationException>(() =>
            {
                var cloudRequestsEngine =
                    new CloudRequestEngineBuilder(
                        new LoggerFactory(),
                        new HttpClient())
                    .Build();
            });
        }

        /// <summary>
        /// Test that the configured URL takes precedence over the environment
        /// variable.
        /// </summary>
        [TestMethod]
        public void Endpoint_Config_Precedence_Config()
        {
            var expectedUrl = "http://localhost/test_conf/";

            Environment.SetEnvironmentVariable(Constants.FOD_CLOUD_API_URL, "http://localhost/test_env/");

            Action<int> VerifyPublicPropsTimes = (timesExpected) => _handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(timesExpected), // we expected a single external request
               ItExpr.Is<HttpRequestMessage>(req =>
                  req.Method == HttpMethod.Get  // we expected a GET request
                  && req.RequestUri.GetLeftPart(UriPartial.Path) == expectedUrl + Constants.PROPERTIES_FILENAME // to this uri
               ),
               ItExpr.IsAny<CancellationToken>()
            );

            Action<int> VerifyEvidenceKeysTimes = (timesExpected) => _handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(timesExpected), // we expected a single external request
               ItExpr.Is<HttpRequestMessage>(req =>
                  req.Method == HttpMethod.Get  // we expected a GET request
                  && req.RequestUri.GetLeftPart(UriPartial.Path) == expectedUrl + Constants.EVIDENCE_KEYS_FILENAME // to this uri
               ),
               ItExpr.IsAny<CancellationToken>()
            );

            VerifyPublicPropsTimes(0);
            VerifyEvidenceKeysTimes(0);

            var cloudRequestsEngine =
                new CloudRequestEngineBuilder(
                    new LoggerFactory(), 
                    new HttpClient(_handlerMock.Object))
                .SetEndPoint(expectedUrl)
                .SetResourceKey("abcdefgh")
                .Build();

            // Build warms the engine, so both discovery endpoints have
            // already been called exactly once.
            VerifyPublicPropsTimes(1);
            VerifyEvidenceKeysTimes(1);

            // Further accesses must not trigger any additional requests.
            _ = cloudRequestsEngine.PublicProperties;
            _ = cloudRequestsEngine.PublicProperties;
            _ = cloudRequestsEngine.EvidenceKeyFilter;
            _ = cloudRequestsEngine.EvidenceKeyFilter;

            VerifyPublicPropsTimes(1);
            VerifyEvidenceKeysTimes(1);
        }

        /// <summary>
        /// Test that the cloud URL is retrieved from the environment variable 
        /// if specified.
        /// </summary>
        [TestMethod]
        public void Endpoint_Config_Precedence_EnvironmentVariable()
        {
            var expectedUrl = "http://localhost/test_env/";

            Environment.SetEnvironmentVariable(Constants.FOD_CLOUD_API_URL, expectedUrl);

            Action<int> VerifyPublicPropsTimes = (timesExpected) => _handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(timesExpected), // we expected a single external request
               ItExpr.Is<HttpRequestMessage>(req =>
                  req.Method == HttpMethod.Get  // we expected a GET request
                  && req.RequestUri.GetLeftPart(UriPartial.Path) == expectedUrl + Constants.PROPERTIES_FILENAME // to this uri
               ),
               ItExpr.IsAny<CancellationToken>()
            );

            Action<int> VerifyEvidenceKeysTimes = (timesExpected) => _handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(timesExpected), // we expected a single external request
               ItExpr.Is<HttpRequestMessage>(req =>
                  req.Method == HttpMethod.Get  // we expected a GET request
                  && req.RequestUri.GetLeftPart(UriPartial.Path) == expectedUrl + Constants.EVIDENCE_KEYS_FILENAME // to this uri
               ),
               ItExpr.IsAny<CancellationToken>()
            );

            VerifyPublicPropsTimes(0);
            VerifyEvidenceKeysTimes(0);

            var cloudRequestsEngine =
                new CloudRequestEngineBuilder(
                    new LoggerFactory(), 
                    new HttpClient(_handlerMock.Object))
                .SetResourceKey("abcdefgh")
                .Build();


            // Build warms the engine, so both discovery endpoints have
            // already been called exactly once.
            VerifyPublicPropsTimes(1);
            VerifyEvidenceKeysTimes(1);

            // Further accesses must not trigger any additional requests.
            _ = cloudRequestsEngine.PublicProperties;
            _ = cloudRequestsEngine.PublicProperties;
            _ = cloudRequestsEngine.EvidenceKeyFilter;
            _ = cloudRequestsEngine.EvidenceKeyFilter;

            VerifyPublicPropsTimes(1);
            VerifyEvidenceKeysTimes(1);
        }

        /// <summary>
        /// Test that the default cloud endpoint is used when no other is 
        /// specified.
        /// </summary>
        [TestMethod]
        public void Endpoint_Config_Precedence_Default()
        {
            Action<int> VerifyPublicPropsTimes = (timesExpected) => _handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(timesExpected), // we expected a single external request
               ItExpr.Is<HttpRequestMessage>(req =>
                  req.Method == HttpMethod.Get  // we expected a GET request
                  && req.RequestUri.GetLeftPart(UriPartial.Path) == Constants.PROPERTIES_ENDPOINT_DEFAULT // to this uri
               ),
               ItExpr.IsAny<CancellationToken>()
            );

            Action<int> VerifyEvidenceKeysTimes = (timesExpected) => _handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(timesExpected), // we expected a single external request
               ItExpr.Is<HttpRequestMessage>(req =>
                  req.Method == HttpMethod.Get  // we expected a GET request
                  && req.RequestUri.GetLeftPart(UriPartial.Path) == Constants.EVIDENCE_KEYS_ENDPOINT_DEFAULT // to this uri
               ),
               ItExpr.IsAny<CancellationToken>()
            );

            VerifyPublicPropsTimes(0);
            VerifyEvidenceKeysTimes(0);

            var cloudRequestsEngine =
                new CloudRequestEngineBuilder(
                    new LoggerFactory(),
                    new HttpClient(_handlerMock.Object))
                .SetResourceKey("abcdefgh")
                .Build();


            // Build warms the engine, so both discovery endpoints have
            // already been called exactly once.
            VerifyPublicPropsTimes(1);
            VerifyEvidenceKeysTimes(1);

            // Further accesses must not trigger any additional requests.
            _ = cloudRequestsEngine.PublicProperties;
            _ = cloudRequestsEngine.PublicProperties;
            _ = cloudRequestsEngine.EvidenceKeyFilter;
            _ = cloudRequestsEngine.EvidenceKeyFilter;

            VerifyPublicPropsTimes(1);
            VerifyEvidenceKeysTimes(1);
        }

        /// <summary>
        /// The default circuit breaker sensitivity must stay in the range
        /// that only trips on a genuine outage: a high failure count over a
        /// short window (a failure rate), not a low count over a long one.
        /// The default count must also remain a configurable value (within
        /// the allowed min/max), not sit on the ceiling.
        /// </summary>
        [TestMethod]
        public void FailuresToEnterRecovery_Defaults_RequireSustainedFailureRate()
        {
            Assert.IsTrue(
                Constants.CLOUD_REQUEST_FAILURES_TO_ENTER_RECOVERY_DEFAULT >= 100,
                "The default failure count is too low; a small count lets " +
                "stray timeouts on a healthy cloud trip the breaker.");
            Assert.IsTrue(
                Constants.CLOUD_REQUEST_FAILURES_WINDOW_SECONDS_DEFAULT <= 10,
                "The default window is too long; a long window lets failures " +
                "accumulate slowly rather than requiring an outage-like rate.");
            Assert.IsTrue(
                Constants.CLOUD_REQUEST_FAILURES_TO_ENTER_RECOVERY_DEFAULT
                    < Constants.CLOUD_REQUEST_FAILURES_TO_ENTER_RECOVERY_MAX,
                "The default failure count must leave headroom below the " +
                "maximum so a less sensitive breaker can still be configured.");
            Assert.IsTrue(
                Constants.CLOUD_REQUEST_RECOVERY_SECONDS_DEFAULT <= 10,
                "The default recovery period is too long; a trip should " +
                "suppress requests only briefly before probing the cloud again.");

            // The default must be a value the builder accepts (does not
            // exceed the max), so an engine can be built without overriding
            // it.
            _ = new CloudRequestEngineBuilder(
                    new LoggerFactory(), new HttpClient(_handlerMock.Object))
                .SetResourceKey("abcdefgh")
                .SetFailuresToEnterRecovery(
                    Constants.CLOUD_REQUEST_FAILURES_TO_ENTER_RECOVERY_DEFAULT)
                .Build();
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Un-set cloud URL environment variable.
            Environment.SetEnvironmentVariable(Constants.FOD_CLOUD_API_URL, "");
        }
    }
}
