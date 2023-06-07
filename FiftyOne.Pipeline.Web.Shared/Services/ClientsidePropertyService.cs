/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2022 51 Degrees Mobile Experts Limited, Davidson House,
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
using FiftyOne.Pipeline.Web.Shared;
using FiftyOne.Pipeline.JsonBuilder.FlowElement;
using System.Text;
using FiftyOne.Pipeline.Web.Shared.Adapters;
using Microsoft.Extensions.Logging;

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
        private IPipeline _pipeline;

        private ILogger<ClientsidePropertyService> _logger;

        /// <summary>
        /// A list of all the HTTP headers that are requested evidence
        /// for elements that populate JavaScript properties 
        /// </summary>
        private StringValues _headersAffectingJavaScript;

        private enum ContentType
        {
            JavaScript,
            Json
        }

        /// <summary>
        /// The cache control values that will be set for the JavaScript and
        /// JSON.
        /// </summary>
        private StringValues _cacheControl = new StringValues(
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

            var headersAffectingJavaScript = new List<string>();

            foreach (var filter in _pipeline.FlowElements.Select(e => e.EvidenceKeyFilter))
            {
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
            _headersAffectingJavaScript = new StringValues(headersAffectingJavaScript.ToArray());
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

            // Get the hash code.
            var hash = flowData.GenerateKey(_pipeline.EvidenceKeyFilter).GetHashCode();

            if (int.TryParse(context.Request.GetHeaderValue("If-None-Match"), 
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
                switch (contentType)
                {
                    case ContentType.JavaScript:
                        var jsElement = flowData.Pipeline.GetElement<JavaScriptBuilderElement>();
                        if (jsElement == null)
                        {
                            throw new PipelineConfigurationException(
                                Messages.ExceptionNoJavaScriptBuilder);
                        }
                        var jsData = flowData.GetFromElement(jsElement);
                        content = jsData?.JavaScript;
                        break;
                    case ContentType.Json:
                        var jsonElement = flowData.Pipeline.GetElement<JsonBuilderElement>();
                        if (jsonElement == null)
                        {
                            throw new PipelineConfigurationException(
                                Messages.ExceptionNoJsonBuilder);
                        }
                        var jsonData = flowData.GetFromElement(jsonElement);
                        content = jsonData?.Json;
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
                    hash.ToString(CultureInfo.InvariantCulture),
                    length,
                    contentType == ContentType.JavaScript ? "x-javascript" : "json");

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
        private void SetHeaders(IContextAdapter context, string hash, int contentLength, string contentType)
        {
            try
            {
                context.Response.SetHeader("Content-Type",
                    $"application/{contentType}");
                context.Response.SetHeader("Content-Length",
                    contentLength.ToString(CultureInfo.InvariantCulture));
                context.Response.SetHeader("Cache-Control", _cacheControl);
                if (string.IsNullOrEmpty(_headersAffectingJavaScript.ToString()) == false)
                {
                    context.Response.SetHeader("Vary", _headersAffectingJavaScript);
                }
                context.Response.SetHeader("ETag", new StringValues(
                    new string[] {
                    hash,
                    }));
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
