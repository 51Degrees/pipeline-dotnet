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
using System.Collections.Specialized;
using System.Linq;
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
        /// <exception cref="AggregateException">
        /// Thrown if an error occurred during processing, 
        /// unless inderlying <see ref="IPipeline.SuppressProcessExceptions"/> is true.
        /// </exception>
        public static IFlowData Process(HttpRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            // Create a new FlowData instance.
            var flowData = GetInstance().Pipeline.CreateFlowData();

            try
            {
                EvidenceFiller filler;
                try
                {
                    filler = new EvidenceFiller(flowData);
                }
                catch (Exception ex)
                {
                    flowData.AddError(ex, null);
                    throw;
                }

                filler.CheckAndAddAll("header.", request.Headers, (headers, k) => headers[k]);
                filler.CheckAndAddAll("cookie.", request.Cookies, (cookies, k) => cookies[k].Value);
                filler.CheckAndAddAll("query.", request.QueryString, (query, k) => query[k]);
                if (request.RequestContext.HttpContext.Session is HttpSessionStateBase session)
                {
                    filler.CheckAndAdd("session", new AspFrameworkSession(session));
                    filler.CheckAndAddAll("", session.Keys, (k) => session[k]);
                }

                // Add form parameters to the evidence.
                if (request.HttpMethod == Shared.Constants.METHOD_POST &&
                    Shared.Constants.CONTENT_TYPE_FORM.Contains(request.ContentType))
                {
                    filler.CheckAndAddAll(Core.Constants.EVIDENCE_QUERY_PREFIX
                        + Core.Constants.EVIDENCE_SEPERATOR, request.Form, (form, k) => form[k]);
                }

                filler.CheckAndAdd("server.client-ip", request.UserHostAddress);
                filler.AddRequestProtocolToEvidence(request);

                if (filler.Errors is IList<Exception> errors)
                {
                    foreach (var error in errors)
                    {
                        flowData.AddError(error, null);
                    }
                    throw new AggregateException(errors);
                }

                // Process the evidence and return the result
                flowData.Process();

                if (GetInstance().SetHeaderPropertiesEnabled &&
                    request.RequestContext.HttpContext.ApplicationInstance != null)
                {
                    // Set HTTP headers in the response.
                    SetHeadersProvider.GetInstance().SetHeaders(flowData,
                        request.RequestContext.HttpContext.ApplicationInstance.Context);
                }
            } 
            catch (Exception ex)
            {
                var shouldSuppress 
                    // Suppress all ?
                    = GetInstance().Pipeline.SuppressProcessExceptions
                    // thrown by `EvidenceKeyFilter` ?
                    || ex is PipelineTemporarilyUnavailableException
                    // thrown by `Process` ?
                    || (ex is AggregateException 
                    && ex.InnerException is PipelineTemporarilyUnavailableException);
                if (!shouldSuppress)
                {
                    Exception ex2 = null;
                    try
                    {
                        flowData.Dispose();
                    }
                    catch (Exception ex3)
                    {
                        ex2 = ex3;
                    }
                    if (ex2 is null)
                    {
                        if (ex is AggregateException) { throw; }
                        throw new AggregateException(ex);
                    }
                    throw new AggregateException(new Exception[] { ex, ex2 });
                }
            }

            return flowData;
        }

        /// <summary>
        /// A convenience wrapper around IFlowData.
        /// Reduces amount of calls to `IFlowData.EvidenceKeyFilter`
        /// </summary>
        public struct EvidenceFiller
        {
            private readonly IFlowData _flowData;
            private readonly IEvidenceKeyFilter _evidenceKeyFilter;
            private IList<Exception> _errors;

            /// <summary>
            /// A collection of exceptions thrown while trying to set data.
            /// </summary>
            public IList<Exception> Errors => _errors;

            /// <param name="flowData">
            /// The <see cref="IFlowData"/> to add the evidence to.
            /// </param>
            /// <exception cref="PipelineException">
            /// thrown if <see cref="IFlowData.EvidenceKeyFilter"/> is null
            /// or re-thrown from <see cref="IFlowData.EvidenceKeyFilter"/> itself
            /// </exception>
            public EvidenceFiller(IFlowData flowData)
            {
                _flowData = flowData;
                _evidenceKeyFilter = flowData.EvidenceKeyFilter;
                if (_evidenceKeyFilter is null)
                {
                    throw new PipelineException($"Failed to retrieve {nameof(flowData.EvidenceKeyFilter)} from {nameof(flowData)}");
                }
                _errors = null;
            }

            /// <summary>
            /// Check if the given key is needed by the given flowdata.
            /// If it is then add it as evidence.
            /// </summary>
            /// <param name="key">
            /// The evidence key
            /// </param>
            /// <param name="value">
            /// The evidence value
            /// </param>
            public void CheckAndAdd(string key, object value)
            {
                try
                {
                    if (_evidenceKeyFilter.Include(key))
                    {
                        _flowData.AddEvidence(key, value);
                    }
                }
                catch (Exception ex)
                {
                    if (_errors is null)
                    {
                        _errors = new List<Exception>();
                    }
                    _errors.Add(ex);
                }
            }

            /// <summary>
            /// Convenience loop wrapper for enumerable of keys
            /// </summary>
            public void CheckAndAddAll(string keyPrefix, System.Collections.IEnumerable keys, Func<string, object> valueGetter)
            {
                foreach (string k in keys)
                {
                    CheckAndAdd(keyPrefix + k, valueGetter(k));
                }
            }

            /// <summary>
            /// Convenience loop wrapper for NameObjectCollectionBase derivatives
            /// </summary>
            public void CheckAndAddAll<T>(string keyPrefix, T nameObjectCollection, Func<T, string, object> valueGetter) where T : NameObjectCollectionBase
                => CheckAndAddAll(keyPrefix, nameObjectCollection.Keys, (k) => valueGetter(nameObjectCollection, k));

            /// <summary>
            /// Get the request protocol using .NET's Request object
            /// 'isHttps'. Fall back to non-standard headers.
            /// </summary>
            public void AddRequestProtocolToEvidence(HttpRequest request)
            {
                string protocol;
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
                CheckAndAdd(Core.Constants.EVIDENCE_PROTOCOL, protocol);
            }
        }
    }
}
