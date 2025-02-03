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

using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.Engines.Trackers;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Xml;
using FiftyOne.Common.TestHelpers;
using FiftyOne.Pipeline.Engines.TestHelpers;
using System;
using System.Linq;
using System.Threading;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Tests.FlowElements
{
    [TestClass]
    public class ShareUsageElementTests
    {
        private SequenceElement _sequenceElement;

        // Share usage instance that is being tested
        private ShareUsageElement _shareUsageElement;

        // Mocks and dependencies
        private TestLogger<ShareUsageElement> _logger;
        private Mock<MockHttpMessageHandler> _httpHandler;
        private Mock<IPipeline> _pipeline;
        private Mock<ITracker> _tracker;
        private HttpClient _httpClient;

        // Test instance data.
        private List<string> _xmlContent = new List<string>();

        /// <summary>
        /// Initialise the test instance.
        /// </summary>
        [TestInitialize]
        public void Init()
        {
            _logger = new TestLogger<ShareUsageElement>();

            // Create the HttpClient using the mock handler
            _httpHandler = new Mock<MockHttpMessageHandler>() { CallBase = true };
            _httpClient = new HttpClient(_httpHandler.Object);

            // Configure the mock handler to store the XML content of requests
            // in the _xmlContent list and return an 'OK' status code.
            _httpHandler.Setup(h => h.Send(It.IsAny<HttpRequestMessage>()))
                .Callback((HttpRequestMessage request) =>
                {
                    StoreRequestXml(request);
                })
                .Returns(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("<empty />", Encoding.UTF8, "application/xml"),
                });

            // Configure the pipeline to return an empty list of flow elements
            _pipeline = new Mock<IPipeline>();
            _pipeline.Setup(p => p.FlowElements).Returns(
                new ReadOnlyCollection<IFlowElement>(new List<IFlowElement>()));

            // Configure the tracker to always allow sharing.
            _tracker = new Mock<ITracker>();
            _tracker.Setup(t => t.Track(It.IsAny<IFlowData>())).Returns(true);
        }

        /// <summary>
        /// Helper method used to create a share usage instance.
        /// </summary>
        private void CreateShareUsage(double sharePercentage,
            int minimumEntriesPerMessage,
            int interval,
            List<string> blockedHeaders,
            List<string> includedQueryStringParams, 
            List<KeyValuePair<string, string>> ignoreDataEvidenceFiler,
            bool shareAll = false)
        {
            _sequenceElement = new SequenceElement(new Mock<ILogger<SequenceElement>>().Object);
            _sequenceElement.AddPipeline(_pipeline.Object);
            _shareUsageElement = new ShareUsageElement(
                _logger,
                _httpClient,
                sharePercentage,
                minimumEntriesPerMessage,
                minimumEntriesPerMessage * 2,
                100,
                100,
				interval,
				true,
                "http://51Degrees.com/test",
                blockedHeaders,
                includedQueryStringParams,
                ignoreDataEvidenceFiler,
                Engines.Constants.DEFAULT_ASP_COOKIE_NAME,
                _tracker.Object,
                shareAll);
            _shareUsageElement.AddPipeline(_pipeline.Object);
        }

        /// <summary>
        /// Test that the ShareUsageElement behaves as expected when it is 
        /// configured to send data after a single event.
        /// Check that client IP and headers from the evidence are included
        /// in the XML.
        /// </summary>
        [TestMethod]
        public void ShareUsageElement_SingleEvent_ClientIPAndHeader()
        {
            // Arrange
            CreateShareUsage(1, 1, 1, new List<string>(), new List<string>(), new List<KeyValuePair<string, string>>());

            Dictionary<string, object> evidenceData = new Dictionary<string, object>()
            {
                { Core.Constants.EVIDENCE_CLIENTIP_KEY, "1.2.3.4" },
                { Core.Constants.EVIDENCE_HTTPHEADER_PREFIX + Core.Constants.EVIDENCE_SEPERATOR + "x-forwarded-for", "5.6.7.8" },
                { Core.Constants.EVIDENCE_HTTPHEADER_PREFIX + Core.Constants.EVIDENCE_SEPERATOR + "forwarded-for", "2001::" },
                { Core.Constants.EVIDENCE_COOKIE_PREFIX + Core.Constants.EVIDENCE_SEPERATOR + Engines.Constants.FIFTYONE_COOKIE_PREFIX + "Profile", "123456" },
                { Core.Constants.EVIDENCE_COOKIE_PREFIX + Core.Constants.EVIDENCE_SEPERATOR + "RemoveMe", "123456" }
            };
            var data = MockFlowData.CreateFromEvidence(evidenceData, false);

            // Act
            _shareUsageElement.Process(data.Object);
            // Wait for the consumer task to finish.
            Assert.IsNotNull(_shareUsageElement.SendDataTask);
            _shareUsageElement.SendDataTask.Wait();

            // Assert
            // Check that one and only one HTTP message was sent
            _httpHandler.Verify(h => h.Send(It.IsAny<HttpRequestMessage>()), Times.Once);
            Assert.AreEqual(1, _xmlContent.Count);

            // Validate that the XML is well formed by passing it through a reader
            using (XmlReader xr = XmlReader.Create(new StringReader(_xmlContent[0])))
            {
                while (xr.Read()) { }
            }
            // Check that the expected values are populated.
            Assert.IsTrue(_xmlContent[0].Contains("<ClientIP>1.2.3.4</ClientIP>"));
            Assert.IsTrue(_xmlContent[0].Contains("<header Name=\"x-forwarded-for\"><![CDATA[5.6.7.8]]></header>"));
            Assert.IsTrue(_xmlContent[0].Contains("<header Name=\"forwarded-for\"><![CDATA[2001::]]></header>"));
            Assert.IsTrue(_xmlContent[0].Contains($"<cookie Name=\"{Engines.Constants.FIFTYONE_COOKIE_PREFIX}Profile\"><![CDATA[123456]]></cookie>"));
            Assert.IsFalse(_xmlContent[0].Contains("<cookie Name=\"RemoveMe\">"));
        }

        /// <summary>
        /// Test that the ShareUsageElement behaves as expected when it is 
        /// configured to send data after two events and just one event is 
        /// logged.
        /// </summary>
        [TestMethod]
        public void ShareUsageElement_TwoEvents_FirstEvent()
        {
            // Arrange
            CreateShareUsage(1, 2, 1, new List<string>(), new List<string>(), new List<KeyValuePair<string, string>>());

            Dictionary<string, object> evidenceData = new Dictionary<string, object>()
            {
                { Core.Constants.EVIDENCE_CLIENTIP_KEY, "1.2.3.4" },
            };
            var data = MockFlowData.CreateFromEvidence(evidenceData, false);

            // Act
            _shareUsageElement.Process(data.Object);

            // Assert
            // Check that no HTTP messages were sent.
            _httpHandler.Verify(h => h.Send(It.IsAny<HttpRequestMessage>()), Times.Never);
        }

        /// <summary>
        /// Test that the ShareUsageElement behaves as expected when it is 
        /// configured to send data after two events and two events are 
        /// logged.
        /// </summary>
        [TestMethod]
        public void ShareUsageElement_TwoEvents_SecondEvent()
        {
            // Arrange
            CreateShareUsage(1, 2, 1, new List<string>(), new List<string>(), new List<KeyValuePair<string, string>>());

            Dictionary<string, object> evidenceData = new Dictionary<string, object>()
            {
                { Core.Constants.EVIDENCE_CLIENTIP_KEY, "1.2.3.4" },
            };
            var data = MockFlowData.CreateFromEvidence(evidenceData, false);

            // Act
            _shareUsageElement.Process(data.Object);
            _shareUsageElement.Process(data.Object);

            // Wait for the consumer task to finish.
            Assert.IsNotNull(_shareUsageElement.SendDataTask);
            _shareUsageElement.SendDataTask.Wait();

            // Assert
            // Check that one and only one HTTP message was sent.
            _httpHandler.Verify(h => h.Send(It.IsAny<HttpRequestMessage>()), Times.Once);
            Assert.AreEqual(1, _xmlContent.Count);

            // Validate that the XML is well formed by passing it through a reader
            using (XmlReader xr = XmlReader.Create(new StringReader(_xmlContent[0])))
            {
                while (xr.Read()) { }
            }
            // Make sure there are 2 'Device' nodes
            int count = 0;
            int index = 0;
            while (index >= 0)
            {
                index = _xmlContent[0].IndexOf("<Device>", index + 1);
                if (index > 0) { count++; }
            }
            Assert.AreEqual(2, count);
        }

        /// <summary>
        /// Test that the ShareUsageElement behaves as expected when using
        /// the 'restricted headers' option.
        /// This should prevent all but a few HTTP headers from being 
        /// included in the data.
        /// </summary>
        [TestMethod]
        public void ShareUsageElement_RestrictedHeaders()
        {
            // Arrange
            CreateShareUsage(1, 1, 1, new List<string>() { "x-forwarded-for", "forwarded-for" }, new List<string>(), new List<KeyValuePair<string, string>>());

            string useragent = "Mozilla/5.0 (Windows NT 6.1; Win64; x64; rv:47.0) Gecko/20100101 Firefox/47.0 Mozilla/5.0 (Macintosh; Intel Mac OS X x.y; rv:42.0) Gecko/20100101 Firefox/42.0.";
            Dictionary<string, object> evidenceData = new Dictionary<string, object>()
            {
                { Core.Constants.EVIDENCE_CLIENTIP_KEY, "1.2.3.4" },
                { Core.Constants.EVIDENCE_HTTPHEADER_PREFIX + Core.Constants.EVIDENCE_SEPERATOR + "x-forwarded-for", "5.6.7.8" },
                { Core.Constants.EVIDENCE_HTTPHEADER_PREFIX + Core.Constants.EVIDENCE_SEPERATOR + "forwarded-for", "2001::" },
                { Core.Constants.EVIDENCE_HTTPHEADER_PREFIX + Core.Constants.EVIDENCE_SEPERATOR + "user-agent", useragent },
            };
            var data = MockFlowData.CreateFromEvidence(evidenceData, false);

            // Act
            _shareUsageElement.Process(data.Object);
            // Wait for the consumer task to finish.
            Assert.IsNotNull(_shareUsageElement.SendDataTask);
            _shareUsageElement.SendDataTask.Wait();

            // Assert
            // Check that one and only one HTTP message was sent.
            _httpHandler.Verify(h => h.Send(It.IsAny<HttpRequestMessage>()), Times.Once);
            Assert.AreEqual(1, _xmlContent.Count);

            // Validate that the XML is well formed by passing it through a reader
            using (XmlReader xr = XmlReader.Create(new StringReader(_xmlContent[0])))
            {
                while (xr.Read()) { }
            }
            // Check that the expected values are populated.
            Assert.IsTrue(_xmlContent[0].Contains("<ClientIP>1.2.3.4</ClientIP>"));
            Assert.IsTrue(_xmlContent[0].Contains($"<header Name=\"user-agent\"><![CDATA[{useragent}]]></header>"));
            Assert.IsFalse(_xmlContent[0].Contains("<header Name=\"x-forwarded-for\">"));
            Assert.IsFalse(_xmlContent[0].Contains("<header Name=\"forwarded-for\">"));
        }

        /// <summary>
        /// Test that the ShareUsageElement behaves as expected when it is 
        /// configured to share a low percentage of requests.
        /// </summary>
        [TestMethod]
        public void ShareUsageElement_LowPercentage()
        {
            // Arrange
            CreateShareUsage(0.001, 100, 1, new List<string>(), new List<string>(), new List<KeyValuePair<string, string>>());

            Dictionary<string, object> evidenceData = new Dictionary<string, object>()
            {
                { Core.Constants.EVIDENCE_CLIENTIP_KEY, "1.2.3.4" },
            };
            var data = MockFlowData.CreateFromEvidence(evidenceData, false);

            // Act
            int requiredEvents = 0;
            while (_xmlContent.Count == 0 &&
                requiredEvents <= 1000000)
            {
                _shareUsageElement.Process(data.Object);
                if (requiredEvents % 200000 == 0) {
                    Thread.Sleep(1000);
                }
                requiredEvents++;
            }
            // Wait for the consumer task to finish.
            Assert.IsNotNull(_shareUsageElement.SendDataTask);
            _shareUsageElement.SendDataTask.Wait();

            // Assert
            // On average, the number of required events should be around 
            // 100,000. However, as it's chance based it can vary 
            // significantly. We only want to catch any gross errors so just
            // make sure the value is of the expected order of magnitude.
            Console.WriteLine($"requiredEvents = {requiredEvents}");
            Assert.IsTrue(requiredEvents > 10000, $"Expected the number of required " +
                $"events to be at least 10,000, but was actually '{requiredEvents}'");
            Assert.IsTrue(requiredEvents < 1000000, $"Expected the number of required " +
                $"events to be less than 1,000,000, but was actually '{requiredEvents}'");
            // Check that one and only one HTTP message was sent.
            _httpHandler.Verify(h => h.Send(It.IsAny<HttpRequestMessage>()), Times.Once);
            Assert.AreEqual(1, _xmlContent.Count);
        }

        /// <summary>
        /// Test that any data collected is sent when the element is disposed
        /// of rather than being discarded.
        /// </summary>
        [TestMethod]
        public void ShareUsageElement_SendOnCleanup()
        {
            // Arrange
            CreateShareUsage(1, 2, 1, new List<string>(), new List<string>(), new List<KeyValuePair<string, string>>());

            Dictionary<string, object> evidenceData = new Dictionary<string, object>()
            {
                { Core.Constants.EVIDENCE_CLIENTIP_KEY, "1.2.3.4" },
            };
            var data = MockFlowData.CreateFromEvidence(evidenceData, false);

            // Act
            _shareUsageElement.Process(data.Object);

            // No data should be being sending yet.
            Assert.IsNull(_shareUsageElement.SendDataTask);
            _httpHandler.Verify(h => h.Send(It.IsAny<HttpRequestMessage>()), Times.Never);

            // Dispose of the element.
            _shareUsageElement.Dispose();

            // Assert
            // Check that no HTTP messages were sent.
            _httpHandler.Verify(h => h.Send(It.IsAny<HttpRequestMessage>()), Times.Once);
        }

        /// <summary>
        /// Test that the ShareUsageElement will log a warning if errors occur when sending
        /// </summary>
        [TestMethod]
        public void ShareUsageElement_LogErrors()
        {
            // Arrange
            _httpHandler.Setup(h => h.Send(It.IsAny<HttpRequestMessage>()))
                .Returns(new HttpResponseMessage(
                    System.Net.HttpStatusCode.InternalServerError));
            CreateShareUsage(1, 1, 1, new List<string>(), new List<string>(), new List<KeyValuePair<string, string>>());

            Dictionary<string, object> evidenceData = new Dictionary<string, object>()
            {
                { Core.Constants.EVIDENCE_CLIENTIP_KEY, "1.2.3.4" },
            };
            var data = MockFlowData.CreateFromEvidence(evidenceData, false);

            // Act
            _shareUsageElement.Process(data.Object);
            // Wait for the consumer task to finish.
            Assert.IsNotNull(_shareUsageElement.SendDataTask);
            _shareUsageElement.SendDataTask.Wait();

            // Assert
            // Check that a warning was logged.
            Assert.AreEqual(1, _logger.WarningEntries.Count());
            Assert.IsTrue(_logger.WarningEntries.First().StartsWith("Failure sending usage data"));
            Console.WriteLine(_logger.WarningEntries.First());
        }

        /// <summary>
        /// Test that the ShareUsageElement does not share usage if the evidence
        /// contains any of the configured key value pairs.
        /// </summary>
        [TestMethod]
        public void ShareUsageElement_IgnoreOnEvidence()
        {
            // Arrange
            _httpHandler.Setup(h => h.Send(It.IsAny<HttpRequestMessage>()))
                .Returns(new HttpResponseMessage(
                    System.Net.HttpStatusCode.OK));
            CreateShareUsage(1, 1, 1, new List<string>(), new List<string>(),
                new List<KeyValuePair<string, string>>()
                {
                    new KeyValuePair<string, string>("header.User-Agent", "Azure Traffic Manager Endpoint Monitor")
                });

            Dictionary<string, object> evidenceData = new Dictionary<string, object>()
            {
                { Core.Constants.EVIDENCE_CLIENTIP_KEY, "1.2.3.4" },
                { "header.User-Agent", "Azure Traffic Manager Endpoint Monitor" }
            };
            var data = MockFlowData.CreateFromEvidence(evidenceData, false);

            // Act
            _shareUsageElement.Process(data.Object);
            // Check that the consumer task did not start.
            Assert.IsNull(_shareUsageElement.SendDataTask);

            // Assert
            // Check that no HTTP messages were sent.
            _httpHandler.Verify(h => h.Send(It.IsAny<HttpRequestMessage>()), Times.Never);
        }

        /// <summary>
        /// Check that the usage element can handle invalid xml chars.
        /// </summary>
        /// <param name="config"></param>
        [TestMethod]
        public void ShareUsageElement_BadSchema()
        {
            // Arrange
            CreateShareUsage(1, 1, 1, new List<string>(), new List<string>(), new List<KeyValuePair<string, string>>());

            Dictionary<string, object> evidenceData = new Dictionary<string, object>()
            {
                // Contains hidden character at the end of the string.
                // (0x0018) - Cancel control character
                { Core.Constants.EVIDENCE_HEADER_USERAGENT_KEY, "iPhone" },
            };
            var data = MockFlowData.CreateFromEvidence(evidenceData, false);

            // Act
            _shareUsageElement.Process(data.Object);
            // Wait for the consumer task to finish.
            Assert.IsNotNull(_shareUsageElement.SendDataTask);
            _shareUsageElement.SendDataTask.Wait();

            // Assert
            // Check that one and only one HTTP message was sent.
            _httpHandler.Verify(h => h.Send(It.IsAny<HttpRequestMessage>()), Times.Once);
            Assert.AreEqual(1, _xmlContent.Count);

            // Validate that the XML is well formed by passing it through a reader
            using (XmlReader xr = XmlReader.Create(new StringReader(_xmlContent[0])))
            {
                while (xr.Read()) { }
            }
            // Check that the expected values are populated.
            Assert.IsTrue(_xmlContent[0].Contains(@"iPhone\x0018"));
            Assert.IsTrue(_xmlContent[0].Contains("<BadSchema>true</BadSchema>"));

        }

        /// <summary>
        /// Test that the share usage build can handle invalid configuration for
        /// the ignoreDataEvidenceFilter.
        /// </summary>
        [DataTestMethod]
        [DataRow("user-agent=iPhone")]
        [DataRow("user-agent,iPhone")]
        [DataRow("test,iPhone,block")]
        public void ShareUsageBuilder_IgnoreData_InvalidFilter(string config)
        {
            var logger = new TestLogger();

            ShareUsageBuilder builder = new ShareUsageBuilder(new TestLoggerFactory(), logger, _httpClient);
            ShareUsageElement element = builder.SetSharePercentage(1)
                .SetMinimumEntriesPerMessage(1)
                .SetRepeatEvidenceIntervalMinutes(1)
                .SetIgnoreFlowDataEvidenceFilter(config)
                .Build();

            Assert.IsTrue(logger.WarningEntries.Count() > 0);
            Assert.IsTrue(logger.ErrorEntries.Count() == 0);
        }

        /// <summary>
        /// Test valid configuration for the ignoreDataEvidenceFilter.
        /// </summary>
        [DataTestMethod]
        [DataRow("user-agent:iPhone")]
        [DataRow("user-agent:iPhone,host:bacon.com")]
        [DataRow("user-agent:iPhone,host:bacon.com,license:ABCDEF")]
        public void ShareUsageBuilder_IgnoreData_ValidFilter(string config)
        {
            var logger = new TestLogger();
            
            ShareUsageBuilder builder = new ShareUsageBuilder(new TestLoggerFactory(), logger, _httpClient);
            ShareUsageElement element = builder.SetSharePercentage(1)
                .SetMinimumEntriesPerMessage(1)
                .SetRepeatEvidenceIntervalMinutes(1)
                .SetIgnoreFlowDataEvidenceFilter(config)
                .Build();

            Assert.IsTrue(logger.WarningEntries.Count() == 0);
            Assert.IsTrue(logger.ErrorEntries.Count() == 0);
        }

        /// <summary>
        /// Test that the ShareUsageElement generates a session id if one is not
        /// contained in the evidence and adds it to the results.
        /// </summary>
        [TestMethod]
        public void ShareUsageElement_SessionIdAndSequence_None()
        {
            // Arrange
            CreateShareUsage(1, 1, 1, new List<string>(), new List<string>(), new List<KeyValuePair<string, string>>());

            Dictionary<string, object> evidenceData = new Dictionary<string, object>()
            {
                { Core.Constants.EVIDENCE_CLIENTIP_KEY, "1.2.3.4" },
            };

            var pipeline = new TestPipeline(_pipeline.Object);
            var data = new TestFlowData(new Mock<ILogger<TestFlowData>>().Object, pipeline);
            data.AddEvidence(evidenceData);

            // Act
            _sequenceElement.Process(data);
            _shareUsageElement.Process(data);
            // Wait for the consumer task to finish.
            Assert.IsNotNull(_shareUsageElement.SendDataTask);
            _shareUsageElement.SendDataTask.Wait();

            // Assert
            // Check that one and only one HTTP message was sent
            _httpHandler.Verify(h => h.Send(It.IsAny<HttpRequestMessage>()), Times.Once);
            Assert.AreEqual(1, _xmlContent.Count);

            // Validate that the XML is well formed by passing it through a reader
            using (XmlReader xr = XmlReader.Create(new StringReader(_xmlContent[0])))
            {
                while (xr.Read()) { }
            }

            // Check that the expected values are populated.
            Assert.IsTrue(_xmlContent[0].Contains("<SessionId>"));
            Assert.IsTrue(_xmlContent[0].Contains("<Sequence>1</Sequence>"));
            Assert.IsTrue(data.GetEvidence().AsDictionary().ContainsKey(Constants.EVIDENCE_SESSIONID));
            Assert.IsTrue(data.GetEvidence().AsDictionary().ContainsKey(Constants.EVIDENCE_SEQUENCE));
        }

        /// <summary>
        /// Test that if a session id and sequence exists in the evidence the 
        /// ShareUsageElement persists the session id and increments the 
        /// sequence.
        /// </summary>
        [TestMethod]
        public void ShareUsageElement_SessionIdAndSequence_Existing()
        {
            // Arrange
            CreateShareUsage(1, 1, 1, new List<string>(), new List<string>(), new List<KeyValuePair<string, string>>());

            Dictionary<string, object> evidenceData = new Dictionary<string, object>()
            {
                { Core.Constants.EVIDENCE_CLIENTIP_KEY, "1.2.3.4" },
                { Constants.EVIDENCE_SESSIONID, "abcdefg-hijklmn-opqrst-uvwyxz" },
                { Constants.EVIDENCE_SEQUENCE, "2" },
            };

            var pipeline = new TestPipeline(_pipeline.Object);
            var data = new TestFlowData(new Logger<TestFlowData>(new LoggerFactory()), pipeline, new Evidence(new Logger<Evidence>(new LoggerFactory())));
            data.AddEvidence(evidenceData);

            // Act
            _sequenceElement.Process(data);
            _shareUsageElement.Process(data);
            // Wait for the consumer task to finish.
            Assert.IsNotNull(_shareUsageElement.SendDataTask);
            _shareUsageElement.SendDataTask.Wait();

            // Assert
            // Check that one and only one HTTP message was sent
            _httpHandler.Verify(h => h.Send(It.IsAny<HttpRequestMessage>()), Times.Once);
            Assert.AreEqual(1, _xmlContent.Count);

            // Validate that the XML is well formed by passing it through a reader
            using (XmlReader xr = XmlReader.Create(new StringReader(_xmlContent[0])))
            {
                while (xr.Read()) { }
            }

            // Check that the expected values are populated.
            Assert.IsTrue(_xmlContent[0].Contains("<SessionId>abcdefg-hijklmn-opqrst-uvwyxz</SessionId>"));
            Assert.IsTrue(_xmlContent[0].Contains("<Sequence>3</Sequence>"));
            Assert.IsTrue(data.GetEvidence().AsDictionary().ContainsKey(Constants.EVIDENCE_SESSIONID));
            Assert.IsTrue(data.GetEvidence().AsDictionary().ContainsKey(Constants.EVIDENCE_SEQUENCE));
        }

        /// <summary>
        /// Test that the share all function causes all evidence to be shared.
        /// </summary>
        [TestMethod]
        public void ShareUsageElement_ShareAll()
        {
            // Arrange
            CreateShareUsage(1, 1, 1, 
                new List<string>() { "notblocked" }, 
                new List<string>(), 
                new List<KeyValuePair<string, string>>(), true);

            Dictionary<string, object> evidenceData = new Dictionary<string, object>()
            {
                { Core.Constants.EVIDENCE_HTTPHEADER_PREFIX + Core.Constants.EVIDENCE_SEPERATOR + "notblocked", "abc" },
                { Core.Constants.EVIDENCE_QUERY_PREFIX + Core.Constants.EVIDENCE_SEPERATOR + "somevalue", "123" },
                { Core.Constants.EVIDENCE_COOKIE_PREFIX + Core.Constants.EVIDENCE_SEPERATOR + "mycookie", "zyx" },
                { "someprefix" + Core.Constants.EVIDENCE_SEPERATOR + "anothervalue", "987" },
            };
            var data = MockFlowData.CreateFromEvidence(evidenceData, false);

            // Act
            _shareUsageElement.Process(data.Object);
            // Wait for the consumer task to finish.
            Assert.IsNotNull(_shareUsageElement.SendDataTask);
            _shareUsageElement.SendDataTask.Wait();

            // Assert
            // Check that one and only one HTTP message was sent
            _httpHandler.Verify(h => h.Send(It.IsAny<HttpRequestMessage>()), Times.Once);
            Assert.AreEqual(1, _xmlContent.Count);

            // Validate that the XML is well formed by passing it through a reader
            using (XmlReader xr = XmlReader.Create(new StringReader(_xmlContent[0])))
            {
                while (xr.Read()) { }
            }
            // Check that the expected values are populated.
            Assert.IsTrue(_xmlContent[0].Contains("<header Name=\"notblocked\"><![CDATA[abc]]></header>"));
            Assert.IsTrue(_xmlContent[0].Contains("<query Name=\"somevalue\"><![CDATA[123]]></query>"));
            Assert.IsTrue(_xmlContent[0].Contains("<cookie Name=\"mycookie\"><![CDATA[zyx]]></cookie>"));
            Assert.IsTrue(_xmlContent[0].Contains("<someprefix Name=\"anothervalue\"><![CDATA[987]]></someprefix>"));
        }

        /// <summary>
        /// Test that passing a null value for the 
        /// 'included query string parameters' value will
        /// result in all query. evidence being shared.
        /// </summary>
        [TestMethod]
        public void ShareUsageElement_NullQueryWhitelist()
        {
            // Arrange
            CreateShareUsage(1, 1, 1,
                new List<string>(), null,
                new List<KeyValuePair<string, string>>());

            Dictionary<string, object> evidenceData = new Dictionary<string, object>()
            {
                { Core.Constants.EVIDENCE_QUERY_PREFIX + Core.Constants.EVIDENCE_SEPERATOR + "somevalue", "123" },
            };
            var data = MockFlowData.CreateFromEvidence(evidenceData, false);

            // Act
            _shareUsageElement.Process(data.Object);
            // Wait for the consumer task to finish.
            Assert.IsNotNull(_shareUsageElement.SendDataTask);
            _shareUsageElement.SendDataTask.Wait();

            // Assert
            // Check that one and only one HTTP message was sent
            _httpHandler.Verify(h => h.Send(It.IsAny<HttpRequestMessage>()), Times.Once);
            Assert.AreEqual(1, _xmlContent.Count);

            // Validate that the XML is well formed by passing it through a reader
            using (XmlReader xr = XmlReader.Create(new StringReader(_xmlContent[0])))
            {
                while (xr.Read()) { }
            }
            // Check that the expected values are populated.
            Assert.IsTrue(_xmlContent[0].Contains("<query Name=\"somevalue\"><![CDATA[123]]></query>"));
        }

        /// <summary>
        /// Helper method used to extract the XML data from an 
        /// HTTP request.
        /// </summary>
        /// <param name="request">
        /// The <see cref="HttpRequestMessage"/> object from which to extract
        /// the XML content.
        /// </param>
        private void StoreRequestXml(HttpRequestMessage request)
        {
            using (var stream = request.Content.ReadAsStreamAsync().Result)
            using (GZipStream decompressedStream =
                new GZipStream(stream, CompressionMode.Decompress, true))
            using (StreamReader reader = new StreamReader(decompressedStream))
            {
                _xmlContent.Add(reader.ReadToEnd());
            }
        }

    }
}
