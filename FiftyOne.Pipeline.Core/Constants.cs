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

namespace FiftyOne.Pipeline.Core
{
    /// <summary>
    /// Class containing values for commonly used evidence keys
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", 
        "CA1707:Identifiers should not contain underscores", 
        Justification = "51Degrees coding style is for constant names " +
            "to be all-caps with an underscore to separate words.")]
    public static class Constants
    {
        /// <summary>
        /// The string used to split evidence name parts
        /// </summary>
        public const string EVIDENCE_SEPERATOR = ".";

        /// <summary>
        /// Used to prefix evidence that is obtained from HTTP headers 
        /// </summary>
        public const string EVIDENCE_HTTPHEADER_PREFIX = "header";
        /// <summary>
        /// Used to prefix evidence that is obtained from HTTP bookies 
        /// </summary>
        public const string EVIDENCE_COOKIE_PREFIX = "cookie";
        /// <summary>
        /// Used to prefix evidence that is obtained from an HTTP request's
        /// query string or is passed into the pipeline for off-line 
        /// processing.
        /// </summary>
        public const string EVIDENCE_QUERY_PREFIX = "query";
        /// <summary>
        /// Used to prefix evidence that is obtained from the server
        /// that the Pipeline is running on.
        /// </summary>
        public const string EVIDENCE_SERVER_PREFIX = "server";
        /// <summary>
        /// Used to prefix evidence that is obtained relating to the user's
        /// session.
        /// </summary>
        public const string EVIDENCE_SESSION_PREFIX = "session";

        /// <summary>
        /// The suffix used when the User-Agent is passed as evidence.
        /// </summary>
        public const string EVIDENCE_USERAGENT = "user-agent";

        /// <summary>
        /// The complete key to be used when the client IP address is
        /// passed as evidence
        /// </summary>
        public const string EVIDENCE_CLIENTIP_KEY = EVIDENCE_SERVER_PREFIX + EVIDENCE_SEPERATOR + "client-ip";

        /// <summary>
        /// The complete key to be used when the User-Agent is
        /// passed as evidence in the query string or is set from
        /// a data store for off-line processing.
        /// </summary>
        public const string EVIDENCE_QUERY_USERAGENT_KEY = EVIDENCE_QUERY_PREFIX + EVIDENCE_SEPERATOR + EVIDENCE_USERAGENT;

        /// <summary>
        /// The complete key to be used when the User-Agent is
        /// passed as evidence in the HTTP headers.
        /// </summary>
        public const string EVIDENCE_HEADER_USERAGENT_KEY = EVIDENCE_HTTPHEADER_PREFIX + EVIDENCE_SEPERATOR + EVIDENCE_USERAGENT;

        /// <summary>
        /// Used by the Pipeline to store the session object if one 
        /// is available.
        /// </summary>
        public const string EVIDENCE_SESSION_KEY = EVIDENCE_SESSION_PREFIX + EVIDENCE_SEPERATOR + "session";

        /// <summary>
        /// The complete key to be used when the 'Protocol' HTTP header is
        /// passed as evidence
        /// </summary>
        public const string EVIDENCE_PROTOCOL = EVIDENCE_HTTPHEADER_PREFIX + EVIDENCE_SEPERATOR + "protocol";

        /// <summary>
        /// The default value for the flag that controls whether the pipeline will automatically 
        /// dispose of its elements when it is disposed.
        /// </summary>
        public const bool PIPELINE_BUILDER_DEFAULT_AUTO_DISPOSE_ELEMENTS = true;

        /// <summary>
        /// The default value for the flag that controls whether the pipeline will allow exceptions
        /// from flow elements to bubble up to the caller, or be caught and logged.
        /// </summary>
        [ObsoleteAttribute("This constant is obsolete. Use " + nameof(PIPELINE_BUILDER_DEFAULT_AUTO_SUPRESS_PROCESS_EXCEPTIONS) + " instead.", false)]
        public const bool PIPELINE_BUILDER_DEFAULT_AUTO_SUPRESS_PROCESS_EXCEPTION = PIPELINE_BUILDER_DEFAULT_AUTO_SUPRESS_PROCESS_EXCEPTIONS;

        /// <summary>
        /// The default value for the flag that controls whether the pipeline will allow exceptions
        /// from flow elements to bubble up to the caller, or be caught and logged.
        /// </summary>
        public const bool PIPELINE_BUILDER_DEFAULT_AUTO_SUPRESS_PROCESS_EXCEPTIONS = false;
    }
}
