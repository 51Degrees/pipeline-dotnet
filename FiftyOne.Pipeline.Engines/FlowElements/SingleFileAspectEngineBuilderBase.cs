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
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.Services;
using System;
using System.IO;

namespace FiftyOne.Pipeline.Engines.FlowElements
{
    /// <summary>
    /// Abstract base class that exposes the common options that all
    /// on-premise engine builders using a single data file should make use of.
    /// </summary>
    /// <typeparam name="TBuilder">
    /// The specific builder type to use as the return type from the fluent 
    /// builder methods.
    /// </typeparam>
    /// <typeparam name="TEngine">
    /// The type of the engine that this builder will build
    /// </typeparam>
    public abstract class SingleFileAspectEngineBuilderBase<TBuilder, TEngine> :
        OnPremiseAspectEngineBuilderBase<TBuilder, TEngine>
        where TBuilder : OnPremiseAspectEngineBuilderBase<TBuilder, TEngine>
        where TEngine : IOnPremiseAspectEngine
    {

        private DataFileConfigurationBuilder _dataFileBuilder = new DataFileConfigurationBuilder();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="dataUpdateService">
        /// The <see cref="IDataUpdateService"/> instance to use when 
        /// checking for data updates.
        /// If null is passed then data updates functionality will be 
        /// unavailable.
        /// </param>
        public SingleFileAspectEngineBuilderBase(
            IDataUpdateService dataUpdateService) : base(dataUpdateService)
        { }

        /// <summary>
        /// Build an engine using the current options and the specified 
        /// data file.
        /// Also registers the data file with the data update service.
        /// </summary>
        /// <param name="datafile">        
        /// The path to the data file location.
        /// If a relative path is supplied then the location is determined 
        /// as relative to the current working directory as supplied by
        /// <see cref="Directory.GetCurrentDirectory"/>.
        /// Whether the path is relative or absolute, if there is no file 
        /// at the specified location then, before starting processing, 
        /// the engine will download the latest version of the file using 
        /// the configured auto-update settings.
        /// </param>
        /// <param name="createTempDataCopy"> 
        /// If true, the engine will create a copy of the data file in a
        /// temporary location rather than using the file provided directly.
        /// If not loading all data into memory, this is required for 
        /// automatic data updates to occur.
        /// </param>
        /// <returns>
        /// An <see cref="IAspectEngine"/>
        /// </returns>
        public virtual TEngine Build(
            [DefaultValue("No default, value must be supplied")] string datafile, 
            [DefaultValue("No default, value must be supplied")] bool createTempDataCopy)
        {
            var config = _dataFileBuilder.Build(datafile, createTempDataCopy);
            AddDataFile(config);
            return Build();
        }

        /// <summary>
        /// Build an engine using the current options and the specified 
        /// byte array.
        /// Also registers the data file with the data update service.
        /// </summary>
        /// <param name="data">
        /// A <see cref="Stream"/> containing the data that would normally 
        /// be in a data file. 
        /// If this argument is null then, before starting processing, 
        /// the engine will download the latest version of the datafile 
        /// using the configured auto-update settings. 
        /// </param>
        /// <returns>
        /// An <see cref="IAspectEngine"/>.
        /// </returns>
        public virtual TEngine Build([CodeConfigOnly] Stream data)
        {
            var config = _dataFileBuilder.Build(data);
            AddDataFile(config);
            return Build();
        }

        /// <summary>
        /// Build an engine using the configured options.
        /// Also registers the data file with the data update service.
        /// </summary>
        /// <returns>
        /// An <see cref="IAspectEngine"/>.
        /// </returns>
        protected virtual TEngine Build()
        {
            if (DataFileConfigs.Count != 1)
            {
                throw new PipelineConfigurationException("This builder " +
                    $"requires one and only one data file to be configured " +
                    $"but {DataFileConfigs.Count} data file " +
                    $"configurations that have been supplied.");
            }
            var engine = BuildEngine();
            return engine;
        }

        #region Helper methods mirroring those on DataFileConfigurationBuilder
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
        [DefaultValue("51Degrees default, set by subclass")]
        public TBuilder SetDataUpdateUrl(string url)
        {
            _dataFileBuilder.SetDataUpdateUrl(new Uri(url));
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
        [CodeConfigOnly]
        public TBuilder SetDataUpdateUrl(Uri url)
        {
            _dataFileBuilder.SetDataUpdateUrl(url);
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
            _dataFileBuilder.SetDataUpdateUrlFormatter(formatter);
            return this as TBuilder;
        }

        /// <summary>
        /// Enable/Disable the UrlFormatter to be used when this engine's
        /// data file is updated.
        /// Default is true.
        /// If set to false, the UrlFormatter will be ignored.
        /// </summary>
        /// <param name="formatter">
        /// The formatter to use.
        /// </param>
        /// <returns>
        /// This builder instance.
        /// </returns>
        [CodeConfigOnly]
        public TBuilder SetDataUpdateUseUrlFormatter(
            IDataUpdateUrlFormatter formatter)
        {
            _dataFileBuilder.SetDataUpdateUrlFormatter(formatter);
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
            _dataFileBuilder.SetDataUpdateUseUrlFormatter(useFormatter);
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
            _dataFileBuilder.SetDataUpdateVerifyMd5(verify);
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
            _dataFileBuilder.SetDataUpdateDecompress(decompress);
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
            _dataFileBuilder.SetAutoUpdate(enabled);
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
            _dataFileBuilder.SetDataFileSystemWatcher(enabled);
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
            _dataFileBuilder.SetUpdatePollingInterval(pollingIntervalSeconds);
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
            _dataFileBuilder.SetUpdatePollingInterval(pollingInterval);
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
            _dataFileBuilder.SetUpdateRandomisationMax(maximumDeviationSeconds);
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
        [CodeConfigOnly]
        public TBuilder SetUpdateRandomisationMax(TimeSpan maximumDeviation)
        {
            _dataFileBuilder.SetUpdateRandomisationMax(maximumDeviation);
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
            _dataFileBuilder.SetVerifyIfModifiedSince(enabled);
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
            _dataFileBuilder.SetDataUpdateLicenseKey(key);
            return this as TBuilder;
        }

        /// <summary>
        /// Set the license keys to use when updating the Engine's data file.
        /// </summary>
        /// <param name="keys">51Degrees license keys</param>
        /// <returns>This builder</returns>
        [DefaultValue("No keys")]
        public TBuilder SetDataUpdateLicenseKeys(
            string[] keys)
        {
            _dataFileBuilder.SetDataUpdateLicenseKeys(keys);
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
        /// <returns>This builder</returns>
        [DefaultValue(Constants.DATA_FILE_DEFAULT_UPDATE_ON_STARTUP)]
        public TBuilder SetDataUpdateOnStartup(
            bool enabled)
        {
            _dataFileBuilder.SetUpdateOnStartup(enabled);
            return this as TBuilder;
        }
        #endregion
    }
}
