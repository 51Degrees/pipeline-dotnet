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

namespace FiftyOne.Pipeline.Web
{
    /// <summary>
    /// Static class containing various constants that are used by the 
    /// Pipeline web integration and/or are helpful to callers.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming",
        "CA1707:Identifiers should not contain underscores",
        Justification = "51Degrees coding style is for constant names " +
            "to be all-caps with an underscore to separate words.")]
    public static class Constants
    {
        /// <summary>
        /// Key prefix used for 51Degrees data stored in the HTTP context.
        /// </summary>
        internal const string HTTPCONTEXT_FIFTYONE =
            "fiftyonedegrees";

        /// <summary>
        /// Key used to store the FlowData object in the HTTP context.
        /// </summary>
        public const string HTTPCONTEXT_FLOWDATA =
            HTTPCONTEXT_FIFTYONE + ".flowdata";

        /// <summary>
        /// The name used in the configuration options for the Pipeline's
        /// configuration element.
        /// </summary>
        public const string PIPELINE_OPTIONS = "PipelineOptions";

        /// <summary>
        /// The copyright message to add to all JavaScript. This message 
        /// can not be altered by 3rd parties.
        /// </summary>
        internal const string ClientSidePropertyCopyright = 
            "// Copyright 51Degrees Mobile Experts Limited";

       /// <summary>
       /// Element datakey to get response set header properties.
       /// </summary>
        public const string ELEMENT_DATAKEY = "device";
        
        /// <summary>
        /// UACH response header name.
        /// </summary>
        public const string ACCEPTCH_HEADER = "Accept-CH";
        
        /// <summary>
        /// UACH SetHeaderBrowserAccept-CH property key value.
        /// </summary>
 
        public const string ACCEPTCH_BROWSER = "setheaderbrowseraccept-ch";
        
        /// <summary>
        /// UACH SetHeaderPlatformAccept-CH property key value.
        /// </summary>
        public const string ACCEPTCH_PLATFORM = "setheaderplatformaccept-ch";
        
        /// <summary>
        /// UACH SetHeaderHardwareAccept-CH property key value.
        /// </summary>
        public const string ACCEPTCH_HARDWARE = "setheaderhardwareaccept-ch";

    }
}
