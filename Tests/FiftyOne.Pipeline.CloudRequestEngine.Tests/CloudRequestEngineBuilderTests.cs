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
        [ExpectedException(typeof(PipelineConfigurationException))]
        public void BuildEngine_ResourceKey_NotSet()
        {
            var cloudRequestsEngine =
                new CloudRequestEngineBuilder(
                    new LoggerFactory(), 
                    new HttpClient())
                .Build();
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

            _ = cloudRequestsEngine.PublicProperties;

            VerifyPublicPropsTimes(1);

            _ = cloudRequestsEngine.EvidenceKeyFilter;

            VerifyEvidenceKeysTimes(1);

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


            _ = cloudRequestsEngine.PublicProperties;

            VerifyPublicPropsTimes(1);

            _ = cloudRequestsEngine.EvidenceKeyFilter;

            VerifyEvidenceKeysTimes(1);

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


            _ = cloudRequestsEngine.PublicProperties;

            VerifyPublicPropsTimes(1);

            _ = cloudRequestsEngine.EvidenceKeyFilter;

            VerifyEvidenceKeysTimes(1);

            _ = cloudRequestsEngine.PublicProperties;
            _ = cloudRequestsEngine.PublicProperties;
            _ = cloudRequestsEngine.EvidenceKeyFilter;
            _ = cloudRequestsEngine.EvidenceKeyFilter;

            VerifyPublicPropsTimes(1);
            VerifyEvidenceKeysTimes(1);
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Un-set cloud URL environment variable.
            Environment.SetEnvironmentVariable(Constants.FOD_CLOUD_API_URL, "");
        }
    }
}
