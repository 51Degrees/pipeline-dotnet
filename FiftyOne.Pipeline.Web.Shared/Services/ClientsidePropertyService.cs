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

using System;

using System.Collections.Generic;

using Microsoft.Extensions.Primitives;
using FiftyOne.Pipeline.Core.FlowElements;
using System.Linq;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.JavaScriptBuilder.FlowElement;
using System.Globalization;
using FiftyOne.Pipeline.JsonBuilder.FlowElement;
using System.Text;
using FiftyOne.Pipeline.Web.Shared.Adapters;
using Microsoft.Extensions.Logging;
using FiftyOne.Pipeline.JavaScriptBuilder.Data;
using FiftyOne.Pipeline.JsonBuilder.Data;

namespace FiftyOne.Pipeline.Web.Shared.Services
{
    /// <summary>
    /// Class that provides functionality for the 'Client side Overrides'
    /// feature.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/web-integration.md#client-side-features">Specification</see>
    /// </summary>
    public class ClientsidePropertyService : IClientsidePropertyService
    {
        /// <summary>
        /// Pipeline
        /// </summary>
        private readonly IPipeline _pipeline;

        private readonly ILogger<ClientsidePropertyService> _logger;

        /// <summary>
        /// A list of all the HTTP headers that are requested evidence
        /// for elements that populate JavaScript properties 
        /// </summary>
        private StringValues? _headersAffectingJavaScript = null;
        private readonly object _headersAffectingJavaScriptLock = new object();

        private StringValues HeadersAffectingJavaScript
        {
            get
            {
                if (_headersAffectingJavaScript.HasValue)
                {
                    return _headersAffectingJavaScript.Value;
                }

                lock (_headersAffectingJavaScriptLock)
                {
                    if (_headersAffectingJavaScript.HasValue)
                    {
                        return _headersAffectingJavaScript.Value;
                    }

                    CollectHeadersAffectingJavaScript(out StringValues newHeaders, out bool gotExceptions);

                    if (!gotExceptions)
                    {
                        _headersAffectingJavaScript = newHeaders;
                    }
                    return newHeaders;
                }
            }
        }

        private enum ContentType
        {
            JavaScript,
            Json
        }

        /// <summary>
        /// The cache control values that will be set for the JavaScript and
        /// JSON.
        /// </summary>
        private readonly StringValues _cacheControl = new StringValues(
            new string[] {
                "private",
                "max-age=1800",
            });

        /// <summary>
        /// Create a new ClientsidePropertyService
        /// </summary>
        /// <param name="pipeline"></param>
        /// <param name="logger"></param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if a required parameter is null.
        /// </exception>
        public ClientsidePropertyService(
            IPipeline pipeline,
            ILogger<ClientsidePropertyService> logger)
        {
            if (pipeline == null) throw new ArgumentNullException(nameof(pipeline));

            _pipeline = pipeline;
            _logger = logger;
        }

        private void CollectHeadersAffectingJavaScript(out StringValues headers, out bool gotExceptions)
        {
            var headersAffectingJavaScript = new List<string>();
            gotExceptions = false;

            foreach (var flowElement in _pipeline.FlowElements)
            {
                IEvidenceKeyFilter filter;
                try
                {
                    filter = flowElement.EvidenceKeyFilter;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to get {nameof(flowElement.EvidenceKeyFilter)} from {{flowElementType}}", flowElement.GetType().Name);
                    gotExceptions = true;
                    continue;
                }

                // If the filter is a white list or derived type then
                // get all HTTP header evidence keys from white list
                // and add them to the headers that could affect the 
                // generated JavaScript.
                var inclusionList = filter as EvidenceKeyFilterWhitelist;
                if (inclusionList != null)
                {
                    headersAffectingJavaScript.AddRange(inclusionList.Whitelist
                        .Where(entry => entry.Key.StartsWith(
                            Core.Constants.EVIDENCE_HTTPHEADER_PREFIX + 
                            Core.Constants.EVIDENCE_SEPERATOR, 
                            StringComparison.OrdinalIgnoreCase) &&
                            // Exclude any header names that contain
                            // control characters.
                            entry.Key.Any(c => char.IsControl(c)) == false)
                        .Select(entry => entry.Key.Substring(entry.Key.IndexOf(
                            Core.Constants.EVIDENCE_SEPERATOR, 
                            StringComparison.OrdinalIgnoreCase) + 1))
                        // Only include headers that are not already in
                        // the list.
                        .Where(entry => headersAffectingJavaScript.Contains(
                            entry, StringComparer.OrdinalIgnoreCase) == false)
                        .Distinct(StringComparer.OrdinalIgnoreCase));
                }
            }
            headers = new StringValues(headersAffectingJavaScript.ToArray());
        }

        /// <summary>
        /// Add the JavaScript from the flow data object to the HttpResponse
        /// </summary>
        /// <param name="context">
        /// An <see cref="IContextAdapter"/> representing the HttpResponse 
        /// to add the JavaScript to.
        /// </param>
        /// <param name="flowData">
        /// The flow data to get the JavaScript from.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if a required parameter is null.
        /// </exception>
        public void ServeJavascript(IContextAdapter context, IFlowData flowData)
        {
            ServeContent(context, flowData, ContentType.JavaScript);
        }

