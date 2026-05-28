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
using System.Collections.Generic;
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("FiftyOne.Pipeline.Engines.Tests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100c3a631e6634ea697e19c6d2fedc285bdc7f0447a5583e1ac3c5ed3502b7633e691f899a265c42a5611122a23fd2bc882e4e412384a5d4183271782416cf016b06e6648273d44896e95ce482bb8b13054ba6a6f41d393c3f3f2e5780d620e50cb67c248882e4427bf007b7c77fdd65f832c7f4a3fef9dc18e39f792d1a37cc980")]
[assembly: InternalsVisibleTo("FiftyOne.Pipeline.Engines.FiftyOne.Tests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100c3a631e6634ea697e19c6d2fedc285bdc7f0447a5583e1ac3c5ed3502b7633e691f899a265c42a5611122a23fd2bc882e4e412384a5d4183271782416cf016b06e6648273d44896e95ce482bb8b13054ba6a6f41d393c3f3f2e5780d620e50cb67c248882e4427bf007b7c77fdd65f832c7f4a3fef9dc18e39f792d1a37cc980")]
[assembly: InternalsVisibleTo("FiftyOne.Pipeline.CloudRequestEngine, PublicKey=0024000004800000940000000602000000240000525341310004000001000100c3a631e6634ea697e19c6d2fedc285bdc7f0447a5583e1ac3c5ed3502b7633e691f899a265c42a5611122a23fd2bc882e4e412384a5d4183271782416cf016b06e6648273d44896e95ce482bb8b13054ba6a6f41d393c3f3f2e5780d620e50cb67c248882e4427bf007b7c77fdd65f832c7f4a3fef9dc18e39f792d1a37cc980")]
[assembly: InternalsVisibleTo("FiftyOne.Pipeline.CloudRequestEngine.Tests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100c3a631e6634ea697e19c6d2fedc285bdc7f0447a5583e1ac3c5ed3502b7633e691f899a265c42a5611122a23fd2bc882e4e412384a5d4183271782416cf016b06e6648273d44896e95ce482bb8b13054ba6a6f41d393c3f3f2e5780d620e50cb67c248882e4427bf007b7c77fdd65f832c7f4a3fef9dc18e39f792d1a37cc980")]

namespace FiftyOne.Pipeline.Engines
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
        /// The prefix that is added to all cookies set by 51Degrees
        /// client-side code that can be used as evidence.
        /// </summary>
        public const string FIFTYONE_COOKIE_PREFIX = "51d_";
        /// <summary>
        /// The default name of the cookie that holds the ID for the 
        /// ASP.NET session.
        /// </summary>
        public const string DEFAULT_ASP_COOKIE_NAME = "asp.net_sessionid";

        /// <summary>
        /// The default endpoint for the JavaScript to call when requesting json data
        /// </summary>
        public const string DEFAULT_JSON_ENDPOINT = "/51dpipeline/json";

        /// <summary>
        /// Default value for the flag that controls whether data files automatically look
        /// for updates or not.
        /// </summary>
        public const bool DATA_FILE_DEFAULT_AUTO_UPDATES_ENABLED = true;

        /// <summary>
        /// Default value for the flag that controls whether the file system watcher is 
        /// enabled for a data file or not.
        /// </summary>
        public const bool DATA_FILE_DEFAULT_FILESYSTEMWATCHER_ENABLED = true;

        /// <summary>
        /// Default polling interval for the data update service in seconds.
        /// </summary>
        public const int DATA_FILE_DEFAULT_UPDATE_POLLING_SECONDS = 30 * 60;
        /// <summary>
        /// Default polling interval for the data update service in seconds. This uses the older 
        /// name. Please use the new name <see cref="DATA_FILE_DEFAULT_UPDATE_POLLING_SECONDS"/> 
        /// which is more consistent with other default values.
        /// </summary>
        [Obsolete("Use DATA_FILE_DEFAULT_UPDATE_POLLING_SECONDS instead")]
        public const int DATA_UPDATE_POLLING_DEFAULT = DATA_FILE_DEFAULT_UPDATE_POLLING_SECONDS;

        /// <summary>
        /// Default maximum randomization to be applied to the calculated data file update timer 
        /// interval in seconds.
        /// </summary>
        public const int DATA_FILE_DEFAULT_RANDOMISATION_SECONDS = 10 * 60;
        /// <summary>
        /// Default randomization to be applied to the calculated data file update timer 
        /// interval in seconds. This uses the older name. Please use the new name 
        /// <see cref="DATA_FILE_DEFAULT_RANDOMISATION_SECONDS"/> which is more consistent 
        /// with other default values.
        /// </summary>
        [Obsolete("Use DATA_FILE_DEFAULT_RANDOMISATION_SECONDS instead")]
        public const int DATA_UPDATE_RANDOMISATION_DEFAULT = 10 * 60;

        /// <summary>
        /// Default value for the flag that controls whether an update that has been downloaded 
        /// for a data file will be decompressed or not.
        /// </summary>
        public const bool DATA_FILE_DEFAULT_DECOMPRESS = true;

        /// <summary>
        /// Default value for the flag that controls whether a 'Content-Md5' header is expected
        /// and should be verified when a data file update is downloaded.
        /// </summary>
        public const bool DATA_FILE_DEFAULT_VERIFY_MD5 = true;

        /// <summary>
        /// Default value for the flag that controls whether a the update service should supply
        /// and 'If-Modified-Since' header to the data update url when requesting a new data file.
        /// </summary>
        public const bool DATA_FILE_DEFAULT_VERIFY_MODIFIED_SINCE = true;

        /// <summary>
        /// Default value for the flag that controls whether a the update service should look
        /// for a new data file when the engine is created.
        /// </summary>
        public const bool DATA_FILE_DEFAULT_UPDATE_ON_STARTUP = false;

        /// <summary>
        /// Default value for the flag that controls whether a license key is required in order
        /// for the data update url to supply a data file.
        /// </summary>
        public const bool DATA_FILE_DEFAULT_LICENSE_KEY_REQUIRED = false;

        /// <summary>
        /// Default value for the id that is used to differentiate one data file from another 
        /// for the same engine.
        /// </summary>
        public const string DATA_FILE_DEFAULT_IDENTIFIER = "Default";

        /// <summary>
        /// Default value for the url that is used when checking for updates for a data file.
        /// </summary>
        public const string DATA_FILE_DEFAULT_UPDATE_OVERRIDE_URL = null;

        /// <summary>
        /// Default timeout value when accessing a property from an engine with lazy loading enabled. 
        /// </summary>
        public const int LAZY_LOADING_DEFAULT_TIMEOUT_MS = 1000;

        /// <summary>
        /// Default size for engine caches 
        /// </summary>
        public const int CACHE_DEFAULT_SIZE = 1000;

        /// <summary>
        /// Default value for the flag that controls whether the cache should be recording 
        /// hit/miss counts.
        /// </summary>
        public const bool CACHE_DEFAULT_HIT_OR_MISS_ENABLED = false;
    }
}
