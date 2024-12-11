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
using FiftyOne.Pipeline.Core.Attributes;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace FiftyOne.Pipeline.CloudRequestEngine.FlowElements
{
    /// <summary>
    /// A fluent builder for <see cref="CloudRequestEngine"/> instances.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", 
        "CA1054:Uri parameters should not be strings", 
        Justification = "Changing this could cause implementation issues " +
        "so it being delayed for the time being.")]
    public class CloudRequestEngineBuilder : 
        AspectEngineBuilderBase<CloudRequestEngineBuilder, CloudRequestEngine>
    {
        #region Private Properties

        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<CloudRequestData> _dataLogger;
        private readonly HttpClient _httpClient;

        // Note - Defaults for these fields are set in the Build method.
        private string _dataEndpoint = "";
        private string _propertiesEndpoint = "";
        private string _evidenceKeysEndpoint = "";

        private string _resourceKey = Constants.RESOURCE_KEY_DEFAULT;
        private string _licenseKey = Constants.LICENSE_KEY_DEFAULT;
        private string _cloudRequestOrigin = Constants.CLOUD_REQUEST_ORIGIN_DEFAULT;
        private int _timeout = Constants.CLOUD_REQUEST_TIMEOUT_DEFAULT_SECONDS;
        private int _failuresToEnterRecovery = Constants.CLOUD_REQUEST_FAILURES_TO_ENTER_RECOVERY_DEFAULT;
        private int _failuresWindowSeconds = Constants.CLOUD_REQUEST_FAILURES_WINDOW_SECONDS_DEFAULT;
        private double _recoverySeconds = Constants.CLOUD_REQUEST_RECOVERY_SECONDS_DEFAULT;

        #endregion

        #region Constructor

        /// <summary>
        /// Construct a new instance of the builder.
        /// </summary>
        /// <param name="loggerFactory">
        /// Factory used to create loggers for the engine
        /// </param>
        /// <param name="httpClient">
        /// HttpClient instance used to make http requests
        /// </param>
        public CloudRequestEngineBuilder(
            ILoggerFactory loggerFactory,
            HttpClient httpClient)
        {
            _loggerFactory = loggerFactory;
            _httpClient = httpClient;
            _dataLogger = _loggerFactory.CreateLogger<CloudRequestData>();
        }

        #endregion

        /// <summary>
        /// The root endpoint which the CloudRequestsEngine will query.
        /// This will set the data, properties and evidence keys endpoints.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if a required parameter is null.
        /// </exception>
        [DefaultValue(Constants.CLOUD_URI_DEFAULT)]
        public CloudRequestEngineBuilder SetEndPoint(string uri)
        {
            if (uri == null) throw new ArgumentNullException(nameof(uri));

            if (uri.EndsWith("/", StringComparison.Ordinal) == false)
            {
                uri += '/';
            }
            return SetDataEndpoint(uri + Constants.DATA_FILENAME)
                .SetPropertiesEndpoint(uri + Constants.PROPERTIES_FILENAME)
                .SetEvidenceKeysEndpoint(uri + Constants.EVIDENCE_KEYS_FILENAME);
        }

        /// <summary>
        /// The endpoint the CloudRequestEngine will query to get a processing
        /// result.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        [DefaultValue(Constants.DATA_ENDPOINT_DEFAULT)]
        public CloudRequestEngineBuilder SetDataEndpoint(string uri)
        {
            _dataEndpoint = uri;
            return this;
        }

        /// <summary>
        /// The endpoint the cloudRequestEngine will query to get the available
        /// properties.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        [DefaultValue(Constants.PROPERTIES_ENDPOINT_DEFAULT)]
        public CloudRequestEngineBuilder SetPropertiesEndpoint(string uri)
        {
            _propertiesEndpoint = uri;
            return this;
        }

        /// <summary>
        /// The endpoint the cloudRequestEngine will query to get the required
        /// evidence keys.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        [DefaultValue(Constants.EVIDENCE_KEYS_ENDPOINT_DEFAULT)]
        public CloudRequestEngineBuilder SetEvidenceKeysEndpoint(string uri)
        {
            _evidenceKeysEndpoint = uri;
            return this;
        }

        /// <summary>
        /// The resource key to query the endpoint with.
        /// </summary>
        /// <param name="resourceKey">
        /// The resource key to use when making requests to the cloud service
        /// </param>
        /// <returns>
        /// This builder
        /// </returns>
        [DefaultValue("No default - a resource key must be supplied")]
        public CloudRequestEngineBuilder SetResourceKey(string resourceKey)
        {
            _resourceKey = resourceKey;
            return this;
        }

        /// <summary>
        /// The license key to query the endpoint with.
        /// </summary>
        /// <param name="licenseKey"></param>
        /// <returns></returns>
        [Obsolete("License key is no longer used directly. " +
            "Use a resource key instead.")]
        [CodeConfigOnly]
        public CloudRequestEngineBuilder SetLicenseKey(string licenseKey)
        {
            _licenseKey = licenseKey;
            return this;
        }

        /// <summary>
        /// Timeout in seconds for the request to the endpoint.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        [DefaultValue(Constants.CLOUD_REQUEST_TIMEOUT_DEFAULT_SECONDS)]
        public CloudRequestEngineBuilder SetTimeOutSeconds(int timeout)
        {
            _timeout = timeout;
            return this;
        }

        /// <summary>
        /// Number of failures to occur within
        /// 
        /// for CloudRequestEngine to temporarily stop sending requests.
        /// </summary>
        /// <param name="failuresToEnterRecovery"></param>
        /// <returns></returns>
        [DefaultValue(Constants.CLOUD_REQUEST_FAILURES_TO_ENTER_RECOVERY_DEFAULT)]
        public CloudRequestEngineBuilder SetFailuresToEnterRecovery(
            int failuresToEnterRecovery)
        {
            if (failuresToEnterRecovery < Constants.CLOUD_REQUEST_FAILURES_TO_ENTER_RECOVERY_MIN
                || failuresToEnterRecovery > Constants.CLOUD_REQUEST_FAILURES_TO_ENTER_RECOVERY_MAX)
            {
                throw new ArgumentOutOfRangeException(
                        nameof(failuresToEnterRecovery),
                        $"{nameof(failuresToEnterRecovery)} must be within"
                        + $" {Constants.CLOUD_REQUEST_FAILURES_TO_ENTER_RECOVERY_MIN} and"
                        + $" {Constants.CLOUD_REQUEST_FAILURES_TO_ENTER_RECOVERY_MAX} (both inclusive)."
                        + $" Received: {failuresToEnterRecovery}.");
            }
            _failuresToEnterRecovery = failuresToEnterRecovery;
            return this;
        }

        /// <summary>
        /// The time period in seconds within which a number of
        /// failed requests should reach `FailuresToEnterRecovery`
        /// for the engine to enter "recovery period"
        /// </summary>
        /// <param name="failuresWindowSeconds"></param>
        [DefaultValue(Constants.CLOUD_REQUEST_FAILURES_WINDOW_SECONDS_DEFAULT)]
        public CloudRequestEngineBuilder SetFailuresWindowSeconds(
            int failuresWindowSeconds)
        {
            if ((_failuresWindowSeconds = failuresWindowSeconds) <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(failuresWindowSeconds),
                    $"{nameof(failuresWindowSeconds)} must be positive,"
                    + $" Received: {failuresWindowSeconds}.");
            }
            return this;
        }

        /// <summary>
        /// For how long to disallow server calls after failure.
        /// </summary>
        /// <param name="recoverySeconds"></param>
        /// <returns></returns>
        [DefaultValue(Constants.CLOUD_REQUEST_RECOVERY_SECONDS_DEFAULT)]
        public CloudRequestEngineBuilder SetRecoverySeconds(double recoverySeconds)
        {
            _recoverySeconds = recoverySeconds;
            return this;
        }

        /// <summary>
        /// The value to set for the Origin header when making requests
        /// to the cloud service.
        /// This is used by the cloud service to check that the request
        /// is being made from a origin matching those allowed by the 
        /// resource key.
        /// For more detail, see the 'Request Headers' section in the 
        /// <a href="https://cloud.51degrees.com/api-docs/index.html">cloud documentation</a>.
        /// </summary>
        /// <param name="cloudRequestOrigin">
        /// The value to use for the Origin header.
        /// </param>
        /// <returns>
        /// This builder
        /// </returns>
        [DefaultValue(Constants.CLOUD_REQUEST_ORIGIN_DEFAULT)]
        public CloudRequestEngineBuilder SetCloudRequestOrigin(string cloudRequestOrigin)
        {
            _cloudRequestOrigin = cloudRequestOrigin;
            return this;
        }

        /// <summary>
        /// Build and return a new <see cref="CloudRequestEngine"/>
        /// instance using the current configuration.
        /// </summary>
        /// <returns></returns>
        public CloudRequestEngine Build()
        {
            if (string.IsNullOrWhiteSpace(_resourceKey))
            {
                throw new PipelineConfigurationException(
                    Messages.ExceptionResourceKeyNeeded);
            }

            // If any of the endpoints are not set, then use the environment variable if
            // specified, or the default base url if not.
            var endpoint = Environment.GetEnvironmentVariable(Constants.FOD_CLOUD_API_URL) ?? 
                Constants.CLOUD_URI_DEFAULT;

            if (string.IsNullOrWhiteSpace(_dataEndpoint))
            {
                SetDataEndpoint(endpoint + Constants.DATA_FILENAME);
            }
            if (string.IsNullOrWhiteSpace(_propertiesEndpoint))
            {
                SetPropertiesEndpoint(endpoint + Constants.PROPERTIES_FILENAME);
            }
            if (string.IsNullOrWhiteSpace(_evidenceKeysEndpoint))
            {
                SetEvidenceKeysEndpoint(endpoint + Constants.EVIDENCE_KEYS_FILENAME);
            }

            return BuildEngine();
        }

        private CloudRequestData CreateAspectData(IPipeline pipeline, 
            FlowElementBase<CloudRequestData, IAspectPropertyMetaData> engine)
        {
            return new CloudRequestData(
                _dataLogger,
                pipeline,
                (IAspectEngine)engine);
        }

        /// <summary>
        /// Create a new engine using the current configuration.
        /// </summary>
        /// <param name="properties">
        /// The properties to populate.
        /// </param>
        /// <returns>
        /// A new <see cref="CloudRequestEngine"/> instance.
        /// </returns>
        protected override CloudRequestEngine NewEngine(List<string> properties)
        {
            if (string.IsNullOrWhiteSpace(_resourceKey))
            {
                throw new PipelineConfigurationException(
                    Messages.ExceptionResourceKeyNeeded);
            }

            var failThrottlingStrategy 
                = (_recoverySeconds > 0)
                ? new SimpleRecoveryStrategy(_recoverySeconds)
                : (IRecoveryStrategy)new InstantRecoveryStrategy();

            return new CloudRequestEngine(
                _loggerFactory.CreateLogger<CloudRequestEngine>(),
                CreateAspectData,
                _httpClient,
                new CloudRequestEngine.EndpointsAndKeys
                {
                    DataEndpoint = _dataEndpoint,
                    ResourceKey = _resourceKey,
                    LicenseKey = _licenseKey,
                    PropertiesEndpoint = _propertiesEndpoint,
                    EvidenceKeysEndpoint = _evidenceKeysEndpoint,
                    CloudRequestOrigin = _cloudRequestOrigin,
                    RequestedProperties = properties,
                },
                _timeout,
                new WindowedFailHandler(
                    failThrottlingStrategy,
                    _failuresToEnterRecovery,
                    TimeSpan.FromSeconds(_failuresWindowSeconds)));
        }
    }
}
