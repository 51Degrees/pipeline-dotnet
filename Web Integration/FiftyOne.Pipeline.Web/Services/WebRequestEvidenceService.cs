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
using System.Globalization;
using System.IO;
using System.Linq;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Web.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FiftyOne.Pipeline.Web.Services
{
    /// <summary>
    /// A helper service that is used to add evidence from a web request 
    /// to a <see cref="IFlowData"/> instance.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/web-integration.md#populating-evidence">Specification</see>
    /// </summary>
    public class WebRequestEvidenceService : IWebRequestEvidenceService
    {
        /// <summary>
        /// Logger
        /// </summary>
        private ILogger<WebRequestEvidenceService> _logger;

        /// <summary>
        /// True if session is enabled.
        /// </summary>
        private bool _sessionEnabled;
        
        /// <summary>
        /// True if session has been checked for. Once it has been checked it
        /// will not change.
        /// </summary>
        private bool _checkedForSession = false;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">A logger</param>
        public WebRequestEvidenceService(
            ILogger<WebRequestEvidenceService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Check whether or not session is enabled. If it is not, then don't
        /// try to get evidence from it as an exception may be thrown.
        /// </summary>
        /// <param name="httpRequest">
        /// The request to check for a session.
        /// </param>
        /// <returns>
        /// True if session is enabled.
        /// </returns>
        private bool GetSessionEnabled(HttpRequest httpRequest)
        {
            if (_checkedForSession == false)
            {
                try
                {
                    if (httpRequest.HttpContext != null &&
                        httpRequest.HttpContext.Session != null)
                    {
                        _sessionEnabled = true;
                    }
                    else
                    {
                        _sessionEnabled = false;
                    }
                }
#pragma warning disable CA1031 // Do not catch general exception types
                // This is a non-critical operation so we just
                // want to catch any exceptions and handle them
                // the same way, by disabling the feature.
                catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    _sessionEnabled = false;
                }
                _checkedForSession = true;
            }
            return _sessionEnabled;
        }

        /// <summary>
        /// Use the specified <see cref="HttpRequest"/> to populated the 
        /// <see cref="IFlowData"/> with evidence.
        /// </summary>
        /// <param name="flowData">
        /// The <see cref="IFlowData"/> to populate.
        /// </param>
        /// <param name="httpRequest">
        /// The <see cref="HttpRequest"/> to pull values from.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if a required parameter is null.
        /// </exception>
        public void AddEvidenceFromRequest(IFlowData flowData, HttpRequest httpRequest)
        {
            if (httpRequest == null) throw new ArgumentNullException(nameof(httpRequest));
            if (flowData == null) throw new ArgumentNullException(nameof(flowData));

            try
            {
                foreach (var header in httpRequest.Headers)
                {
                    string evidenceKey = Core.Constants.EVIDENCE_HTTPHEADER_PREFIX +
                        Core.Constants.EVIDENCE_SEPERATOR + header.Key;
                    CheckAndAdd(flowData, evidenceKey, header.Value.ToString());
                }
                foreach (var cookie in httpRequest.Cookies)
                {
                    string evidenceKey = Core.Constants.EVIDENCE_COOKIE_PREFIX +
                        Core.Constants.EVIDENCE_SEPERATOR + cookie.Key;
                    CheckAndAdd(
                        flowData, 
                        evidenceKey, 
                        cookie.Value == null ? "" : 
                            cookie.Value.ToString(CultureInfo.InvariantCulture));
                }
                foreach (var queryValue in httpRequest.Query)
                {
                    string evidenceKey = Core.Constants.EVIDENCE_QUERY_PREFIX +
                        Core.Constants.EVIDENCE_SEPERATOR + queryValue.Key;
                    CheckAndAdd(flowData, evidenceKey, queryValue.Value.ToString());
                }
                // Add form parameters to the evidence.
                if (httpRequest.Method == Shared.Constants.METHOD_POST &&
                    Shared.Constants.CONTENT_TYPE_FORM.Contains(httpRequest.ContentType))
                {
                    try
                    {
                        foreach (var formValue in httpRequest.Form)
                        {
                            string evidenceKey = Core.Constants.EVIDENCE_QUERY_PREFIX +
                                Core.Constants.EVIDENCE_SEPERATOR + formValue.Key;
                            CheckAndAdd(flowData, evidenceKey, formValue.Value.ToString());
                        }
                    }
                    catch (InvalidDataException e)
                    {
                        _logger.LogInformation(e,
                            Messages.MessageInvalidForm);
                    }
                }
                if (GetSessionEnabled(httpRequest))
                {
                    foreach (var sessionKey in httpRequest.HttpContext.Session.Keys)
                    {
                        string evidenceKey = Core.Constants.EVIDENCE_SESSION_PREFIX +
                            Core.Constants.EVIDENCE_SEPERATOR + sessionKey;
                        CheckAndAdd(flowData, evidenceKey, httpRequest.HttpContext.Session.GetString(sessionKey));
                    }
                    CheckAndAdd(flowData, Core.Constants.EVIDENCE_SESSION_KEY,
                        new AspCoreSession(httpRequest.HttpContext.Session));
                }

                if (httpRequest.HttpContext.Connection?.RemoteIpAddress != null)
                {
                    CheckAndAdd(flowData, Core.Constants.EVIDENCE_CLIENTIP_KEY,
                        httpRequest.HttpContext.Connection.RemoteIpAddress.ToString());
                }

                AddRequestProtocolToEvidence(flowData, httpRequest);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                _logger.LogWarning(ex, Messages.MessageEvidenceError);
            }
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
        private void AddRequestProtocolToEvidence(IFlowData flowData, HttpRequest request)
        {
            string protocol = "https";
            if (request.IsHttps)
            {
                protocol = "https";
            }
            else if (request.Headers.ContainsKey("X-Origin-Proto"))
            {
                protocol = request.Headers["X-Origin-Proto"];
            }
            else if (request.Headers.ContainsKey("X-Forwarded-Proto"))
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
