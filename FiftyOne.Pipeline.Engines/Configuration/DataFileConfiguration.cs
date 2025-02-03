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

using FiftyOne.Pipeline.Engines.Data;
using System.Collections.Generic;
using System.IO;

namespace FiftyOne.Pipeline.Engines.Configuration
{
    /// <summary>
    /// This class contains the automatic update configuration parameters 
    /// that can be supplied to an engine for a particular data file that 
    /// the engine uses.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/data-updates.md">Specification</see>
    /// </summary>
    public class DataFileConfiguration : IDataFileConfiguration
    {
        /// <summary>
        /// Create <see cref="DataFileConfiguration"/> instance
        /// with all default values.
        /// </summary>
        public DataFileConfiguration()
        {
        }

        /// <summary>
        /// The identifier of the data file that this configuration 
        /// information applies to.
        /// If the engine only supports a single data file then this 
        /// value will be ignored.
        /// </summary>
        public string Identifier { get; set; } = Constants.DATA_FILE_DEFAULT_IDENTIFIER;

        /// <summary>
        /// The complete file path to the location of the data file.
        /// This value will be null if the instance has the MemoryOnly 
        /// flag set. 
        /// </summary>
        public string DataFilePath { get; set; }

        /// <summary>
        /// Set to true if the engine should create a temporary copy of 
        /// the data file rather than using the one at the location
        /// provided directly.
        /// This setting must be set to true if automatic updates are required.
        /// </summary>
        public bool CreateTempCopy { get; set; }

        /// <summary>
        /// True if data for this file should only exist in memory. 
        /// I.e. Assume there is no file system.
        /// </summary>
        public bool MemoryOnly { get; set; }

        /// <summary>
        /// The <see cref="Stream"/> containing the data.
        /// Note that this will be set to null after being read by the engine
        /// in order to reduce memory usage.
        /// </summary>
        public Stream DataStream { get; set; }

        /// <summary>
        /// The URL to check when looking for updates to the data file.
        /// </summary>
#pragma warning disable CA1056 // Uri properties should not be strings
        public string DataUpdateUrl { get; set; }
#pragma warning restore CA1056 // Uri properties should not be strings

        /// <summary>
        /// Flag that indicates if updates to the data file will be checked 
        /// for and applied to the engine automatically or not.
        /// </summary>
        public bool AutomaticUpdatesEnabled { get; set; } = 
            Constants.DATA_FILE_DEFAULT_AUTO_UPDATES_ENABLED;

        /// <summary>
        /// A list of license keys to use when checking for updates
        /// using the <see cref="DataUpdateUrl"/>.
        /// Note that the exact formatting of the query string is 
        /// controlled by the configured <see cref="UrlFormatter"/>.
        /// </summary>
        public IReadOnlyList<string> DataUpdateLicenseKeys { get; set; } = new List<string>();

        /// <summary>
        /// If true, a <see cref="FileSystemWatcher"/> will be created
        /// to watch the file at <see cref="DataFilePath"/>.
        /// If the file is modified then the engine will automatically 
        /// be notified so that it can refresh it's internal data 
        /// structures.
        /// </summary>
        public bool FileSystemWatcherEnabled { get; set; } = 
            Constants.DATA_FILE_DEFAULT_FILESYSTEMWATCHER_ENABLED;

        /// <summary>
        /// The interval between checks for updates for this data file
        /// using the specified <see cref="DataUpdateUrl"/>.
        /// </summary>
        public int PollingIntervalSeconds { get; set; } = 
            Constants.DATA_FILE_DEFAULT_UPDATE_POLLING_SECONDS;

        /// <summary>
        /// The maximum time in seconds that the polling interval may
        /// be randomized by.
        /// I.e. each polling interval will be the configured
        /// interval + or - a random amount between zero and this value.
        /// </summary>
        /// <remarks>
        /// This settings is intended to be used to allow multiple 
        /// instances of a system stagger their update requests to
        /// reduce chance of conflict errors or slow update downloads.
        /// </remarks>
        public int MaxRandomisationSeconds { get; set; } = 
            Constants.DATA_FILE_DEFAULT_RANDOMISATION_SECONDS;

        /// <summary>
        /// The formatter to use when getting the data update URL with
        /// query string parameters set.
        /// </summary>
        public IDataUpdateUrlFormatter UrlFormatter { get; set; } = null;

        /// <summary>
        /// Must return true if the data downloaded from the DataUpdateUrl
        /// is compressed and false otherwise.
        /// </summary>
        public bool DecompressContent { get; set; } = Constants.DATA_FILE_DEFAULT_DECOMPRESS;

        /// <summary>
        /// Must return true if the response from the DataUpdateUrl
        /// is expected to include a 'Content-Md5' HTTP header that
        /// contains an Md5 hash that can be used to check the 
        /// integrity of the downloaded content.
        /// </summary>
        public bool VerifyMd5 { get; set; } = Constants.DATA_FILE_DEFAULT_VERIFY_MD5;

        /// <summary>
        /// Must return true if the request to the DataUpdateUrl supports
        /// the 'If-Modified-Since' header and false if it does not.
        /// </summary>
        public bool VerifyModifiedSince { get; set; } = 
            Constants.DATA_FILE_DEFAULT_VERIFY_MODIFIED_SINCE;

        /// <summary>
        /// If true then when this file is registered with the data 
        /// update service, it will immediately try to download the latest
        /// copy of the file. 
        /// This action will block execution until the download is complete 
        /// and the engine has loaded the new file.
        /// </summary>
        public bool UpdateOnStartup { get; set; } = Constants.DATA_FILE_DEFAULT_UPDATE_ON_STARTUP;

        /// <summary>
        /// If true then, if updates are enabled, the software will 
        /// check that a license key is present when the associated 
        /// engine is built.
        /// If a license key has not been supplied, an exception will
        /// be thrown.
        /// </summary>
        public bool LicenseKeyRequiredForUpdates { get; set; } = 
            Constants.DATA_FILE_DEFAULT_LICENSE_KEY_REQUIRED;
    }
}
