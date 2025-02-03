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

using FiftyOne.Pipeline.Core.Configuration;

namespace FiftyOne.Pipeline.Web.Shared
{
    /// <summary>
    /// Extends the PipelineOptions class to add web specific options.
    /// </summary>
    public class PipelineWebIntegrationOptions : PipelineOptions
    {       
        /// <summary>
        /// Constructor
        /// </summary>
        public PipelineWebIntegrationOptions()
        {
            ClientSideEvidenceEnabled = true;
            UseAsyncScript = true;
            UseSetHeaderProperties = true;
        }

        /// <summary>
        /// True if client-side properties should be enabled. If enabled
        /// (and the JavaScriptBundlerElement added to the Pipeline), a
        /// client-side JavaScript file will be served at the URL
        /// */51Degrees.core.js.
        /// </summary>
        public bool ClientSideEvidenceEnabled { get; set; }

        /// <summary>
        /// Flag to enable/disable the use of the async attribute for
        /// the client side script.
        /// Defaults to true.
        /// </summary>
        public bool UseAsyncScript { get; set; }

        /// <summary>
        /// Flag to enable/disable a feature that will automatically set
        /// the values of HTTP headers in the response in order to request
        /// additional information.
        /// Defaults to true.
        /// </summary>
        public bool UseSetHeaderProperties { get; set; }
    }
}
