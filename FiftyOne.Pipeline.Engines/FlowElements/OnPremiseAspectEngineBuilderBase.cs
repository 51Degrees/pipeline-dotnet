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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace FiftyOne.Pipeline.Engines.FlowElements
{
    /// <summary>
    /// Abstract base class that exposes the common options that all
    /// 51Degrees on-premise engine builders should make use of.
    /// </summary>
    /// <typeparam name="TBuilder">
    /// The specific builder type to use as the return type from the fluent 
    /// builder methods.
    /// </typeparam>
    /// <typeparam name="TEngine">
    /// The type of the engine that this builder will build
    /// </typeparam>
    public abstract class OnPremiseAspectEngineBuilderBase<TBuilder, TEngine> :
        AspectEngineBuilderBase<TBuilder, TEngine>
        where TBuilder : OnPremiseAspectEngineBuilderBase<TBuilder, TEngine>
        where TEngine : IOnPremiseAspectEngine
    {

        private IDataUpdateService _dataUpdateService;

#pragma warning disable CA2227 // Collection properties should be read only
        /// <summary>
        /// A list of the <see cref="IDataFileConfiguration"/> instances 
        /// for all data files being supplied to the engine.
        /// </summary>
        // This would be a breaking change.
        // The collections in question are protected rather than public,
        // which limits the exposure.
        protected List<IDataFileConfiguration> DataFileConfigs { get; set; } = new List<IDataFileConfiguration>();

        /// <summary>
        /// Used to store a temporary list of the data file 
        /// meta data between their creation and the creation
        /// of the engine.
        /// </summary>
        protected List<AspectEngineDataFile> DataFiles { get; set; } = new List<AspectEngineDataFile>();
#pragma warning restore CA2227 // Collection properties should be read only

        /// <summary>
        /// The directory to use for temporary files when needed
        /// </summary>
        protected string TempDir { get; set; } = Path.GetTempPath();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="dataUpdateService">
        /// The <see cref="IDataUpdateService"/> instance to use when 
        /// checking for data updates.
        /// If null is passed then data updates functionality will be 
        /// unavailable.
        /// </param>
        public OnPremiseAspectEngineBuilderBase(
            IDataUpdateService dataUpdateService)
        {
            _dataUpdateService = dataUpdateService;
        }

        /// <summary>
        /// Add a data file for this engine to use.
        /// </summary>
        /// <param name="configuration">
        /// The data file configuration to add to this engine.
        /// </param>
        /// <returns>
        /// This engine builder instance.
        /// </returns>
        public TBuilder AddDataFile(IDataFileConfiguration configuration)
        {
            DataFileConfigs.Add(configuration);
            return this as TBuilder;
        }

        /// <summary>
        /// Set the temporary path to use when the engine needs to create
        /// temporary files. (e.g. when downloading data updates)
        /// Default = Path.GetTempPath();
        /// </summary>
        /// <param name="dirPath">
        /// The full path to the temporary directory
        /// </param>
        /// <returns>
        /// This engine builder instance.
        /// </returns>
        [DefaultValue("The value returned by Path.GetTempPath()")]
        public TBuilder SetTempDirPath(string dirPath)
        {
            TempDir = dirPath;
            return this as TBuilder;
        }

        /// <summary>
        /// Set the performance profile that the engine should use.
        /// </summary>
        /// <param name="profile"></param>
        /// <returns></returns>
        public abstract TBuilder SetPerformanceProfile(PerformanceProfiles profile);

        /// <summary>
        /// Create a new instance of the <see cref="AspectEngineDataFile"/>
        /// instances used by the engine associated with this builder.
        /// If the engine uses a derived type then this method should be 
        /// overridden to return a new instance on that type.
        /// </summary>
        /// <returns>
        /// A new <see cref="AspectEngineDataFile"/> instance.
        /// </returns>
        protected virtual AspectEngineDataFile NewAspectEngineDataFile()
        {
            return new AspectEngineDataFile(typeof(TEngine));
        }

        /// <summary>
        /// Called by the 'BuildEngine' method to handle
        /// anything that needs doing before the engine is built.
        /// </summary>
        protected override void PreCreateEngine()
        {
            // Register any configured files with the data update service.
            // Any files that have the 'update on startup' flag set 
            // will be updated now.
            // Create the auto-update configuration and register it.
            foreach (var dataFileConfig in DataFileConfigs)
            {
                var dataFile = NewAspectEngineDataFile();
                dataFile.Identifier = dataFileConfig.Identifier;
                dataFile.Configuration = dataFileConfig;
                dataFile.TempDataDirPath = TempDir;

                if (dataFileConfig.AutomaticUpdatesEnabled ||
                    dataFileConfig.UpdateOnStartup ||
                    dataFileConfig.FileSystemWatcherEnabled)
                {
                    // If data update service is null then throw an exception.
                    if (_dataUpdateService == null)
                    {
                        List<string> features = new List<string>();
                        if (dataFileConfig.AutomaticUpdatesEnabled) { features.Add("auto update"); }
                        if (dataFileConfig.UpdateOnStartup) { features.Add("update on startup"); }
                        if (dataFileConfig.FileSystemWatcherEnabled) { features.Add("file system watcher"); }

                        string msg = string.Format(CultureInfo.InvariantCulture,
                            Messages.ExceptionMissingDataUpdateService,
                            string.Join(", ", features));
                        throw new PipelineException(msg);
                    }
                    // If license key is required for requesting data updates
                    // but has not been set then throw an exception.
                    if (dataFileConfig.LicenseKeyRequiredForUpdates &&
                        (dataFileConfig.AutomaticUpdatesEnabled ||
                        dataFileConfig.UpdateOnStartup) &&
                        (dataFileConfig.DataUpdateLicenseKeys == null ||
                        dataFileConfig.DataUpdateLicenseKeys.Count == 0 ||
                        dataFileConfig.DataUpdateLicenseKeys.All(k => string.IsNullOrEmpty(k))))
                    {
                        List<string> features = new List<string>();
                        if (dataFileConfig.AutomaticUpdatesEnabled) { features.Add("auto update"); }
                        if (dataFileConfig.UpdateOnStartup) { features.Add("update on startup"); }

                        string msg = string.Format(CultureInfo.InvariantCulture,
                            Messages.ExceptionMissingLicenseKey,
                            string.Join(", ", features));
                        throw new PipelineConfigurationException(msg);
                    }

                    // Register the data file with the update service.
                    _dataUpdateService.RegisterDataFile(dataFile);
                }
                DataFiles.Add(dataFile);
            }
        }

        /// <summary>
        /// Called by the 'BuildEngine' method to handle
        /// configuration of the engine after it is built.
        /// Can be overridden by derived classes to add additional
        /// configuration, but the base method should always be called.
        /// </summary>
        /// <param name="engine">
        /// The engine to configure.
        /// </param>
        protected override void ConfigureEngine(TEngine engine)
        {
            base.ConfigureEngine(engine);

            foreach (var dataFile in DataFiles)
            {
                dataFile.Engine = engine;
                if (engine != null)
                {
                    engine.AddDataFile(dataFile);
                }
                // The data stream will have been read by now so dispose 
                // of it if this has not already been done.
                if(dataFile.Configuration.DataStream != null)
                {
                    dataFile.Configuration.DataStream.Dispose();
                    dataFile.Configuration.DataStream = null;
                }
            }
        }
    }
}
