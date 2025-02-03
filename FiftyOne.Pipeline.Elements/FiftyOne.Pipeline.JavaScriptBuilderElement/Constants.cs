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

using FiftyOne.Pipeline.JavaScriptBuilder.FlowElement;

namespace FiftyOne.Pipeline.JavaScriptBuilder
{
    /// <summary>
    /// Static class containing various constants that are used by the 
    /// Pipeline and/or are helpful to callers. 
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming",
        "CA1707:Identifiers should not contain underscores",
        Justification = "51Degrees coding style is for constant names " +
            "to be all-caps with an underscore to separate words.")]
    public static class Constants
    {
        /// <summary>
        /// The complete key to be used when the 'Host' HTTP header is
        /// passed as evidence
        /// </summary>
        public const string EVIDENCE_HOST_KEY = 
            Core.Constants.EVIDENCE_HTTPHEADER_PREFIX + 
            Core.Constants.EVIDENCE_SEPERATOR + 
            "Host";

        /// <summary>
        /// The suffix used when the <see cref="JavaScriptBuilderElement"/> 
        /// 'object name' parameter is supplied as evidence.
        /// </summary>
        public const string EVIDENCE_OBJECT_NAME_SUFFIX =
            "fod-js-object-name";

        /// <summary>
        /// The suffix used when the <see cref="JavaScriptBuilderElement"/> 
        /// 'enable cookies' parameter is supplied as evidence.
        /// </summary>
        public const string EVIDENCE_ENABLE_COOKIES_SUFFIX =
            "fod-js-enable-cookies";

        /// <summary>
        /// The complete key to be used when the 
        /// <see cref="JavaScriptBuilderElement"/> 'object name' 
        /// parameter is supplied as part of the query 
        /// string.
        /// </summary>
        public const string EVIDENCE_OBJECT_NAME =
            Core.Constants.EVIDENCE_QUERY_PREFIX +
            Core.Constants.EVIDENCE_SEPERATOR +
            EVIDENCE_OBJECT_NAME_SUFFIX;

        /// <summary>
        /// The complete key to be used when the 
        /// <see cref="JavaScriptBuilderElement"/> 'enable cookies' 
        /// parameter is supplied as part of the query 
        /// string.
        /// </summary>
        public const string EVIDENCE_ENABLE_COOKIES =
            Core.Constants.EVIDENCE_QUERY_PREFIX +
            Core.Constants.EVIDENCE_SEPERATOR +
            EVIDENCE_ENABLE_COOKIES_SUFFIX;

        /// <summary>
        /// The key to access the embedded resource that contains the 
        /// mustache template that is used by the 
        /// <see cref="JavaScriptBuilderElement"/> 
        /// </summary>
        internal const string TEMPLATE = "FiftyOne.Pipeline.JavaScriptBuilder.Templates.JavaScriptResource.mustache";

        /// <summary>
        /// The protocol used by the <see cref="JavaScriptBuilderElement"/> when creating
        /// a callback URL if no other protocol value was found or specified.
        /// </summary>
        public const string FALLBACK_PROTOCOL = "https";

        /// <summary>
        /// The default value for the JavaScript 'object name' used by
        /// the <see cref="JavaScriptBuilderElement"/>.
        /// </summary>
        public const string BUILDER_DEFAULT_OBJECT_NAME = "fod";

        /// <summary>
        /// Default protocol for the JavaScriptBuilderElementBuilder.
        /// An empty string means that it will use whatever protocol is in the evidence collection. 
        /// I.e. whichever protocol the original request used.
        /// </summary>
        public const string BUILDER_DEFAULT_PROTOCOL = "";

        /// <summary>
        /// Default host for the JavaScriptBuilderElementBuilder.
        /// An empty string means that it will use whatever host is in the evidence collection. 
        /// I.e. whichever host the original request used.
        /// </summary>
        public const string BUILDER_DEFAULT_HOST = "";

        /// <summary>
        /// Default endpoint for the JavaScriptBuilderElementBuilder.
        /// Note - this constant is redefinied here for consistency with other JS builder defaults.
        /// </summary>
        public const string BUILDER_DEFAULT_ENDPOINT = Engines.Constants.DEFAULT_JSON_ENDPOINT;

        /// <summary>
        /// Default value for the flag that controls whether the JavaScript generated by 
        /// JavaScriptBuilderElement should be minified or not. 
        /// </summary>
        public const bool BUILDER_DEFAULT_MINIFY = true;

        /// <summary>
        /// Default value for the flag that controls whether the JavaScript generated by 
        /// JavaScriptBuilderElement should store results of processing in cookies or not.
        /// </summary>
        public const bool BUILDER_DEFAULT_ENABLE_COOKIES = true;
    }
}
