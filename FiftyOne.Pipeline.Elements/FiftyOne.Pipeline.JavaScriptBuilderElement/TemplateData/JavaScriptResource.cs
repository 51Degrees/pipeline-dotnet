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

using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.JavaScriptBuilder.TemplateData
{
    /// <summary>
    /// A helper class that packages the parameters required by the 
    /// JavaScript mustache template into the required format.
    /// </summary>
    public class JavaScriptResource
    {
        private string _objName;
        private string _jsonObject;
        private string _sessionId;
        private int _sequence;
        private bool _supportsPromises;
        private bool _supportsFetch;
        private Uri _url;
        private string _parameters;
        private bool _enableCookies;
        private bool _updateEnabled;
        private bool _hasDelayedProperties;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <remarks>
        /// The callback mechanism is a feature that allows the client-side
        /// data to be updated in the background when any properties that 
        /// contain JavaScript code have been executed on the client and
        /// therefore, new evidence is available.
        /// </remarks>
        /// <param name="objName">
        /// The name of the global-scope JavaScript object that will be 
        /// created on the client-side by the JavaScript produced by 
        /// the template.
        /// </param>
        /// <param name="jsonObject">
        /// The JSON data payload to be inserted into the template.
        /// </param>
        /// <param name="sessionId">
        /// The session Id to use in the JavaScript response.
        /// </param>
        /// <param name="sequence">
        /// The sequence value to use in the JavaScript response.
        /// </param>
        /// <param name="supportsPromises">
        /// If true, the template will produce JavaScript that makes 
        /// use of promises.
        /// If false, promises will not be used.
        /// </param>
        /// <param name="supportsFetch">
        /// If true, the template will produce JavaScript that makes use of the
        /// fetch API. Otherwise, the template will fall back to using 
        /// XMLHttpRequest.
        /// </param>
        /// <param name="url">
        /// The complete URL to use for the callback mechanism described 
        /// in remarks for this constructor.
        /// </param>
        /// <param name="parameters">
        /// The parameters to append to the call-back URL.
        /// </param>
        /// <param name="enableCookies">
        /// If false, any cookies created by JavaScript properties that
        /// execute on the client-side and that start with '51D_' will 
        /// be deleted automatically.
        /// </param>
        /// <param name="updateEnabled">
        /// True to use the callback mechanism that is described in 
        /// remarks for this constructor.
        /// False to disable that mechanism. In this case, a second request 
        /// must be initiated by the user in order for the server to 
        /// access the additional evidence gathered by client-side code.
        /// </param>
        /// <param name="hasDelayedProperties">
        /// True to include support for JavaScript properties that are
        /// not executed immediately when the JavaScript is loaded.
        /// </param>
        public JavaScriptResource(
            string objName,
            string jsonObject,
            string sessionId,
            int sequence,
            bool supportsPromises,
            bool supportsFetch,
            string url,
            string parameters,
            bool enableCookies,
            bool updateEnabled,
            bool hasDelayedProperties)
        {
            _objName = objName;
            _jsonObject = string.IsNullOrWhiteSpace(jsonObject) == false
                ? jsonObject : "{\"errors\":[\"Json data missing.\"]}";
            _sessionId = sessionId;
            _sequence = sequence;
            _supportsPromises = supportsPromises;
            _supportsFetch = supportsFetch;
            _url = new Uri(url);
            _parameters = parameters;
            _enableCookies = enableCookies;
            _updateEnabled = updateEnabled;
            _hasDelayedProperties = hasDelayedProperties;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <remarks>
        /// The callback mechanism is a feature that allows the client-side
        /// data to be updated in the background when any properties that 
        /// contain JavaScript code have been executed on the client and
        /// therefore, new evidence is available.
        /// </remarks>
        /// <param name="objName">
        /// The name of the global-scope JavaScript object that will be 
        /// created on the client-side by the JavaScript produced by 
        /// the template.
        /// </param>
        /// <param name="jsonObject">
        /// The JSON data payload to be inserted into the template.
        /// </param>
        /// <param name="sessionId">
        /// The session Id to use in the JavaScript response.
        /// </param>
        /// <param name="sequence">
        /// The sequence value to use in the JavaScript response.
        /// </param>
        /// <param name="supportsPromises">
        /// If true, the template will produce JavaScript that makes 
        /// use of promises.
        /// If false, promises will not be used.
        /// </param>
        /// <param name="supportsFetch">
        /// If true, the template will produce JavaScript that makes use of the
        /// fetch API. Otherwise, the template will fall back to using 
        /// XMLHttpRequest.
        /// </param>
        /// <param name="url">
        /// The complete URL to use for the callback mechanism described 
        /// in remarks for this constructor.
        /// </param>
        /// <param name="parameters">
        /// The parameters to append to the call-back URL.
        /// </param>
        /// <param name="enableCookies">
        /// If false, any cookies created by JavaScript properties that
        /// execute on the client-side and that start with '51D_' will 
        /// be deleted automatically.
        /// </param>
        /// <param name="updateEnabled">
        /// True to use the callback mechanism that is described in 
        /// remarks for this constructor.
        /// False to disable that mechanism. In this case, a second request 
        /// must be initiated by the user in order for the server to 
        /// access the additional evidence gathered by client-side code.
        /// </param>
        /// <param name="hasDelayedProperties">
        /// True to include support for JavaScript properties that are
        /// not executed immediately when the JavaScript is loaded.
        /// </param>
        public JavaScriptResource(
            string objName,
            string jsonObject,
            string sessionId,
            int sequence,
            bool supportsPromises,
            bool supportsFetch,
            Uri url,
            string parameters,
            bool enableCookies,
            bool updateEnabled,
            bool hasDelayedProperties)
        {
            _objName = objName;
            _jsonObject = string.IsNullOrWhiteSpace(jsonObject) == false
                ? jsonObject : "{\"errors\":[\"Json data missing.\"]}";
            _sessionId = sessionId;
            _sequence = sequence;
            _supportsPromises = supportsPromises;
            _supportsFetch = supportsFetch;
            _url = url;
            _parameters = parameters;
            _enableCookies = enableCookies;
            _updateEnabled = updateEnabled;
            _hasDelayedProperties = hasDelayedProperties;
        }

        /// <summary>
        /// Get the parameters supplied to this class as a 
        /// <code><![CDATA[Dictionary<string, object>]]></code>
        /// </summary>
        /// <returns>
        /// A new  <code><![CDATA[Dictionary<string, object>]]></code>
        /// containing the parameters passed to the constructor.
        /// </returns>
        public Dictionary<string, object> AsDictionary()
        {
            var hash = new Dictionary<string, object>();

            hash.Add("_objName", _objName);
            hash.Add("_jsonObject", _jsonObject);
            hash.Add("_sessionId", _sessionId);
            hash.Add("_sequence", _sequence);
            hash.Add("_supportsPromises", _supportsPromises);
            hash.Add("_supportsFetch", _supportsFetch);
            hash.Add("_url", _url?.AbsoluteUri);
            hash.Add("_parameters", _parameters);
            hash.Add("_enableCookies", _enableCookies);
            hash.Add("_updateEnabled", _updateEnabled);
            hash.Add("_hasDelayedProperties", _hasDelayedProperties);
            

            return hash;
        }
    }
}