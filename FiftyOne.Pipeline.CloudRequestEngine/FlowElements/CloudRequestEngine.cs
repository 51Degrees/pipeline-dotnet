/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2020 51 Degrees Mobile Experts Limited, 5 Charlotte Close,
 * Caversham, Reading, Berkshire, United Kingdom RG4 7BY.
 *
 * This Original Work is licensed under the European Union Public Licence (EUPL) 
 * v.1.2 and is subject to its terms as set out below.
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
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Exceptions;
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
using System.Text;

namespace FiftyOne.Pipeline.CloudRequestEngine.FlowElements
{
    /// <summary>
    /// Engine that makes requests to the 51Degrees cloud service based 
    /// on the details passed at creation and the evidence in the
    /// FlowData instance.
    /// The unprocessed JSON response is stored in the FlowData
    /// for other engines to make use of.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming",
        "CA1724:Type names should not match namespaces",
        Justification = "This would be a breaking change so will be " +
        "addressed in a future version.")]
    public class CloudRequestEngine : AspectEngineBase<CloudRequestData, IAspectPropertyMetaData>, ICloudRequestEngine
    {
        private HttpClient _httpClient;

        private string _dataEndpoint;
        private string _resourceKey;
        private string _licenseKey;
        private string _propertiesEndpoint;
        private string _evidenceKeysEndpoint;
        private List<string> _requestedProperties;
        private IEvidenceKeyFilter _evidenceKeyFilter;

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
            List<string> requestedProperties) 
            : base(logger, aspectDataFactory)
        {
            if (httpClient == null) throw new ArgumentNullException(nameof(httpClient));

            try
            {
                _dataEndpoint = dataEndpoint;
                _resourceKey = resourceKey;
                _licenseKey = licenseKey;
                _propertiesEndpoint = propertiesEndpoint;
                _evidenceKeysEndpoint = evidenceKeysEndpoint;
                _requestedProperties = requestedProperties;

                _httpClient = httpClient;
                if (timeout > 0)
                {
                    _httpClient.Timeout = new TimeSpan(0, 0, timeout);
                }
                else
                {
                    _httpClient.Timeout = new TimeSpan(0, 0, 0, 0, -1);
                }

                GetCloudProperties();
                GetCloudEvidenceKeys();

                _propertyMetaData = new List<IAspectPropertyMetaData>()
                {
                     new AspectPropertyMetaData(this, "json-response", typeof(string), "", new List<string>(), true)
                };
            }
            catch (Exception ex)
            {
                Logger.LogCritical(ex, $"Error creating {this.GetType().Name}");
                throw;
            }
        }

        private List<IAspectPropertyMetaData> _propertyMetaData;
        private Dictionary<string, ProductMetaData> _publicProperties;

        /// <summary>
        /// A collection of the properties available in the aspect
        /// data instance that is populated by this engine.
        /// </summary>
        public override IList<IAspectPropertyMetaData> Properties => _propertyMetaData;

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
        /// A filter object that indicates the evidence keys that can be
        /// used by this engine.
        /// This will vary based on the supplied resource key so will
        /// be populated after a call to the cloud service as part of 
        /// object initialization.
        /// </summary>
        public override IEvidenceKeyFilter EvidenceKeyFilter => _evidenceKeyFilter;

        /// <summary>
        /// A collection of the properties that the cloud service can
        /// populate in the JSON response.
        /// Keyed on property name.
        /// </summary>
        public IReadOnlyDictionary<string, ProductMetaData> PublicProperties => _publicProperties;

        /// <summary>
        /// Send evidence to the cloud and get back a JSON result.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="aspectData"></param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if a required parameter is null.
        /// </exception>
        protected override void ProcessEngine(IFlowData data, CloudRequestData aspectData)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (aspectData == null) throw new ArgumentNullException(nameof(aspectData));

            string jsonResult = string.Empty;

            using (var content = GetContent(data))
            {
                if (Logger != null && Logger.IsEnabled(LogLevel.Debug))
                {
                    Logger.LogDebug($"Sending request to cloud service at " +
                        $"'{_dataEndpoint}'. Content: {content}");
                }

                var request = _httpClient.PostAsync(_dataEndpoint, content);
                jsonResult = request.Result.Content.ReadAsStringAsync().Result;
            }

            aspectData.JsonResponse = jsonResult;

            ValidateResponse(jsonResult);
        }

        /// <summary>
        /// Validate the JSON response from the cloud service.
        /// </summary>
        /// <param name="jsonResult">
        /// The JSON content that is returned from the cloud service.
        /// </param>
        /// <exception cref="AggregateException">
        /// Thrown if there are multiple errors returned from the 
        /// cloud service.
        /// </exception>
        /// <exception cref="PipelineException">
        /// Thrown if there is an error from the cloud service or
        /// there is no data in the response.
        /// </exception>
        private void ValidateResponse(string jsonResult)
        {
            var hasData = string.IsNullOrEmpty(jsonResult) == false;
            List<string> messages = new List<string>();

            if (hasData)
            {
                var jObj = JObject.Parse(jsonResult);
                var hasErrors = jObj.ContainsKey("errors");
                hasData = hasErrors ?
                    jObj.Values().Count() > 1 :
                    jObj.Values().Any();

                if (hasErrors)
                {
                    var errors = jObj.Value<JArray>("errors");
                    messages.AddRange(errors.Children<JValue>().Select(t => t.Value.ToString()));
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
                    _dataEndpoint);
                messages.Add(msg);
            }

            // If there are any errors returned from the cloud service 
            // then throw an exception
            if (messages.Count > 1)
            {
                throw new AggregateException(
                    Messages.ExceptionCloudErrorsMultiple,
                    messages.Select(m => new PipelineException(m)));
            }
            else if (messages.Count == 1)
            {
                var msg = string.Format(CultureInfo.InvariantCulture,
                    Messages.ExceptionCloudError,
                    messages[0]);
                throw new PipelineException(msg);
            }
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

            queryData.Add("resource", _resourceKey);

            if (string.IsNullOrWhiteSpace(_licenseKey) == false)
            {
                queryData.Add("license", _licenseKey);
            }

            var evidence = data.GetEvidence().AsDictionary();

            // Add evidence in reverse alphabetical order, excluding special keys. 
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
        private static bool KeyHasPrefix(KeyValuePair<string, object> item, string prefix) 
        {
            var key = item.Key.Split(Core.Constants.EVIDENCE_SEPERATOR.ToCharArray());
            return key[0].Equals(prefix, StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Add query data to the evidence.
        /// </summary>
        /// <param name="queryData">
        /// The destination dictionary to add query data to.
        /// </param>
        /// <param name="allEvidence">
        /// All evidence in the flow data. This is used to report which evidence
        /// keys are conflicting.
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
                var key = item.Key.Split(Core.Constants.EVIDENCE_SEPERATOR.ToCharArray());
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
                    // warning, if the evidence prefix is not query. Otherwise a
                    // warning is not needed as query evidence is expected 
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

                        Logger.LogWarning($"'{item.Key}={item.Value}' evidence " +
                            $"conflicts with '{string.Join("', '", conflicts)}'");
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
        private void GetCloudProperties()
        {
            HttpResponseMessage result = null;
            string jsonResult = string.Empty;

            try
            {
                var request = _httpClient.GetAsync($"{_propertiesEndpoint}?resource={_resourceKey}");
                result = request.Result;
                jsonResult = result.Content.ReadAsStringAsync().Result;
            }
            catch (Exception ex)
            {
                throw new Exception ($"Failed to retrieve available properties " +
                    $"from cloud service at {_propertiesEndpoint}.", ex);
            }
            
            if (result.IsSuccessStatusCode == false)
            {
                List<Exception> exceptions = new List<Exception>();
                if (string.IsNullOrEmpty(jsonResult) == false)
                {
                    var res = JsonConvert.DeserializeObject<LicencedProducts>(jsonResult);
                    foreach (var e in res.Errors)
                    {
                        exceptions.Add(new PipelineException(e));
                    }
                }
                throw new AggregateException(exceptions);
            }

            if (string.IsNullOrEmpty(jsonResult) == false)
            {
                var accessiblePropertyData = JsonConvert
                    .DeserializeObject<LicencedProducts>(jsonResult);

                _publicProperties = accessiblePropertyData.Products;
            }
            else
            {
                throw new Exception($"Failed to retrieve available properties " +
                    $"from cloud service at {_propertiesEndpoint}.");
            }
        }

        /// <summary>
        /// Get the evidence keys that are required by the cloud service.
        /// </summary>
        private void GetCloudEvidenceKeys()
        {
            string jsonResult = string.Empty;

            try
            {
                var request = _httpClient.GetAsync(_evidenceKeysEndpoint);
                jsonResult = request.Result.Content.ReadAsStringAsync().Result;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to retrieve evidence keys " +
                    $"from cloud service at {_evidenceKeysEndpoint}.", ex);
            }

            if (string.IsNullOrEmpty(jsonResult) == false)
            {
                var evidenceKeys = JsonConvert
                    .DeserializeObject<List<string>>(jsonResult);

                _evidenceKeyFilter = new EvidenceKeyFilterWhitelist(evidenceKeys);
            }
            else
            {
                throw new Exception($"Failed to retrieve evidence keys " +
                    $"from cloud service at {_evidenceKeysEndpoint}.");
            }
        }
    }
}
