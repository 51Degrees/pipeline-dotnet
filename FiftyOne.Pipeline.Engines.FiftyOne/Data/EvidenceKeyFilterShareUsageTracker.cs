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

using System.Collections.Generic;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Data
{
    /// <summary>
    /// Wrapper for EvidenceKeyFilter for Share Usage, to be used with the 
    /// ShareUsageTracker to exclude specific evidence keys from the filter
    /// </summary>
    class EvidenceKeyFilterShareUsageTracker : EvidenceKeyFilterShareUsage
    {
        /// <summary>
        /// Constructor
        /// This constructor will create a filter that will include
        /// all evidence. (Except the default excluded items)
        /// </summary>
        public EvidenceKeyFilterShareUsageTracker()
            : base()
        { }

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
        public EvidenceKeyFilterShareUsageTracker(
            List<string> blockedHttpHeaders,
            List<string> includedQueryStringParams,
            bool includeSession,
            string aspSessionCookieName) 
            : base(blockedHttpHeaders,
                  includedQueryStringParams,
                  includeSession,
                  aspSessionCookieName)
        { }

        public override bool Include(string key)
        {
            // Ensure that the session and sequence values are excluded
            // from the tracker, regardless of the filter settings.
            // If these were included then all usage would always 
            // be shared as session id + sequence will always be unique.
            if (key == Constants.EVIDENCE_SESSIONID)
                return false;
            if (key == Constants.EVIDENCE_SEQUENCE)
                return false;
            return base.Include(key);
        }
    }
}
