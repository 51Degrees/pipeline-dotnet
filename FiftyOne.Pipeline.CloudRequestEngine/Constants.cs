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

namespace FiftyOne.Pipeline.CloudRequestEngine
{
    /// <summary>
    /// Cloud request engine constants.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", 
        "CA1707:Identifiers should not contain underscores", 
        Justification = "51Degrees coding style is for constant names " +
            "to be all-caps with an underscore to separate words.")]
    public static class Constants
    {
        /// <summary>
        /// This is the environment variable key which can be used to specify 
        /// the  URL for the cloud service.
        /// </summary>
        public const string FOD_CLOUD_API_URL = "FOD_CLOUD_API_URL";

        /// <summary>
        /// Default URL and API path for the cloud service.
        /// </summary>
        public const string CLOUD_URI_DEFAULT = "https://cloud.51degrees.com/api/v4/";

        /// <summary>
        /// Default filename for the data endpoint.
        /// </summary>
        public const string DATA_FILENAME = "json";

        /// <summary>
        /// Default filename for the properties endpoint.
        /// </summary>
        public const string PROPERTIES_FILENAME = "accessibleproperties";

        /// <summary>
        /// Default filename for the evidence keys endpoint.
        /// </summary>
        public const string EVIDENCE_KEYS_FILENAME = "evidencekeys";

        /// <summary>
        /// Default data endpoint for cloud service.
        /// </summary>
        public const string DATA_ENDPOINT_DEFAULT = CLOUD_URI_DEFAULT + DATA_FILENAME;

        /// <summary>
        /// Default properties endpoint for cloud service.
        /// </summary>
        public const string PROPERTIES_ENDPOINT_DEFAULT = CLOUD_URI_DEFAULT + PROPERTIES_FILENAME;

        /// <summary>
        /// Default evidence keys endpoint for cloud service.
        /// </summary>
        public const string EVIDENCE_KEYS_ENDPOINT_DEFAULT = CLOUD_URI_DEFAULT + EVIDENCE_KEYS_FILENAME;

        /// <summary>
        /// The name of the origin HTTP header
        /// </summary>
        public const string ORIGIN_HEADER_NAME = "Origin";

        /// <summary>
        /// Default value for license key when building CloudRequestEngine
        /// </summary>
        public const string LICENSE_KEY_DEFAULT = null;

        /// <summary>
        /// Default value for resource key when building CloudRequestEngine
        /// </summary>
        public const string RESOURCE_KEY_DEFAULT = null;

        /// <summary>
        /// Default value that the origin header will be set to when calling 
        /// the cloud service with CloudRequestEngine
        /// </summary>
        public const string CLOUD_REQUEST_ORIGIN_DEFAULT = null;

        /// <summary>
        /// Default timeout when calling cloud service with CloudRequestEngine.
        /// </summary>
        public const int CLOUD_REQUEST_TIMEOUT_DEFAULT_SECONDS = 100;

    }
}
