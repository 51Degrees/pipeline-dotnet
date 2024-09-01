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

using FiftyOne.Pipeline.Core.Configuration;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Core.Services;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using FiftyOne.Pipeline.JavaScriptBuilder.FlowElement;
using FiftyOne.Pipeline.JsonBuilder.FlowElement;
using FiftyOne.Pipeline.Web.Framework.Configuration;
using FiftyOne.Pipeline.Web.Framework.Providers;
using FiftyOne.Pipeline.Web.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace FiftyOne.Pipeline.Web.Framework
{
    /// <summary>
    /// Singleton class which contains an IPipeline build from a configuration
    /// file, and some extra options which are available in a web environment.
    /// </summary>
    public class WebPipeline
    {
        private static WebPipeline _instance = null;

        /// <summary>
        /// Whether or not client-side properties are enabled. If they are,
        /// then a 51Degrees.core.js will be served by the server.
        /// </summary>
        public bool ClientSideEvidenceEnabled => _options.ClientSideEvidenceEnabled;

        /// <summary>
        /// Whether or not set header properties are enabled.
        /// </summary>
        public bool SetHeaderPropertiesEnabled => _options.UseSetHeaderProperties;

        /// <summary>
        /// Extra pipeline options which only apply to an implementation in a
        /// web server.
        /// </summary>
        private readonly PipelineWebIntegrationOptions _options;

        /// <summary>
        /// Lock used when constructing a pipeline.
        /// </summary>
        private static readonly object _lock = new object();

        /// <summary>
        /// The single IPipeline instance for a web server.
        /// </summary>
        public IPipeline Pipeline { get; private set; }

        /// <summary>
        /// Get the only instance of WebPipeline. If an instance does not
        /// already exist, one is created.
        /// </summary>
        /// <returns>
        /// The single instance of WebPipeline
        /// </returns>
        public static WebPipeline GetInstance()
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new WebPipeline();
                    }
                }
            }
            return _instance;
        }

        /// <summary>
        /// Private constructor used to construct the only instance which will
        /// exist of this class.
        /// </summary>
        private WebPipeline()
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddPipelineConfig()
                .Build();
            _options = new PipelineWebIntegrationOptions();
            var section = config.GetRequiredSection("PipelineOptions");
            section.Bind(_options, (o) => { o.ErrorOnUnknownConfiguration = true; });

            if (_options == null ||
                _options.Elements == null)
            {
                throw new PipelineConfigurationException(Messages.ExceptionNoConfiguration);
            }

            // Add the sequence element.
            var sequenceConfig = _options.Elements.Where(e =>
                e.BuilderName.IndexOf(nameof(SequenceElement),
                    StringComparison.OrdinalIgnoreCase) >= 0);
            if (sequenceConfig.Any() == false)
            {
                // The sequence element is not included so add it.
                // Make sure it's added as the first element.
                _options.Elements.Insert(0, new ElementOptions()
                {
                    BuilderName = nameof(SequenceElement)
                });
            }

            if (ClientSideEvidenceEnabled)
            {
                // Client-side evidence is enabled so make sure the 
                // JsonBuilderElement and JavaScriptBundlerElement has been 
                // included.
                var jsonConfig = _options.Elements.Where(e =>
                    e.BuilderName.StartsWith(nameof(JsonBuilderElement),
                        StringComparison.OrdinalIgnoreCase));
                var javascriptConfig = _options.Elements.Where(e =>
                    e.BuilderName.StartsWith(nameof(JavaScriptBuilderElement),
                        StringComparison.OrdinalIgnoreCase));

                var jsIndex = javascriptConfig.Any() ?
                    _options.Elements.IndexOf(javascriptConfig.First()) : -1;

                if (jsonConfig.Any() == false)
                {
                    // The json builder is not included so add it.
                    var newElementOptions = new ElementOptions()
                    {
                        BuilderName = nameof(JsonBuilderElement)
                    };
                    if (jsIndex > -1)
                    {
                        // There is already a javascript builder element
                        // so insert the json builder before it.
                        _options.Elements.Insert(jsIndex, newElementOptions);
                    }
                    else
                    {
                        _options.Elements.Add(newElementOptions);
                    }
                }

                if (jsIndex == -1)
                {
                    // The javascript builder is not included so add it.
                    _options.Elements.Add(new ElementOptions()
                    {
                        BuilderName = nameof(JavaScriptBuilderElement)
                    });
                }
            }

            // Add the set headers
            var setHeadersConfig = _options.Elements.Where(e =>
                e.BuilderName.IndexOf(nameof(SetHeadersElement),
                    StringComparison.OrdinalIgnoreCase) >= 0);
            if (setHeadersConfig.Any() == false)
            {
                // The set headers element is not included, so add it.
                // Make sure it's added as the last element.
                _options.Elements.Add(new ElementOptions()
                {
                    BuilderName = nameof(SetHeadersElement)
                });
            }

            // Set up common services.
            var loggerFactory = new LoggerFactory();
            var httpClient = new System.Net.Http.HttpClient();
            var updateService = new DataUpdateService(
                loggerFactory.CreateLogger<DataUpdateService>(),
                httpClient);
            var services = new FiftyOneServiceProvider();
            // Add data update and missing property services.
            services.AddService(loggerFactory);
            services.AddService(httpClient);
            services.AddService(updateService);
            services.AddService(MissingPropertyService.Instance);

            Pipeline = new PipelineBuilder(
                loggerFactory,
                services)
                .BuildFromConfiguration(_options);
        }


        /// <summary>
        /// Populate a FlowData's evidence from the web request, and process
        /// using the Pipeline.
        /// </summary>
        /// <param name="request">
        /// Request containing the evidence to process
        /// </param>
        /// <returns>
        /// A processed FlowData
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if a required parameter is null.
        /// </exception>
        public static IFlowData Process(HttpRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            // Create a new FlowData instance.
            var flowData = GetInstance().Pipeline.CreateFlowData();
            // Add headers
            foreach (var headerName in request.Headers.AllKeys)
            {
                CheckAndAdd(flowData, "header." + headerName, request.Headers[headerName]);
            }
            // Add cookies
            foreach (var cookieName in request.Cookies.AllKeys)
            {
                CheckAndAdd(flowData, "cookie." + cookieName, request.Cookies[cookieName].Value);
            }
            // Add query parameters
            foreach (var paramName in request.QueryString.AllKeys)
            {
                CheckAndAdd(flowData, "query." + paramName, request.QueryString[paramName]);
            }
            if (request.RequestContext.HttpContext.Session != null)
            {
                CheckAndAdd(flowData, "session", new AspFrameworkSession(request.RequestContext.HttpContext.Session));
                foreach (var sessionValueName in request.RequestContext.HttpContext.Session.Keys)
                {
                    CheckAndAdd(flowData, (string)sessionValueName, request.RequestContext.HttpContext.Session[(string)sessionValueName]);
                }
            }
            // Add form parameters to the evidence.
            if (request.HttpMethod == Shared.Constants.METHOD_POST &&
                Shared.Constants.CONTENT_TYPE_FORM.Contains(request.ContentType))
            {
                foreach (var formKey in request.Form.AllKeys)
                {
                    string evidenceKey = Core.Constants.EVIDENCE_QUERY_PREFIX +
                        Core.Constants.EVIDENCE_SEPERATOR + formKey;
                    CheckAndAdd(flowData, evidenceKey, request.Form[formKey]);
                }
            }

            // Add the client IP
            CheckAndAdd(flowData, "server.client-ip", request.UserHostAddress);

            AddRequestProtocolToEvidence(flowData, request);

            // Process the evidence and return the result
            flowData.Process();

            if (GetInstance().SetHeaderPropertiesEnabled &&
                request.RequestContext.HttpContext.ApplicationInstance != null)
            {
                // Set HTTP headers in the response.
                SetHeadersProvider.GetInstance().SetHeaders(flowData,
                    request.RequestContext.HttpContext.ApplicationInstance.Context);
            }

            return flowData;
        }

        /// <summary>
        /// Check if the given key is needed by the given flowdata.
        /// If it is then add it as evidence.
        /// </summary>
        /// <param name="flowData">
        /// The <see cref="IFlowData"/> to add the evidence to.
        /// </param>
        /// <param name="key">
        /// The evidence key
        /// </param>
        /// <param name="value">
        /// The evidence value
        /// </param>
        private static void CheckAndAdd(IFlowData flowData, string key, object value)
        {
            if (flowData.EvidenceKeyFilter.Include(key))
            {
                flowData.AddEvidence(key, value);
            }
        }

        /// <summary>
        /// Get the request protocol using .NET's Request object
        /// 'isHttps'. Fall back to non-standard headers.
        /// </summary>
        private static void AddRequestProtocolToEvidence(IFlowData flowData, HttpRequest request)
        {
            string protocol = "https";
            if (request.IsSecureConnection)
            {
                protocol = "https";
            }
            else if (request.Headers.AllKeys.Contains("X-Origin-Proto"))
            {
                protocol = request.Headers["X-Origin-Proto"];
            }
            else if (request.Headers.AllKeys.Contains("X-Forwarded-Proto"))
            {
                protocol = request.Headers["X-Forwarded-Proto"];
            }
            else
            {
                protocol = "http";
            }

            // Add protocol to the evidence.
            CheckAndAdd(flowData, Core.Constants.EVIDENCE_PROTOCOL, protocol);
        }
    }
}
