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

using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.Engines.FiftyOne.Trackers;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Data
{
    /// <summary>
    /// This filter is used by the <see cref="ShareUsageElement"/>.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/pipeline-elements/usage-sharing-element.md#accepted-evidence">Specification</see>
    /// </summary>
    /// <remarks>
    /// As this filter is generally inclusive, it will often cause far more
    /// evidence to be passed into a pipeline than the engine-specific 
    /// filters, which tend to be based on a list of values to include such as
    /// <see cref="EvidenceKeyFilterWhitelist"/>.
    /// </remarks>
    public class EvidenceKeyFilterShareUsage : IEvidenceKeyFilter
    {
        /// <summary>
        /// If true then the asp.net session cookie will be included in
        /// the filter.
        /// </summary>
        /// <remarks>
        /// The session cookie is used by the <see cref="ShareUsageTracker"/> 
        /// but we do not actually want to share it.
        /// </remarks>
        private bool _includeSession;

        /// <summary>
        /// If true then all evidence will always be shared.
        /// </summary>
        private bool _shareAll = false;

        /// <summary>
        /// The cookie name being used to store the asp.net session id.
        /// </summary>
        private string _aspSessionCookieName;

        /// <summary>
        /// The content of HTTP headers in this array will not be included in 
        /// the request information sent to 51degrees.
        /// Any header names added here are hard-coded to be blocked
        /// regardless of the settings passed to the constructor.
        /// </summary>
        private HashSet<string> _blockedHttpHeaders = new HashSet<string>()
        {
            "cookies"
        };

        /// <summary>
        /// Query string parameters will not be shared by default.
        /// Any query string parameters to be shared must be added to this
        /// collection.
        /// </summary>
        private HashSet<string> _includedQueryStringParams = new HashSet<string>();

        /// <summary>
        /// Constructor
        /// Using this constructor will create a filter that allows all 
        /// evidence. I.e. All evidence will be shared.
        /// </summary>
        public EvidenceKeyFilterShareUsage()
        {
            _shareAll = true;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="blockedHttpHeaders">
        /// A list of the names of the HTTP headers that share usage should
        /// not send to 51Degrees.
        /// </param>
        /// <param name="includedQueryStringParams">
        /// A list of the names of query string parameters that share 
        /// usage should send to 51Degrees.
        /// If this value is null, all query string parameters are shared.
        /// </param>
        /// <param name="includeSession">
        /// If true then the asp.net session cookie will be included in
        /// the filter.
        /// </param>
        /// <param name="aspSessionCookieName">
        /// The name of the cookie that contains the asp.net session id. 
        /// </param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", 
            "CA1308:Normalize strings to uppercase", 
            Justification = "Pipeline specification requires keys to be " +
                "all lower-case")]
        public EvidenceKeyFilterShareUsage(
            List<string> blockedHttpHeaders,
            List<string> includedQueryStringParams,
            bool includeSession,
            string aspSessionCookieName)
        {
            if(blockedHttpHeaders == null)
            {
                throw new ArgumentNullException(nameof(blockedHttpHeaders));
            }
            if (aspSessionCookieName == null)
            {
                throw new ArgumentNullException(nameof(aspSessionCookieName));
            }

            _includeSession = includeSession;
            _aspSessionCookieName = aspSessionCookieName.ToLowerInvariant();
            foreach (var header in blockedHttpHeaders)
            {
                var lowerHeader = header.ToLowerInvariant();
                if (!_blockedHttpHeaders.Contains(lowerHeader))
                {
                    _blockedHttpHeaders.Add(lowerHeader);
                }
            }
            if (includedQueryStringParams == null)
            {
                _includedQueryStringParams = null;
            } 
            else
            { 
                foreach (var parameter in includedQueryStringParams)
                {
                    var lowerParameter = parameter.ToLowerInvariant();
                    if (!_includedQueryStringParams.Contains(lowerParameter))
                    {
                        _includedQueryStringParams.Add(lowerParameter);
                    }
                }
            }
        }

        /// <summary>
        /// Check if the specified evidence key is included by this filter.
        /// </summary>
        /// <param name="key">
        /// The key to check
        /// </param>
        /// <returns>
        /// True if the key is included and false if not.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied key is null
        /// </exception>
        public virtual bool Include(string key)
        {
            if(key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            bool result = _shareAll;

            if (_shareAll == false)
            {
                int firstSeperator = key.IndexOf(Core.Constants.EVIDENCE_SEPERATOR,
                    StringComparison.OrdinalIgnoreCase);
                if (firstSeperator > 0)
                {
                    string firstPart = key.Remove(firstSeperator);
                    string lastPart = key.Substring(firstSeperator + 1);

                    if (firstPart == Core.Constants.EVIDENCE_HTTPHEADER_PREFIX)
                    {
                        // Add the header to the list if the header name does not 
                        // appear in the list of blocked headers.
                        result = _blockedHttpHeaders
#pragma warning disable CA1308 // Normalize strings to uppercase
                            // Pipeline specification is for keys to be lower-case.
                            .Contains(lastPart.ToLowerInvariant()) == false;
#pragma warning restore CA1308 // Normalize strings to uppercase
                    }
                    else if (firstPart == Core.Constants.EVIDENCE_COOKIE_PREFIX)
                    {
                        // Only add cookies that start with the 51Degrees cookie 
                        // prefix.
                        result = lastPart.StartsWith(Engines.Constants.FIFTYONE_COOKIE_PREFIX,
                            StringComparison.OrdinalIgnoreCase) ||
                            (_includeSession && lastPart.Equals(_aspSessionCookieName,
                                StringComparison.OrdinalIgnoreCase));
                    }
                    else if (firstPart == Core.Constants.EVIDENCE_SESSION_PREFIX)
                    {
                        // Only add session values that start with the 51Degrees
                        // cookie prefix.
                        result = lastPart.StartsWith(Engines.Constants.FIFTYONE_COOKIE_PREFIX,
                            StringComparison.OrdinalIgnoreCase);
                    }
                    else if (firstPart == Core.Constants.EVIDENCE_QUERY_PREFIX)
                    {
                        // If no query string parameter filter was specified 
                        // then share all of them.
                        // Otherwise, only include query string parameters that 
                        // start with 51d_ or that have been specified in 
                        // the constructor.
                        result = _includedQueryStringParams == null ||
                            lastPart.StartsWith(Engines.Constants.FIFTYONE_COOKIE_PREFIX,
                            StringComparison.OrdinalIgnoreCase) ||
                            _includedQueryStringParams.Contains(
#pragma warning disable CA1308 // Normalize strings to uppercase
                            // Pipeline specification is for keys to be lower-case.
                            lastPart.ToLowerInvariant());
#pragma warning restore CA1308 // Normalize strings to uppercase
                    }
                    else
                    {
                        // Add anything that is not a cookie, header, session 
                        // variable or quest string parameter.
                        result = true;
                    }
                }
                else
                {
                    result = true;
                }
            }

            return result;
        }

        /// <summary>
        /// Return the 'order of precedence of the specified evidence
        /// key.
        /// Lower values indicate evidence where the value is more likely
        /// to provide differentiation between requests. This is used by 
        /// features such as Caching to improve performance when checking
        /// if one set of evidence matches another.
        /// </summary>
        /// <param name="key">
        /// The key to get the order for.
        /// </param>
        /// <returns>
        /// The order, where lower values indicate a higher order of 
        /// precedence. 
        /// </returns>
        public int? Order(string key)
        {
            return 100;
        }
    }
}
