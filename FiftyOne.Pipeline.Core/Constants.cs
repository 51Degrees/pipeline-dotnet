/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2026 51 Degrees Mobile Experts Limited, Davidson House,
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
        /// The complete key to be used when the HTTP method of the request
        /// is passed as evidence.
        /// </summary>
        /// <remarks>
        /// This key and the two below carry the request line. The values
        /// MUST be exactly what the request carried, byte for byte, with no
        /// decoding and no normalisation, because they are used to rebuild
        /// the text an HTTP message signature was made over and one changed
        /// byte makes a valid signature read as invalid. See the
        /// <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/web-integration.md#populating-evidence">Specification</see>.
        /// </remarks>
        public const string EVIDENCE_REQUEST_METHOD_KEY = EVIDENCE_SERVER_PREFIX + EVIDENCE_SEPERATOR + "request-method";

        /// <summary>
        /// The complete key to be used when the path of the request is
        /// passed as evidence, exactly as the request carried it.
        /// </summary>
        public const string EVIDENCE_REQUEST_PATH_KEY = EVIDENCE_SERVER_PREFIX + EVIDENCE_SEPERATOR + "request-path";

        /// <summary>
        /// The complete key to be used when the whole query string of the
        /// request is passed as evidence, exactly as the request carried it
        /// and without the leading question mark.
        /// </summary>
        /// <remarks>
        /// The query string is carried whole here as well as split into
        /// 'query.' entries, because the split entries lose the ordering and
        /// the encoding of the original and so cannot rebuild it. An
        /// integration that supplies the request line supplies this key on
        /// every request, empty where the request carried no query string,
        /// so that a missing key always means the integration does not
        /// supply the request line rather than that the request had no
        /// query.
        /// </remarks>
        public const string EVIDENCE_REQUEST_QUERY_KEY = EVIDENCE_SERVER_PREFIX + EVIDENCE_SEPERATOR + "request-query";

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

        /// <summary>
        /// The name of the <see cref="System.Diagnostics.ActivitySource"/>
        /// that emits a tracing span for each flow element a pipeline
        /// processes. Register this name with an OpenTelemetry tracer
        /// (AddSource) to collect the spans; nothing is emitted otherwise.
        /// </summary>
        public const string TRACING_SOURCE_NAME = "FiftyOne.Pipeline";
    }
}
