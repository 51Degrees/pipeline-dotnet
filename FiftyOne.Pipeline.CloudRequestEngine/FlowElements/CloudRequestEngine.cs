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

using FiftyOne.Pipeline.CloudRequestEngine.Data;
using FiftyOne.Pipeline.CloudRequestEngine.FailHandling.Facade;
using FiftyOne.Pipeline.CloudRequestEngine.FailHandling.Recovery;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.CloudRequestEngine.FlowElements
{
    /// <summary>
    /// Engine that makes requests to the 51Degrees cloud service.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/pipeline-elements/cloud-request-engine.md">Specification</see> 
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming",
        "CA1724:Type names should not match namespaces",
        Justification = "This would be a breaking change so will be " +
        "addressed in a future version.")]
    public class CloudRequestEngine :
        AspectEngineBase<CloudRequestData, IAspectPropertyMetaData>,
        ICloudRequestEngine
    {
        /// <summary>
        /// Separator as a character array.
        /// </summary>
        private static readonly char[] EVIDENCE_SEPARATOR_CHAR_ARRAY = 
            Core.Constants.EVIDENCE_SEPERATOR.ToCharArray();

        private HttpClient _httpClient;

        /// <summary>
        /// Raw string properties that describe
        /// server destinations and query parameters.
        /// </summary>
        public struct EndpointsAndKeys
        {
            /// <summary>
            /// The URL for the cloud endpoint that will take the supplied
            /// evidence and return JSON formatted data.
            /// </summary>
            public string DataEndpoint;

            /// <summary>
            /// The resource key to use when making requests.
            /// A resource key encapsulates details such as any license keys,
            /// the properties that should be returned and the domains that
            /// requests are permitted from. A new resource key can be
            /// generated for free at https://configure.51degrees.com
            /// </summary>
            public string ResourceKey;

            /// <summary>
            /// The license key to use when making requests.
            /// This parameter is obsolete, use resourceKey instead.
            /// </summary>
            public string LicenseKey;

            /// <summary>
            /// The URL for the cloud endpoint that will return meta-data
            /// on properties that will be populated when the data endpoint 
            /// is called using the given resource key.
            /// </summary>
            public string PropertiesEndpoint;

            /// <summary>
            /// The URL for the cloud endpoint that will return meta-data
            /// on the evidence that will be used when the data endpoint 
            /// is called using the given resource key.
            /// </summary>
            public string EvidenceKeysEndpoint;

            /// <summary>
            /// The value to use for the Origin header when making requests 
            /// to cloud.
            /// </summary>
            public string CloudRequestOrigin;

            /// <summary>
            /// Not currently used.
            /// </summary>
            public IList<string> RequestedProperties;
        }
        private readonly EndpointsAndKeys _endpointsAndKeys;

        private readonly IFailHandler _failHandler;

        /// <summary>
        /// Deprecated constructor.
        /// Use the one with
        /// <see cref="EndpointsAndKeys"/>
        /// and
        /// <see cref="IRecoveryStrategy"/>.
        /// </summary>
        /// <param name="logger">
        /// The logger for this instance
        /// </param>
        /// <param name="aspectDataFactory">
        /// A factory function to use when creating new 
        /// <see cref="CloudRequestData"/> instances.
        /// </param>
        /// <param name="httpClient">
        /// The HttpClient instance to use when making requests
        /// </param>
        /// <param name="licenseKey">
        /// The license key to use when making requests.
        /// This parameter is obsolete, use resourceKey instead.
        /// </param>
        /// <param name="resourceKey">
        /// The resource key to use when making requests.
        /// A resource key encapsulates details such as any license keys,
        /// the properties that should be returned and the domains that
        /// requests are permitted from. A new resource key can be
        /// generated for free at https://configure.51degrees.com
        /// </param>
        /// <param name="dataEndpoint">
        /// The URL for the cloud endpoint that will take the supplied
        /// evidence and return JSON formatted data.
        /// </param>
        /// <param name="propertiesEndpoint">
        /// The URL for the cloud endpoint that will return meta-data
        /// on properties that will be populated when the data endpoint 
        /// is called using the given resource key.
        /// </param>
        /// <param name="evidenceKeysEndpoint">
        /// The URL for the cloud endpoint that will return meta-data
        /// on the evidence that will be used when the data endpoint 
        /// is called using the given resource key.
        /// </param>
        /// <param name="timeout">
        /// The timeout for HTTP requests in seconds.
        /// </param>
        /// <param name="requestedProperties">
        /// Not currently used.
        /// </param>
        /// <param name="cloudRequestOrigin">
        /// The value to use for the Origin header when making requests 
        /// to cloud.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if a required parameter is null.
        /// </exception>
        public CloudRequestEngine(
            ILogger<AspectEngineBase<CloudRequestData, IAspectPropertyMetaData>> logger,
            Func<IPipeline, FlowElementBase<CloudRequestData, IAspectPropertyMetaData>,
                CloudRequestData> aspectDataFactory,
            HttpClient httpClient,
            string dataEndpoint,
            string resourceKey,
            string licenseKey,
            string propertiesEndpoint,
            string evidenceKeysEndpoint,
            int timeout,
            List<string> requestedProperties,
            string cloudRequestOrigin = null)
            : this(
                  logger, 
                  aspectDataFactory, 
                  httpClient,
                  new EndpointsAndKeys
                  {
                      DataEndpoint = dataEndpoint,
                      ResourceKey = resourceKey,
                      LicenseKey = licenseKey,
                      PropertiesEndpoint = propertiesEndpoint,
                      EvidenceKeysEndpoint = evidenceKeysEndpoint,
                      CloudRequestOrigin = cloudRequestOrigin,
                      RequestedProperties = requestedProperties,
                  },
                  timeout,
                  new SimpleFailHandler(new InstantRecoveryStrategy()))
        { }



        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="logger">
        /// The logger for this instance
        /// </param>
        /// <param name="aspectDataFactory">
        /// A factory function to use when creating new 
        /// <see cref="CloudRequestData"/> instances.
        /// </param>
        /// <param name="httpClient">
        /// The HttpClient instance to use when making requests
        /// </param>
        /// <param name="endpointsAndKeys">
        /// Raw string properties that describe
        /// server destinations and query parameters.
        /// </param>
        /// <param name="timeout">
        /// The timeout for HTTP requests in seconds.
        /// </param>
        /// <param name="failHandler">
        /// Controls when to suspend requests
        /// due to recent query failures.
        /// You can pick
        /// <see cref="InstantRecoveryStrategy"/>,
        /// <see cref="NoRecoveryStrategy"/>
        /// or
        /// <see cref="SimpleRecoveryStrategy"/>
        /// and wrap them in
        /// <see cref="SimpleFailHandler"/>
        /// or provide your own implementation of
        /// <see cref="IFailHandler"/>
        /// .
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if a required parameter is null.
        /// </exception>
        public CloudRequestEngine(
            ILogger<AspectEngineBase<CloudRequestData, IAspectPropertyMetaData>> logger,
            Func<IPipeline, FlowElementBase<CloudRequestData, IAspectPropertyMetaData>,
                CloudRequestData> aspectDataFactory,
            HttpClient httpClient,
            EndpointsAndKeys endpointsAndKeys,
            int timeout,
            IFailHandler failHandler)
            : base(logger, aspectDataFactory)
        {
            if (httpClient == null) throw new ArgumentNullException(nameof(httpClient));
            if ((_failHandler = failHandler) is null) 
                throw new ArgumentNullException(nameof(failHandler));

            _endpointsAndKeys = endpointsAndKeys;

            try
            {
                _httpClient = httpClient;
                if (timeout > 0)
                {
                    _httpClient.Timeout = new TimeSpan(0, 0, timeout);
                }
                else
                {
                    _httpClient.Timeout = new TimeSpan(0, 0, 0, 0, -1);
                }

                _propertyMetaData = new List<IAspectPropertyMetaData>()
                {
                     new AspectPropertyMetaData(this, 
                         "json-response", 
                         typeof(string), 
                         "",
                         new List<string>(), 
                         true),
                     new AspectPropertyMetaData(
                         this, 
                         "process-started", 
                         typeof(bool), 
                         "", 
                         new List<string>(), 
                         true)
                };

                // Start the tasks to get the evidence keys and the public
                // properties from the cloud service. If the stop token is
                // not provided then warn via logging.

                _lazyEvidenceKeyFilter 
                    = new Lazy<IEvidenceKeyFilter>(
                        GetCloudEvidenceKeys,
                        LazyThreadSafetyMode.PublicationOnly);
                _lazyPublicProperties 
                    = new Lazy<IReadOnlyDictionary<string, ProductMetaData>>(
                        GetCloudProperties,
                        LazyThreadSafetyMode.PublicationOnly);
            }
            catch (Exception ex)
            {
                Logger?.LogCritical(ex, $"Error creating {this.GetType().Name}");
                throw;
            }
        }

        private List<IAspectPropertyMetaData> _propertyMetaData;

        /// <summary>
        /// A collection of the properties available in the aspect
        /// data instance that is populated by this engine.
        /// </summary>
        public override IList<IAspectPropertyMetaData> Properties => 
            _propertyMetaData;

        /// <summary>
        /// The 'tier' of the source data used to service this request.
        /// </summary>
        public override string DataSourceTier => "cloud";

        /// <summary>
        /// The key for this element's data within an <see cref="IFlowData"/>
        /// instance.
        /// </summary>
        public override string ElementDataKey => "cloud-response";

        /// <summary>
        /// Responsible for initializing IEvidenceKeyFilter
        /// only once.
        /// </summary>
        private Lazy<IEvidenceKeyFilter> _lazyEvidenceKeyFilter;

        /// <summary>
        /// Responsible for initializing IReadOnlyDictionary{string, ProductMetaData}
        /// only once.
        /// </summary>
        private Lazy<IReadOnlyDictionary<string, ProductMetaData>>
            _lazyPublicProperties;

        /// <summary>
        /// A filter object that indicates the evidence keys that can be used 
        /// by this engine. This will vary based on the supplied resource key 
        /// so will be populated after a call to the cloud service as part of 
        /// object initialization.
        /// </summary>
        /// <exception cref="CloudRequestException">
        /// Thrown if there is an error from the cloud service or
        /// there is no data in the response.
        /// </exception>
        /// <exception cref="CloudRequestEngineTemporarilyUnavailableException">
        /// <see cref="IRecoveryStrategy"/> 
        /// temporarily suppresses further requests
        /// due to recent error.
        /// </exception>
        public override IEvidenceKeyFilter EvidenceKeyFilter 
        {
            get
            {
                try
                {
                    if (_lazyEvidenceKeyFilter.IsValueCreated)
                    {
                        return _lazyEvidenceKeyFilter.Value;
                    }
                    lock (_lazyEvidenceKeyFilter)
                    {
                        return _lazyEvidenceKeyFilter.Value;
                    }
                }
                catch (AggregateException ex)
                {
                    Logger?.LogWarning(
                        "Could not fetch evidence key filter from '{0}'",
                        _endpointsAndKeys.EvidenceKeysEndpoint);
                    if (ex.InnerException is CloudRequestException cloudException)
                    {
                        throw ResurfaceCloudException(cloudException, ex);
                    }
                    throw;
                }
            }
        }

        /// <summary>
        /// A collection of the properties that the cloud service can
        /// populate in the JSON response.
        /// Keyed on property name.
        /// </summary>
        /// <exception cref="CloudRequestException">
        /// Thrown if there is an error from the cloud service or
        /// there is no data in the response.
        /// </exception>
        /// <exception cref="CloudRequestEngineTemporarilyUnavailableException">
        /// <see cref="IRecoveryStrategy"/> 
        /// temporarily suppresses further requests
        /// due to recent error.
        /// </exception>
        public IReadOnlyDictionary<string, ProductMetaData> PublicProperties {
            get
            {
                try
                {
                    if (_lazyPublicProperties.IsValueCreated)
                    {
                        return _lazyPublicProperties.Value;
                    }
                    lock (_lazyPublicProperties)
                    {
                        return _lazyPublicProperties.Value;
                    }
                }
                catch (AggregateException ex)
                {
                    Logger?.LogWarning(
                        "Could not fetch public properties from '{0}'",
                        _endpointsAndKeys.PropertiesEndpoint);
                    if (ex.InnerException is CloudRequestException cloudException)
                    {
                        throw ResurfaceCloudException(cloudException, ex);
                    }
                    throw;
                }
            }
        }

        /// <summary>
        /// Send evidence to the cloud and get back a JSON result.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="aspectData"></param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if a required parameter is null.
        /// </exception>
        /// <exception cref="CloudRequestException">
        /// Thrown if there is an error from the cloud service or
        /// there is no data in the response.
        /// </exception>
        /// <exception cref="CloudRequestEngineTemporarilyUnavailableException">
        /// <see cref="IRecoveryStrategy"/> 
        /// temporarily suppresses further requests
        /// due to recent error.
        /// </exception>
        protected override void ProcessEngine(
            IFlowData data, 
            CloudRequestData aspectData)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (aspectData == null)
            {
                throw new ArgumentNullException(nameof(aspectData));
            }

            aspectData.ProcessStarted = true;


            string jsonResult = string.Empty;
            ThrowIfStillRecovering();

            using (var content = GetContent(data))
            using (var requestMessage =
                new HttpRequestMessage(HttpMethod.Post, _endpointsAndKeys.DataEndpoint))
            {
                if (Logger?.IsEnabled(LogLevel.Debug) == true)
                {
                    Logger?.LogDebug($"Sending request to cloud service at " +
                        $"'{_endpointsAndKeys.DataEndpoint}'. Content: {content}");
                }

                requestMessage.Content = content;
                jsonResult = AmendSendAndProcess(
                    requestMessage,
                    checkForErrorMessages: true);
            }

            aspectData.JsonResponse = jsonResult;
        }

        private void ThrowIfStillRecovering()
        {
            try
            {
                _failHandler.ThrowIfStillRecovering();
            }
            catch (Exception ex)
            {
                throw new CloudRequestEngineTemporarilyUnavailableException(
                    "Sending requests to cloud server"
                    + " is temporarily restricted"
                    + " due to recent failures.", ex);
            }
        }

        private string AmendSendAndProcess(
            HttpRequestMessage request,
            bool checkForErrorMessages)
        {
            try
            {
                using (var requestScope = _failHandler.MakeAttemptScope())
                {
                    try
                    {
                        return ProcessResponse(
                            AddCommonHeadersAndSend(
                                request),
                            checkForErrorMessages);
                    }
                    catch (Exception ex)
                    when (!(ex is CloudRequestEngineTemporarilyUnavailableException))
                    {
                        requestScope.RecordFailure(ex);
                        throw;
                    }
                }
            }
            catch (AggregateException ex)
            when (ex.InnerException is CloudRequestException originalCloudException)
            {
                throw ResurfaceCloudException(originalCloudException, ex);
            }
        }

        private CloudRequestException ResurfaceCloudException(
            CloudRequestException cloudException,
            Exception newInnerException)
        {
            Dictionary<string, string> responseHeaders;
            if (cloudException.ResponseHeaders is IDictionary<string, string> oldHeaders)
            {
                if (oldHeaders is Dictionary<string, string> oldHeadersDic)
                {
                    responseHeaders = oldHeadersDic;
                } 
                else
                {
                    responseHeaders = new Dictionary<string, string>(oldHeaders);
                }
            } else
            {
                responseHeaders = null;
            }
            return new CloudRequestException(
                            cloudException.Message,
                            cloudException.HttpStatusCode,
                            responseHeaders,
                            newInnerException);
        }

        /// <summary>
        /// Validate the JSON response from the cloud service.
        /// An exception will be throw if any type of error has 
        /// occurred.
        /// </summary>
        /// <param name="response">
        /// The JSON content that is returned from the cloud service.
        /// </param>
        /// <param name="checkForErrorMessages">
        /// Set to false if the response will never contain error message
        /// text.
        /// </param>
        /// <exception cref="CloudRequestException">
        /// Thrown if there is an error from the cloud service or
        /// there is no data in the response.
        /// </exception>
        private string ProcessResponse(
            HttpResponseMessage response, 
            bool checkForErrorMessages = true)
        {
            var jsonResult = response.Content.ReadAsStringAsync().Result;
            var hasData = string.IsNullOrEmpty(jsonResult) == false;
            List<string> messages = new List<string>();

            Func<Dictionary<string, string>> GetHeaders = () =>
            {
                // Get the response headers. 
                return response.Headers.ToDictionary(
                    kvp => kvp.Key,
                    kvp => string.Join(", ", kvp.Value));
            };

            if (hasData && checkForErrorMessages)
            {
                JObject jObj;
                try
                {
                    jObj = JObject.Parse(jsonResult);
                }
                catch (JsonReaderException ex)
                {
                    throw new CloudRequestException(
                        "Failed to parse server's response as JSON", 
                        (int)response.StatusCode, GetHeaders(), ex);
                }
                var hasErrors = jObj.ContainsKey("errors");
                hasData = hasErrors ?
                    jObj.Values().Count() > 1 :
                    jObj.Values().Any();

                if (hasErrors)
                {
                    var errors = jObj.Value<JArray>("errors");
                    messages.AddRange(errors.Children<JValue>().Select(t => 
                        t.Value.ToString()));
                }

                // Log any warnings that were returned.
                if (jObj.ContainsKey("warnings"))
                {
                    var warnings = jObj.Value<JArray>("warnings");
                    foreach (var warning in warnings.Children<JValue>()
                        .Select(t => t.Value.ToString()))
                    {
                        Logger?.LogWarning(warning);
                    }
                }
            }
            
            // If there were no errors but there was also no other data
            // in the response then add an explanation to the list of 
            // messages.
            if (messages.Count == 0 &&
                hasData == false)
            {
                var msg = string.Format(CultureInfo.InvariantCulture,
                    Messages.MessageNoDataInResponse,
                    _endpointsAndKeys.DataEndpoint);
                messages.Add(msg);
            }
            // If there is no detailed error message, but we got a
            // non-success status code, then add a message to the list
            else if (messages.Count == 0 && 
                response.IsSuccessStatusCode == false)
            {
                var msg = string.Format(CultureInfo.InvariantCulture,
                    Messages.MessageErrorCodeReturned,
                    _endpointsAndKeys.DataEndpoint,
                    response.StatusCode,
                    jsonResult);
                messages.Add(msg);
            }

            // If there are any errors returned from the cloud service 
            // then throw an exception
            if (messages.Count > 1)
            {
                var headers = GetHeaders();
                var aggregated = new AggregateException(
                    Messages.ExceptionCloudErrorsMultiple,
                    messages.Select(m => new CloudRequestException(m, 
                        (int)response.StatusCode, headers)));
                throw new CloudRequestException(
                    Messages.ExceptionCloudErrorsMultiple,
                    (int)response.StatusCode, GetHeaders(), aggregated);
            }
            else if (messages.Count == 1)
            {
                var msg = string.Format(CultureInfo.InvariantCulture,
                    Messages.ExceptionCloudError,
                    messages[0]);
                throw new CloudRequestException(msg, 
                    (int)response.StatusCode, GetHeaders());
            }

            return jsonResult;
        }

        /// <summary>
        /// Generate the Content to send in the POST request. The evidence keys
        /// e.g. 'query.' and 'header.' have an order of precedence. These are
        /// added to the evidence in reverse order, if there is conflict then 
        /// the queryData value is overwritten. 
        /// 
        /// 'query.' evidence should take precedence over all other evidence.
        /// If there are evidence keys other than 'query.' that conflict then
        /// this is unexpected so an error will be logged.
        /// </summary>
        /// <param name="data"></param>
        /// <returns>Evidence in a FormUrlEncodedContent object</returns>
        private FormUrlEncodedContent GetContent(IFlowData data)
        {
            var queryData = new Dictionary<string, string>();

            queryData.Add("resource", _endpointsAndKeys.ResourceKey);

            if (string.IsNullOrWhiteSpace(_endpointsAndKeys.LicenseKey) == false)
            {
                queryData.Add("license", _endpointsAndKeys.LicenseKey);
            }

            var evidence = data.GetEvidence().AsDictionary();

            // Add evidence in reverse alphabetical order, excluding special
            // keys. 
            AddQueryData(queryData, evidence, evidence
                .Where(e =>
                    KeyHasPrefix(e, Core.Constants.EVIDENCE_QUERY_PREFIX) == false &&
                    KeyHasPrefix(e, Core.Constants.EVIDENCE_HTTPHEADER_PREFIX) == false &&
                    KeyHasPrefix(e, Core.Constants.EVIDENCE_COOKIE_PREFIX) == false)
                .OrderByDescending(e => e.Key));
            // Add cookie evidence.
            AddQueryData(queryData, evidence, evidence
                .Where(e => KeyHasPrefix(e, Core.Constants.EVIDENCE_COOKIE_PREFIX)));
            // Add header evidence.
            AddQueryData(queryData, evidence, evidence
                .Where(e => KeyHasPrefix(e, Core.Constants.EVIDENCE_HTTPHEADER_PREFIX)));
            // Add query evidence.
            AddQueryData(queryData, evidence, evidence
                .Where(e => KeyHasPrefix(e, Core.Constants.EVIDENCE_QUERY_PREFIX)));

            var content = new FormUrlEncodedContent(queryData);
            return content;
        }
        
        /// <summary>
        /// Check that the key of a KeyValuePair has the given prefix.
        /// </summary>
        /// <param name="item">The KeyValuePair to check.</param>
        /// <param name="prefix">The prefix to check for.</param>
        /// <returns>True if the key has the prefix.</returns>
        private static bool KeyHasPrefix(
            KeyValuePair<string, object> item, 
            string prefix) 
        {
            var key = item.Key.Split(EVIDENCE_SEPARATOR_CHAR_ARRAY);
            return key[0].Equals(
                prefix, 
                StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Add query data to the evidence.
        /// </summary>
        /// <param name="queryData">
        /// The destination dictionary to add query data to.
        /// </param>
        /// <param name="allEvidence">
        /// All evidence in the flow data. This is used to report which 
        /// evidence keys are conflicting.
        /// </param>
        /// <param name="evidence">
        /// Evidence to add to the query Data.
        /// </param>
        private void AddQueryData(
            Dictionary<string, string> queryData,
            IReadOnlyDictionary<string, object> allEvidence,
            IEnumerable<KeyValuePair<string, object>> evidence)
        {
            foreach (var item in evidence)
            {
                // Get the key parts
                var key = item.Key.Split(EVIDENCE_SEPARATOR_CHAR_ARRAY);
                var prefix = key[0];
                var suffix = key.Last();

                // Check and add the evidence to the query parameters.
                if (queryData.ContainsKey(suffix) == false)
                {
                    queryData.Add(suffix, item.Value.ToString());
                }
                // If the queryParameter exists already...
                else
                {
                    // Get the conflicting pieces of evidence and then log a 
                    // warning, if the evidence prefix is not query. Otherwise
                    // a warning is not needed as query evidence is expected 
                    // to overwrite any existing evidence with the same suffix.
                    if (prefix.Equals(Core.Constants.EVIDENCE_QUERY_PREFIX, 
                        StringComparison.InvariantCultureIgnoreCase) == false)
                    {
                        var oldValue = queryData[suffix];
                        var conflicts = allEvidence
                            .Where(e => e.Key != item.Key)
                            .Where(e => e.Key.EndsWith(suffix, 
                                StringComparison.InvariantCultureIgnoreCase))
                            .Select(e => $"{e.Key}={e.Value}");

                        Logger?.LogWarning(
                            $"'{item.Key}={item.Value}' evidence conflicts " +
                            $"with '{string.Join("', '", conflicts)}'");
                    }
                    // Overwrite the existing queryParameter value.
                    queryData[suffix] = item.Value.ToString();
                }
            }
        }

        /// <summary>
        /// Cleanup any unmanaged resources
        /// </summary>
        protected override void UnmanagedResourcesCleanup()
        {
        }

        /// <summary>
        /// Get the properties that are available from the cloud service.
        /// </summary>
        /// <returns>
        /// The value to be saved into <see cref="PublicProperties"/>
        /// </returns>
        /// <exception cref="CloudRequestException">
        /// Thrown if there is an error from the cloud service or
        /// there is no data in the response.
        /// </exception>
        private IReadOnlyDictionary<string, ProductMetaData> GetCloudProperties()
        {
            ThrowIfStillRecovering();
            var jsonResult = string.Empty;
            Func<string> ErrorMessage = () => 
                "Failed to retrieve available properties " +
                $"from cloud service at {_endpointsAndKeys.PropertiesEndpoint}.";

            var url = $"{_endpointsAndKeys.PropertiesEndpoint}?resource={_endpointsAndKeys.ResourceKey}";
            using (var requestMessage =
                new HttpRequestMessage(HttpMethod.Get, url))
            {
                try
                {
                    jsonResult = AmendSendAndProcess(
                        requestMessage,
                        checkForErrorMessages: true);
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ErrorMessage(), ex);
                    throw;
                }
            }

            if (string.IsNullOrEmpty(jsonResult) == false)
            {
                var accessiblePropertyData = JsonConvert
                    .DeserializeObject<LicencedProducts>(jsonResult);

                return accessiblePropertyData.Products;
            }
            else
            {
                throw new Exception(ErrorMessage());
            }
        }

        /// <summary>
        /// Get the evidence keys that can be consumed by the cloud service.
        /// </summary>
        /// <returns>
        /// The value to be saved into <see cref="EvidenceKeyFilter"/>.
        /// </returns>
        private IEvidenceKeyFilter GetCloudEvidenceKeys()
        {
            ThrowIfStillRecovering();
            var jsonResult = string.Empty;
            Func<string> ErrorMessage = () => 
                "Failed to retrieve evidence keys " +
                $"from cloud service at {_endpointsAndKeys.EvidenceKeysEndpoint}.";

            using (var requestMessage =
                new HttpRequestMessage(HttpMethod.Get, _endpointsAndKeys.EvidenceKeysEndpoint))
            {
                try
                {
                    // Note - Don't check for error messages in the response
                    // as it is a flat JSON array.
                    jsonResult = AmendSendAndProcess(
                        requestMessage,
                        checkForErrorMessages: false);
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ErrorMessage(), ex);
                    throw;
                }
            }

            if (string.IsNullOrEmpty(jsonResult) == false)
            {
                var evidenceKeys = JsonConvert
                    .DeserializeObject<List<string>>(jsonResult);
                return new EvidenceKeyFilterWhitelist(evidenceKeys);
            }
            else
            {
                Logger?.LogWarning(ErrorMessage());
                return null;
            }
        }

        /// <summary>
        /// Add the common headers to the specified message and send it.
        /// </summary>
        /// <param name="request">
        /// The request to send
        /// </param>
        /// <returns>
        /// The response
        /// </returns>
        private HttpResponseMessage AddCommonHeadersAndSend(
            HttpRequestMessage request)
        {
            ThrowIfStillRecovering();
            if (string.IsNullOrEmpty(_endpointsAndKeys.CloudRequestOrigin) == false &&
                (request.Headers.Contains(Constants.ORIGIN_HEADER_NAME) == false ||
                request.Headers.GetValues(Constants.ORIGIN_HEADER_NAME).Contains(
                    _endpointsAndKeys.CloudRequestOrigin) == false))
            {
                request.Headers.Add(Constants.ORIGIN_HEADER_NAME, _endpointsAndKeys.CloudRequestOrigin);
            }
            return SendRequestAsync(request);
        }

        /// <summary>
        /// Send a request and handle any exception if one is thrown.
        /// </summary>
        /// <param name="request">
        /// The request to send
        /// </param>
        /// <returns>
        /// The response
        /// </returns>
        private HttpResponseMessage SendRequestAsync(
            HttpRequestMessage request)
        {
            try
            {
                var task = Task.Run(() => _httpClient.SendAsync(request));
                task.Wait();
                return task.Result;
            }
            catch (AggregateException httpException)
            {
                throw new CloudRequestException(
                    Messages.ExceptionCloudResponseFailure,
                    httpException);
            }
        }
    }
}
