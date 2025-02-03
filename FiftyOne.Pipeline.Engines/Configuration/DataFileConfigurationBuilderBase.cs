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

using FiftyOne.Pipeline.Core.Attributes;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace FiftyOne.Pipeline.Engines.Configuration
{
    /// <summary>
    /// Builder class that is used to create instances of
    /// <see cref="DataFileConfiguration"/> objects.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/data-updates.md">Specification</see>
    /// </summary>
    public abstract class DataFileConfigurationBuilderBase<TBuilder, TConfig>
        where TBuilder : DataFileConfigurationBuilderBase<TBuilder, TConfig>
        where TConfig : DataFileConfiguration, new()
    {
        private string _identifier = Constants.DATA_FILE_DEFAULT_IDENTIFIER;
        private string _dataUpdateUrlOverride = Constants.DATA_FILE_DEFAULT_UPDATE_OVERRIDE_URL;
        private bool? _autoUpdateEnabled = Constants.DATA_FILE_DEFAULT_AUTO_UPDATES_ENABLED;
        private bool? _dataFileSystemWatcherEnabled = Constants.DATA_FILE_DEFAULT_FILESYSTEMWATCHER_ENABLED;
        private int? _updatePollingIntervalSeconds = Constants.DATA_FILE_DEFAULT_UPDATE_POLLING_SECONDS;
        private int? _updateMaxRandomisationSeconds = Constants.DATA_FILE_DEFAULT_RANDOMISATION_SECONDS;
        private IDataUpdateUrlFormatter _dataUpdateUrlFormatter = null;
        private bool? _dataUpdateVerifyMd5 = Constants.DATA_FILE_DEFAULT_VERIFY_MD5;
        private bool? _dataUpdateDecompress = Constants.DATA_FILE_DEFAULT_DECOMPRESS;
        private bool? _dataUpdateVerifyModifiedSince = Constants.DATA_FILE_DEFAULT_VERIFY_MODIFIED_SINCE;
        private bool? _updateOnStartup = Constants.DATA_FILE_DEFAULT_UPDATE_ON_STARTUP;
        private bool _licenseKeyRequired = Constants.DATA_FILE_DEFAULT_LICENSE_KEY_REQUIRED;

        /// <summary>
        /// License keys to use when updating the Engine's data file.
        /// </summary>
        protected List<string> DataUpdateLicenseKeys { get; private set; } = new List<string>();

        /// <summary>
        /// Constructor
        /// </summary>
        public DataFileConfigurationBuilderBase()
        {
        }

        /// <summary>
        /// Set a flag that the data update service can use to determine if this data file 
        /// requires license key to be set in order to request data updates.
        /// </summary>
        /// <param name="licenseKeyRequired"></param>
        /// <returns>This builder</returns>
        [CodeConfigOnly]
        protected TBuilder SetLicenseKeyRequired(bool licenseKeyRequired)
        {
            _licenseKeyRequired = licenseKeyRequired;
            return this as TBuilder;
        }

        /// <summary>
        /// Set the identifier of the data file that this configuration 
        /// information applies to.
        /// If the engine only supports a single data file then this 
        /// value will be ignored.
        /// </summary>
        /// <param name="identifier">
        /// The identifier to use.
        /// </param>
        /// <returns>
        /// This builder instance.
        /// </returns>
        [DefaultValue(Constants.DATA_FILE_DEFAULT_IDENTIFIER)]
        public TBuilder SetDataFileIdentifier(string identifier)
        {
            _identifier = identifier;
            return this as TBuilder;
        }

        /// <summary>
        /// Configure the engine to use the specified URL when looking for
        /// an updated data file.
        /// </summary>
        /// <param name="url">
        /// The URL to check for a new data file.
        /// </param>
        /// <returns>
        /// This builder instance.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if url parameter is null or and empty string
        /// </exception>
        [DefaultValue(Constants.DATA_FILE_DEFAULT_UPDATE_OVERRIDE_URL)]
        public TBuilder SetDataUpdateUrl(string url)
        {
            if(string.IsNullOrEmpty(url)) throw new ArgumentNullException(nameof(url));
            _dataUpdateUrlOverride = url;
            return this as TBuilder;
        }

        /// <summary>
        /// Configure the engine to use the specified URL when looking for
        /// an updated data file.
        /// </summary>
        /// <param name="url">
        /// The URL to check for a new data file.
        /// </param>
        /// <returns>
        /// This builder instance.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if url parameter is null
        /// </exception>
        [CodeConfigOnly]
        public TBuilder SetDataUpdateUrl(Uri url)
        {
            if (url == null) throw new ArgumentNullException(nameof(url));
            _dataUpdateUrlOverride = url.AbsoluteUri;
            return this as TBuilder;
        }

        /// <summary>
        /// Specify a <see cref="IDataUpdateUrlFormatter"/> to be 
        /// used by the <see cref="DataUpdateService"/> when building the 
        /// complete URL to query for updated data.
        /// </summary>
        /// <param name="formatter">
        /// The formatter to use.
        /// </param>
        /// <returns>
        /// This builder instance.
        /// </returns>
        [CodeConfigOnly]
        public TBuilder SetDataUpdateUrlFormatter(
            IDataUpdateUrlFormatter formatter)
        {
            _dataUpdateUrlFormatter = formatter;
            return this as TBuilder;
        }

        /// <summary>
        /// Enable or disable the <see cref="IDataUpdateUrlFormatter"/>
        /// to be used when creating the complete URL to request updates
        /// from.
        /// </summary>
        /// <remarks>
        /// Setting this to false is equivalent to calling 
        /// <see cref="SetDataUpdateUrlFormatter(IDataUpdateUrlFormatter)"/>
        /// with a null parameter.
        /// It is available as a separate method in order to support 
        /// disabling the formatter from a configuration file.
        /// </remarks>
        /// <param name="useFormatter">
        /// True to use the specified formatter (default). False to
        /// prevent the specified formatter from being used.
        /// </param>
        /// <returns>
        /// This builder instance.
        /// </returns>
        [DefaultValue(true)]
        public TBuilder SetDataUpdateUseUrlFormatter(bool useFormatter)
        {
            if (useFormatter == false)
            {
                _dataUpdateUrlFormatter = null;
            }
            return this as TBuilder;
        }

        /// <summary>
        /// Set a value indicating if the <see cref="DataUpdateService"/>
        /// should expect the response from the data update URL to contain a
        /// 'content-md5' HTTP header that can be used to verify the integrity
        /// of the content.
        /// </summary>
        /// <param name="verify">
        /// True if the content should be verified with the Md5 hash.
        /// False otherwise.
        /// </param>
        /// <returns>
        /// This builder instance.
        /// </returns>
        [DefaultValue(Constants.DATA_FILE_DEFAULT_VERIFY_MD5)]
        public TBuilder SetDataUpdateVerifyMd5(bool verify)
        {
            _dataUpdateVerifyMd5 = verify;
            return this as TBuilder;
        }

        /// <summary>
        /// Set a value indicating if the <see cref="DataUpdateService"/>
        /// should expect content from the configured data update URL to be
        /// compressed or not.
        /// </summary>
        /// <param name="decompress">
        /// True if the content from the data update URL needs to be 
        /// decompressed. False otherwise.
        /// </param>
        /// <returns>
        /// This builder instance.
        /// </returns>
        [DefaultValue(Constants.DATA_FILE_DEFAULT_DECOMPRESS)]
        public TBuilder SetDataUpdateDecompress(bool decompress)
        {
            _dataUpdateDecompress = decompress;
            return this as TBuilder;
        }

        /// <summary>
        /// Enable or disable automatic updates for this engine. 
        /// </summary>
        /// <param name="enabled">
        /// If true, the engine will update it's data file with no manual 
        /// intervention.
        /// If false, the engine will never update it's data file unless 
        /// the manual update method is called on 
        /// <see cref="IDataUpdateService"/>
        /// </param>
        /// <returns>
        /// This builder instance.
        /// </returns>
        [DefaultValue(Constants.DATA_FILE_DEFAULT_AUTO_UPDATES_ENABLED)]
        public TBuilder SetAutoUpdate(bool enabled)
        {
            _autoUpdateEnabled = enabled;
            return this as TBuilder;
        }

        /// <summary>
        /// The <see cref="DataUpdateService"/> has the ability to watch a
        /// data file on disk and automatically refresh the engine as soon 
        /// as the file is updated.
        /// This setting enables/disables that feature.
        /// </summary>
        /// <remarks>
        /// The AutoUpdate feature must also be enabled in order for the file 
        /// system watcher to work.
        /// If the engine is built from a byte[] then this setting does nothing.
        /// </remarks>
        /// <param name="enabled">
        /// The cache configuration to use.
        /// </param>
        /// <returns>
        /// This builder instance.
        /// </returns>
        [DefaultValue(Constants.DATA_FILE_DEFAULT_FILESYSTEMWATCHER_ENABLED)]
        public TBuilder SetDataFileSystemWatcher(bool enabled)
        {
            _dataFileSystemWatcherEnabled = enabled;
            return this as TBuilder;
        }

        /// <summary>
        /// Set the time between checks for a new data file made by the 
        /// <see cref="DataUpdateService"/>.
        /// Default = 30 minutes.
        /// </summary>
        /// <remarks>
        /// Generally, the <see cref="DataUpdateService"/> will not check for 
        /// a new data file until the 'expected update time' that is stored
        /// in the current data file.
        /// This interval is the time to wait between checks after that time
        /// if no update is initially found.
        /// If automatic updates are disabled then this setting does nothing.
        /// </remarks>
        /// <param name="pollingIntervalSeconds">
        /// The number of seconds between checks.
        /// </param>
        /// <returns>
        /// This builder instance.
        /// </returns>
        [DefaultValue(Constants.DATA_FILE_DEFAULT_UPDATE_POLLING_SECONDS)]
        public TBuilder SetUpdatePollingInterval(int pollingIntervalSeconds)
        {
            _updatePollingIntervalSeconds = pollingIntervalSeconds;
            return this as TBuilder;
        }

        /// <summary>
        /// Set the time between checks for a new data file made by the 
        /// <see cref="DataUpdateService"/>.
        /// Default = 30 minutes.
        /// </summary>
        /// <remarks>
        /// Generally, the <see cref="DataUpdateService"/> will not check for 
        /// a new data file until the 'expected update time' that is stored
        /// in the current data file.
        /// This interval is the time to wait between checks after that time
        /// if no update is initially found.
        /// If automatic updates are disabled then this setting does nothing.
        /// </remarks>
        /// <param name="pollingInterval">
        /// The time between checks.
        /// </param>
        /// <returns>
        /// This builder instance.
        /// </returns>
        [CodeConfigOnly]
        public TBuilder SetUpdatePollingInterval(TimeSpan pollingInterval)
        {
            var seconds = pollingInterval.TotalSeconds;
            if (seconds > int.MaxValue)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture,
                        Messages.ExceptionPollingIntervalTooLarge,
                        int.MaxValue,
                        seconds),
                    nameof(pollingInterval));
            }
            _updatePollingIntervalSeconds = (int)seconds;
            return this as TBuilder;
        }

        /// <summary>
        /// A random element can be added to the <see cref="DataUpdateService"/> 
        /// polling interval.
        /// This option sets the maximum length of this random addition.
        /// Default = 10 minutes.
        /// </summary>
        /// <param name="maximumDeviationSeconds">
        /// The maximum time added to the data update polling interval.
        /// </param>
        /// <returns>
        /// This builder instance.
        /// </returns>
        [DefaultValue(Constants.DATA_FILE_DEFAULT_RANDOMISATION_SECONDS)]
        public TBuilder SetUpdateRandomisationMax(int maximumDeviationSeconds)
        {
            _updateMaxRandomisationSeconds = maximumDeviationSeconds;
            return this as TBuilder;
        }

        /// <summary>
        /// A random element can be added to the <see cref="DataUpdateService"/> 
        /// polling interval.
        /// This option sets the maximum length of this random addition.
        /// Default = 10 minutes.
        /// </summary>
        /// <param name="maximumDeviation">
        /// The maximum time added to the data update polling interval.
        /// </param>
        /// <returns>
        /// This builder instance.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown if the supplied deviation is too large.
        /// </exception>
        [CodeConfigOnly]
        public TBuilder SetUpdateRandomisationMax(TimeSpan maximumDeviation)
        {
            var seconds = maximumDeviation.TotalSeconds;
            if (seconds > int.MaxValue)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture,
                        Messages.ExceptionRandomizationTooLarge,
                        int.MaxValue, seconds), 
                    nameof(maximumDeviation));
            }
            _updateMaxRandomisationSeconds = (int)seconds;
            return this as TBuilder;
        }


        /// <summary>
        /// Set if DataUpdateService sends the If-Modified-Since header
        /// in the request for a new data file.
        /// </summary>
        /// <param name="enabled">
        /// Whether to use the If-Modified-Since header.
        /// </param>
        /// <returns>
        /// This builder instance.
        /// </returns>
        [DefaultValue(Constants.DATA_FILE_DEFAULT_VERIFY_MODIFIED_SINCE)]
        public TBuilder SetVerifyIfModifiedSince(bool enabled)
        {
            _dataUpdateVerifyModifiedSince = enabled;
            return this as TBuilder;
        }

        /// <summary>
        /// Set the license key to use when updating the Engine's data file.
        /// </summary>
        /// <param name="key">
        /// 51Degrees license key.
        /// This parameter can be set to null, but doing so will disable 
        /// automatic updates for this file.
        /// </param>
        /// <returns>This builder</returns>
        [DefaultValue("")]
        public TBuilder SetDataUpdateLicenseKey(
            string key)
        {
            if (key == null)
            {
                // Clear any configured license keys and disable
                // any features that would make use of the license key.
                DataUpdateLicenseKeys.Clear();
                _autoUpdateEnabled = false;
                _updateOnStartup = false;
            }
            else
            {
                DataUpdateLicenseKeys.Add(key);
            }
            return this as TBuilder;
        }

        /// <summary>
        /// Set the license keys to use when updating the Engine's data file.
        /// </summary>
        /// <param name="keys">51Degrees license keys</param>
        /// <returns>This builder</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied array is null
        /// </exception>
        [DefaultValue("No license keys")]
        public TBuilder SetDataUpdateLicenseKeys(
            string[] keys)
        {
            if(keys == null)
            {
                throw new ArgumentNullException(nameof(keys));
            }
            foreach (var key in keys)
            {
                DataUpdateLicenseKeys.Add(key);
            }
            return this as TBuilder;
        }

        /// <summary>
        /// Configure the data file to update on startup or not.
        /// </summary>
        /// <param name="enabled">
        /// If true then when this file is registered with the data 
        /// update service, it will immediately try to download the latest
        /// copy of the file. 
        /// This action will block execution until the download is complete 
        /// and the engine has loaded the new file.
        /// </param>
        [DefaultValue(Constants.DATA_FILE_DEFAULT_UPDATE_ON_STARTUP)]
        public TBuilder SetUpdateOnStartup (bool enabled)
        {
            _updateOnStartup = enabled;
            return this as TBuilder;
        }

        /// <summary>
        /// Called to indicate that configuration of this file is complete
        /// and the user can continue to configure the engine that the
        /// data file will be used by.
        /// </summary>
        /// <param name="filename">
        /// The path to the data file location.
        /// If a relative path is supplied then the location is determined 
        /// as relative to the current working directory as supplied by
        /// <see cref="Directory.GetCurrentDirectory"/>.
        /// Whether the path is relative or absolute, if there is no file 
        /// at the specified location then, before starting processing, 
        /// the engine will download the latest version of the file using 
        /// the configured auto-update settings.
        /// </param>
        /// <param name="createTempCopy">
        /// If true then the engine will make a temporary copy of the data
        /// file for its own use.
        /// This allows the original to be updated while the engine 
        /// continues processing.
        /// </param>
        /// <returns>
        /// The new <see cref="DataFileConfiguration"/> instance
        /// </returns>
        public TConfig Build(string filename, bool createTempCopy)
        {
            var config = new TConfig();
        
            // If the path is relative then append it to the working directory.
            if(Path.IsPathRooted(filename) == false)
            {
                filename = Path.Combine(Directory.GetCurrentDirectory(), filename);
            }

            ConfigureCommonOptions(config);
            config.DataFilePath = filename;
            config.CreateTempCopy = createTempCopy;

            return config;
        }

        /// <summary>
        /// Called to indicate that configuration of this file is complete
        /// and the user can continue to configure the engine that the
        /// data file will be used by.
        /// </summary>
        /// <param name="data">
        /// A <see cref="Stream"/> containing the data.
        /// </param>
        /// <returns>
        /// The new <see cref="DataFileConfiguration"/> instance
        /// </returns>
        public TConfig Build(Stream data)
        {
            var config = new TConfig();

            ConfigureCommonOptions(config);
            config.DataStream = data;
            config.MemoryOnly = true;

            return config;
        }

        /// <summary>
        /// Set any properties on the configuration object that are the 
        /// same regardless of the method of creation. 
        /// (i.e. file or byte array)
        /// </summary>
        /// <param name="config">
        /// The configuration object to update.
        /// </param>
        private void ConfigureCommonOptions(TConfig config)
        {
            config.Identifier = _identifier;
            config.DataUpdateLicenseKeys = DataUpdateLicenseKeys;
            if (_autoUpdateEnabled.HasValue)
            {
                config.AutomaticUpdatesEnabled = _autoUpdateEnabled.Value;
            }
            if (_dataFileSystemWatcherEnabled.HasValue)
            {
                config.FileSystemWatcherEnabled = _dataFileSystemWatcherEnabled.Value;
            }
            if (_updatePollingIntervalSeconds.HasValue)
            {
                config.PollingIntervalSeconds = _updatePollingIntervalSeconds.Value;
            }
            if (_updateMaxRandomisationSeconds.HasValue)
            {
                config.MaxRandomisationSeconds = _updateMaxRandomisationSeconds.Value;
            }
            if (_dataUpdateUrlOverride != null)
            {
                config.DataUpdateUrl = _dataUpdateUrlOverride;
            }
            if (_dataUpdateUrlFormatter != null)
            {
                config.UrlFormatter = _dataUpdateUrlFormatter;
            }
            if (_dataUpdateDecompress.HasValue)
            {
                config.DecompressContent = _dataUpdateDecompress.Value;
            }
            if (_dataUpdateVerifyMd5.HasValue)
            {
                config.VerifyMd5 = _dataUpdateVerifyMd5.Value;
            }
            if (_dataUpdateVerifyModifiedSince.HasValue)
            {
                config.VerifyModifiedSince = _dataUpdateVerifyModifiedSince.Value;
            }
            if (_updateOnStartup.HasValue)
            {
                config.UpdateOnStartup = _updateOnStartup.Value;
            }
            config.LicenseKeyRequiredForUpdates = _licenseKeyRequired;
        }
    }
}
