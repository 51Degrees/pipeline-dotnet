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
using FiftyOne.Pipeline.Engines.Exceptions;
using FiftyOne.Pipeline.Engines.FlowElements;
using System;
using System.IO;

namespace FiftyOne.Pipeline.Engines.Services
{
    /// <summary>
    /// Service that manages updates to data files that are used by
    /// <see cref="IOnPremiseAspectEngine"/> instances.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/data-updates.md">Specification</see>
    /// </summary>
    public interface IDataUpdateService
    {
        /// <summary>
        /// Register an data file for automatic updates.
        /// </summary>
        /// <param name="dataFile">
        /// The details of the data file to register.
        /// </param>
        void RegisterDataFile(AspectEngineDataFile dataFile);
        /// <summary>
        /// Unregister a data file.
        /// </summary>
        /// <param name="dataFile">
        /// The data file to unregister
        /// </param>
        void UnRegisterDataFile(AspectEngineDataFile dataFile);

        /// <summary>
        /// Check if there are updates for the specified engine.
        /// </summary>
        /// <param name="engine">
        /// The engine to check for
        /// </param>
        /// <param name="dataFileIdentifier">
        /// The identifier of the data file to check for.
        /// If the engine has only one data file then this parameter is ignored.
        /// </param>
        /// <returns>
        /// The result of the update check.
        /// </returns>
        /// <exception cref="DataUpdateException">
        /// Thrown if some problem occurs during the update process.
        /// </exception>
        AutoUpdateStatus CheckForUpdate(
            IOnPremiseAspectEngine engine,
            string dataFileIdentifier = null);

        /// <summary>
        /// Update the specified data file from a <see cref="MemoryStream"/>
        /// </summary>
        /// <param name="dataFile">
        /// The data file to update.
        /// </param>
        /// <param name="data">
        /// The data to update with.
        /// </param>
        /// <exception cref="DataUpdateException">
        /// Thrown if some problem occurs during the update process.
        /// </exception>
        AutoUpdateStatus UpdateFromMemory(AspectEngineDataFile dataFile, 
            MemoryStream data);

        /// <summary>
        /// The event handler fired when a call to CheckForUpdate is completed.
        /// </summary>
        event EventHandler<DataUpdateCompleteArgs> CheckForUpdateComplete;

        /// <summary>
        /// The event handler fired when a call to CheckForUpdate is started.
        /// </summary>
        event EventHandler<DataUpdateEventArgs> CheckForUpdateStarted;
    }

    /// <summary>
    /// Event arguments used when events are fired by an 
    /// <see cref="IDataUpdateService"/>
    /// </summary>
    public class DataUpdateEventArgs : EventArgs
    {
        /// <summary>
        /// The data file that the event relates to
        /// </summary>
        public AspectEngineDataFile DataFile { get; set; }
    }
    /// <summary>
    /// Event arguments used when an 'UpdteComplete' event is fired by an 
    /// <see cref="IDataUpdateService"/>
    /// </summary>
#pragma warning disable CA1710 // Identifiers should have correct suffix
    // This would be a breaking change.
    public class DataUpdateCompleteArgs : DataUpdateEventArgs
#pragma warning restore CA1710 // Identifiers should have correct suffix
    {
        /// <summary>
        /// The final status of the data update
        /// </summary>
        public AutoUpdateStatus Status { get; set; }
    }


    /// <summary>
    /// Possible status values for the data update process.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", 
        "CA1707:Identifiers should not contain underscores", 
        Justification = "51Degrees code style for public constants and " +
        "enum names is to use all caps with underscores as a separator.")]
    public enum AutoUpdateStatus
    {
        /// <summary>
        /// Status has not been set.
        /// </summary>
        UNSPECIFIED,
        /// <summary>
        /// Update completed successfully. 
        /// </summary>
        AUTO_UPDATE_SUCCESS,
        /// <summary>
        /// HTTPS connection could not be established. 
        /// </summary>
        AUTO_UPDATE_HTTPS_ERR,
        /// <summary>
        /// No need to perform update. 
        /// </summary>
        AUTO_UPDATE_NOT_NEEDED,
        /// <summary>
        /// Update currently under way. 
        /// </summary>
        AUTO_UPDATE_IN_PROGRESS,
        /// <summary>
        /// Path to master file is directory not file
        /// </summary>
        AUTO_UPDATE_MASTER_FILE_CANT_RENAME,
        /// <summary>
        /// 51Degrees server responded with 429: too many attempts. 
        /// /// </summary>
        AUTO_UPDATE_ERR_429_TOO_MANY_ATTEMPTS,
        /// <summary>
        /// 51Degrees server responded with 403, meaning key is revoked. 
        /// </summary>
        AUTO_UPDATE_ERR_403_FORBIDDEN,
        /// <summary>
        /// Used when IO operations with input or output stream failed. 
        /// </summary>
        AUTO_UPDATE_ERR_READING_STREAM,
        /// <summary>
        /// MD5 validation failed 
        /// </summary>
        AUTO_UPDATE_ERR_MD5_VALIDATION_FAILED,
        /// <summary>
        /// The new data file can't be renamed to replace the previous one.
        /// </summary>
        AUTO_UPDATE_NEW_FILE_CANT_RENAME,
        /// <summary>
        /// Refreshing the engine with the new data caused an error to occur.
        /// </summary>
        AUTO_UPDATE_REFRESH_FAILED,
        /// <summary>
        /// There was no data file configuration matching the specified
        /// identifier.
        /// </summary>
        AUTO_UPDATE_NO_CONFIGURATION,
        /// <summary>
        /// A temporary file path was required but has not been set.
        /// </summary>
        AUTO_UPDATE_TEMP_PATH_NOT_SET,
        /// <summary>
        /// There was an error while checking for an update that was not
        /// anticipated by the developers of this service.
        /// </summary>
        AUTO_UPDATE_UNKNOWN_ERROR,
        /// <summary>
        /// An operation could not be completed within the expected time.
        /// </summary>
        AUTO_UPDATE_TIMEOUT
    }
}
