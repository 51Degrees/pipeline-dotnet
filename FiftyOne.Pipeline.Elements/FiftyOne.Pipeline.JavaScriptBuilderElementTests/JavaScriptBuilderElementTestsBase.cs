using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;
using FiftyOne.Pipeline.Engines.TestHelpers;
using FiftyOne.Pipeline.Core.Data;
using Moq;
using Newtonsoft.Json.Linq;
using FiftyOne.Pipeline.JsonBuilder.FlowElement;
using FiftyOne.Pipeline.JsonBuilder.Data;

namespace FiftyOne.Pipeline.JavaScript.Tests
{
    public class JavaScriptBuilderElementTestsBase
    {
        public string ClientServerUrl { get; private set; }

        public ChromeDriver Driver { get; private set; }
        public INetwork Interceptor => Driver?.Manage().Network;

        public ILoggerFactory LoggerFactory { get; private set; }


        private Mock<IJsonBuilderElement> _mockjsonBuilderElement;
        private Mock<IElementData> _elementDataMock;
        private IList<IElementPropertyMetaData> _elementPropertyMetaDatas;

        public Action<NetworkRequestSentEventArgs> OnRequestSent { get; set; } = null;

        public virtual async Task Init()
        {
            ClientServerUrl = $"http://localhost:{TestHttpListener.GetRandomUnusedPort()}/";

            var chromeOptions = new ChromeOptions();
            chromeOptions.SetLoggingPreference(LogType.Browser, OpenQA.Selenium.LogLevel.Info);
            chromeOptions.AcceptInsecureCertificates = true;
            // run in headless mode.
            chromeOptions.AddArgument("--headless");
            try
            {
                Driver = new ChromeDriver(chromeOptions);
            }
            catch (WebDriverException)
            {
                Assert.Inconclusive("Could not create a ChromeDriver, check " +
                    "that the Chromium driver is installed");
            }

            Interceptor.NetworkRequestSent += OnNetworkRequestSent;
            await Interceptor.StartMonitoring();

            _mockjsonBuilderElement = new Mock<IJsonBuilderElement>();

            _elementPropertyMetaDatas = new List<IElementPropertyMetaData>() {
                new ElementPropertyMetaData(_mockjsonBuilderElement.Object, "property", typeof(string), true)
            };

            _mockjsonBuilderElement.Setup(x => x.Properties).Returns(_elementPropertyMetaDatas);

            _elementDataMock = new Mock<IElementData>();
            _elementDataMock.Setup(ed => ed.AsDictionary()).Returns(new Dictionary<string, object>() { { "property", "thisIsAValue" } });

            LoggerFactory = new LoggerFactory();
        }

        private void OnNetworkRequestSent(object sender, NetworkRequestSentEventArgs e)
            => OnRequestSent?.Invoke(e);


        delegate void GetValueCallback(string key, out object result);

        /// <summary>
        /// Configure the flow data to respond in the way we want for 
        /// this test.
        /// </summary>
        /// <param name="flowData">
        /// The mock flow data instance to configure 
        /// </param>
        /// <param name="jsonData">
        /// The JSON data to embed in the flow data.
        /// This will be copied into the JavaScript that is produced.
        /// </param>
        /// <param name="hostName">
        /// The host name to add to the evidence.
        /// The JavaScriptBuilder should use this to generate the 
        /// callback URL.
        /// </param>
        /// <param name="protocol">
        /// The protocol to add to the evidence.
        /// The JavaScriptBuilder should use this to generate the 
        /// callback URL.
        /// </param>
        /// <param name="userAgent">
        /// The User-Agent to add to the evidence.
        /// </param>
        /// <param name="latitude">
        /// The latitude to add to the evidence.
        /// </param>
        /// <param name="longitude">
        /// The longitude to add to the evidence.
        /// </param>
        public void Configure(
            Mock<IFlowData> flowData,
            JObject jsonData = null,
            string hostName = "localhost",
            string protocol = "https",
            string userAgent = "iPhone",
            string latitude = "51",
            string longitude = "-1",
            string jsObjName = null)
        {
            if (jsonData == null)
            {
                jsonData = new JObject();
                jsonData["device"] = new JObject(new JProperty("ismobile", true));
            }

            flowData.Setup(d => d.Get<IJsonBuilderElementData>()).Returns(() =>
            {
                var d = new JsonBuilderElementData(new Mock<ILogger<JsonBuilderElementData>>().Object, flowData.Object.Pipeline);
                d.Json = jsonData.ToString();
                return d;
            });

            string session = "abcdefg-hijklmn-opqrst-uvwxyz";
            int sequence = 1;
            // Setup the TryGetEvidence methods that are used to get 
            // host and protocol for the callback URL
            flowData.Setup(d => d.TryGetEvidence(JavaScriptBuilder.Constants.EVIDENCE_HOST_KEY, out It.Ref<object>.IsAny))
                .Callback(new GetValueCallback((string key, out object result) => { result = hostName; })).Returns(true);
            flowData.Setup(d => d.TryGetEvidence(Core.Constants.EVIDENCE_PROTOCOL, out It.Ref<object>.IsAny))
                .Callback(new GetValueCallback((string key, out object result) => { result = protocol; })).Returns(true);
            flowData.Setup(d => d.TryGetEvidence(Engines.FiftyOne.Constants.EVIDENCE_SESSIONID, out It.Ref<object>.IsAny))
                .Callback(new GetValueCallback((string key, out object result) => { result = session; })).Returns(true);
            flowData.Setup(d => d.TryGetEvidence(Engines.FiftyOne.Constants.EVIDENCE_SEQUENCE, out It.Ref<object>.IsAny))
                .Callback(new GetValueCallback((string key, out object result) => { result = sequence; })).Returns(true);

            flowData.Setup(d => d.GetAsString(It.IsAny<string>())).Returns("None");
            var evidenceDict = new Dictionary<string, object>() {
                { JavaScriptBuilder.Constants.EVIDENCE_HOST_KEY, hostName },
                { Core.Constants.EVIDENCE_PROTOCOL, protocol },
                { Core.Constants.EVIDENCE_QUERY_USERAGENT_KEY, userAgent },
                { "query.latitude", latitude },
                { "query.longitude", longitude },
                { Engines.FiftyOne.Constants.EVIDENCE_SEQUENCE, sequence },
                { Engines.FiftyOne.Constants.EVIDENCE_SESSIONID, session }
            };
            if (jsObjName != null)
            {
                flowData.Setup(d => d.TryGetEvidence(JavaScriptBuilder.Constants.EVIDENCE_OBJECT_NAME, out It.Ref<object>.IsAny))
                    .Callback(new GetValueCallback((string key, out object result) => { result = jsObjName; })).Returns(true);
                evidenceDict.Add(JavaScriptBuilder.Constants.EVIDENCE_OBJECT_NAME, jsObjName);
            }

            flowData.Setup(d => d.GetEvidence().AsDictionary()).Returns(evidenceDict);
            flowData.Setup(d => d.Get(It.IsAny<string>())).Returns(_elementDataMock.Object);
        }

        public virtual async Task Cleanup()
        {
            if (Driver != null)
            {
                await Interceptor.StopMonitoring();
                Driver.Quit();
            }

            // Ignore request monitoring events
            OnRequestSent = null;
        }
    }
}
