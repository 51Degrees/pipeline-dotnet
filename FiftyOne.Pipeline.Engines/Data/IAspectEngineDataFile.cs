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

using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using System;

namespace FiftyOne.Pipeline.Engines.Data
{
    /// <summary>
    /// Interface for the details of a data file used by an Aspect engine.
    /// These properties are used by the <see cref="DataUpdateService"/>
    /// to perform automatic updates if enabled.
    /// </summary>
    public interface IAspectEngineDataFile : IDisposable
    {
        /// <summary>
        /// An identifier for this data file. Each data file used by an engine
        /// must have a different identifier.
        /// </summary>
        string Identifier { get; set; }

        /// <summary>
        /// The engine this data file is used by
        /// </summary>
        IOnPremiseAspectEngine Engine { get; set; }

        /// <summary>
        /// Get the type of the engine that this data file is for.
        /// This exists in addition to the Engine property because Engine
        /// can be null.
        /// This then allows the user to identify which engine this file 
        /// relates to.
        /// </summary>
        Type EngineType { get; }

        /// <summary>
        /// The complete file path to the location of the data file.
        /// This value will be null if the file has been supplied from 
        /// a byte[] in memory. 
        /// </summary>
        string DataFilePath { get; }

        /// <summary>
        /// The path to use when working with temporary files associated
        /// with this data file.
        /// </summary>
        string TempDataDirPath { get; set; }

        /// <summary>
        /// The complete file path to the location of the temporary
        /// copy of the data file that is currently being used by the 
        /// <see cref="IAspectEngine"/>.
        /// Engines often make a temporary copy of the data file in order
        /// to allow the original to be updated.
        /// This value will be null if the file is loaded entirely into memory.
        /// </summary>
        string TempDataFilePath { get; set; }

        /// <summary>
        /// True if automatic updates are enabled, false otherwise.
        /// </summary>
        bool AutomaticUpdatesEnabled { get; }

        /// <summary>
        /// The date and time by which an update to the current data file is 
        /// expected to have been published.
        /// </summary>
        DateTime UpdateAvailableTime { get; set; }

        /// <summary>
        /// The date and time that the current data file was published.
        /// </summary>
        DateTime DataPublishedDateTime { get; set; }

        /// <summary>
        /// The configuration that was provided for this data file.
        /// </summary>
        IDataFileConfiguration Configuration { get; set; }

        /// <summary>
        /// Get the data update URL complete with any query string 
        /// parameters that are needed to retrieve the data.
        /// By default, no query string parameters are added to the URL.
        /// </summary>
        [Obsolete("Use the FormattedUri property instead. " +
            "This property may be removed in future versions")]
#pragma warning disable CA1056 // Uri properties should not be strings
        string FormattedUrl { get; }
#pragma warning restore CA1056 // Uri properties should not be strings

        /// <summary>
        /// Get the data update URL complete with any query string 
        /// parameters that are needed to retrieve the data.
        /// By default, no query string parameters are added to the URL.
        /// </summary>
        Uri FormattedUri { get; }

        /// <summary>
        /// Returns true if this file has been registered with the 
        /// data update service. False if not.
        /// </summary>
        bool IsRegistered { get; }

        /// <summary>
        /// Set the data update service that this data file is 
        /// registered with.
        /// </summary>
        /// <param name="dataUpdateService">
        /// The data update service.
        /// </param>
        void SetDataUpdateService(IDataUpdateService dataUpdateService);
    }
}