        /// <summary>
        /// Add the JSON from the flow data object to the HttpResponse
        /// </summary>
        /// <param name="context">
        /// An <see cref="IContextAdapter"/> representing the HttpResponse 
        /// to add the JSON to.
        /// </param>
        /// <param name="flowData">
        /// The flow data to get the JSON from.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if a required parameter is null.
        /// </exception>
        public void ServeJson(IContextAdapter context, IFlowData flowData)
        { 
            ServeContent(context, flowData, ContentType.Json);
        }

        private void ServeContent(IContextAdapter context, IFlowData flowData, ContentType contentType)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (flowData == null) throw new ArgumentNullException(nameof(flowData));

            context.Response.Clear();
            context.Response.ClearHeaders();

            bool hadFailures = false;

            IEvidenceKeyFilter pipelineEvidenceKeyFilter = null;
            try
            {
                pipelineEvidenceKeyFilter = _pipeline.EvidenceKeyFilter;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to get {nameof(_pipeline.EvidenceKeyFilter)} from {nameof(_pipeline)}");
                hadFailures = true;
            }

            // Get the hash code.
            int? hash = null;
            if (pipelineEvidenceKeyFilter != null) {
                hash = flowData.GenerateKey(pipelineEvidenceKeyFilter).GetHashCode();
            }

            if (hash.HasValue &&
                int.TryParse(context.Request.GetHeaderValue("If-None-Match"), 
                    out int previousHash) &&
                hash == previousHash)
            {
                // The response hasn't changed so respond with a 304.
                context.Response.StatusCode = 304;
            }
            else
            {
                // Otherwise, return the requested content to the client.
                string content = null;
                Func<IJsonBuilderElementData> GetJsonData = () =>
                {
                    var jsonElement = flowData.Pipeline.GetElement<JsonBuilderElement>();
                    if (jsonElement == null)
                    {
                        throw new PipelineConfigurationException(
                            Messages.ExceptionNoJsonBuilder);
                    }
                    IJsonBuilderElementData jsonData = null;
                    try
                    {
                        jsonData = flowData.GetFromElement(jsonElement);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to get data from {flowElementType}", jsonElement.GetType().Name);
                        jsonData = jsonElement.GetFallbackResponse(flowData);
                        hadFailures = true;
                    }
                    return jsonData;
                };

                Func<IJavaScriptBuilderElementData> GetJsData = () =>
                {
                    var jsElement = flowData.Pipeline.GetElement<JavaScriptBuilderElement>();
                    if (jsElement == null)
                    {
                        throw new PipelineConfigurationException(
                            Messages.ExceptionNoJavaScriptBuilder);
                    }
                    IJavaScriptBuilderElementData jsData;
                    try
                    {
                        jsData = flowData.GetFromElement(jsElement);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to get data from {flowElementType}", jsElement.GetType().Name);
                        jsData = jsElement.GetFallbackResponse(flowData, GetJsonData());
                        hadFailures = true;
                    }
                    return jsData;
                };

                switch (contentType)
                {
                    case ContentType.JavaScript:
                        content = GetJsData()?.JavaScript;
                        break;
                    case ContentType.Json:
                        content = GetJsonData()?.Json;
                        break;
                    default:
                        break;
                }

                int length = 0;
                if (content != null && content.Length > 0)
                {
                    length = Encoding.UTF8.GetBytes(content).Length;
                }

                context.Response.StatusCode = 200;
                SetHeaders(context, 
                    hash.HasValue ? hash.Value.ToString(CultureInfo.InvariantCulture) : null,
                    length,
                    contentType == ContentType.JavaScript ? "x-javascript" : "json",
                    !hadFailures);

                context.Response.Write(content);
            }

        }

        /// <summary>
        /// Set various HTTP headers on the JavaScript response.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="hash"></param>
        /// <param name="contentLength"></param>
        /// <param name="contentType"></param>
        /// <param name="shouldCache"></param>
        private void SetHeaders(IContextAdapter context, string hash, int contentLength, string contentType, bool shouldCache)
        {
            try
            {
                context.Response.SetHeader("Content-Type",
                    $"application/{contentType}");
                context.Response.SetHeader("Content-Length",
                    contentLength.ToString(CultureInfo.InvariantCulture));
                context.Response.SetHeader("Cache-Control", shouldCache ? _cacheControl.ToString() : "no-cache");
                var headersAffectingJavaScript = HeadersAffectingJavaScript;
                if (string.IsNullOrEmpty(headersAffectingJavaScript.ToString()) == false)
                {
                    context.Response.SetHeader("Vary", headersAffectingJavaScript);
                }
                if (!string.IsNullOrEmpty(hash))
                {
                    context.Response.SetHeader("ETag", new StringValues(
                        new string[] {
                    hash,
                        }));
                }
                var origin = GetAllowOrigin(context.Request);
                if (origin != null)
                {
                    context.Response.SetHeader("Access-Control-Allow-Origin", origin);
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, Messages.MessageJavaScriptCachingError);
            }
        }

        /// <summary>
        /// Returns the value for the Access-Control-Allow-Origin response header by inspecting the Origin header
        /// of the request. See https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Origin for details associated
        /// with the Origin header.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        private string GetAllowOrigin(IRequestAdapter request)
        {
            var value = request.GetHeaderValue("Origin");
            if (String.IsNullOrEmpty(value) == false && "null".Equals(value) == false)
            {
                return value;
            }
            return null;
        }
    }
}
