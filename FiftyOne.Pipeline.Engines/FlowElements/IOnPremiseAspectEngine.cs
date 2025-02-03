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

namespace FiftyOne.Pipeline.Engines.FlowElements
{
    /// <summary>
    /// Interface for an AspectEngine that processes evidence using one 
    /// or more local data files.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/conceptual-overview.md#on-premise-engines">Specification</see>
    /// </summary>
    public interface IOnPremiseAspectEngine : IAspectEngine
    {
        /// <summary>
        /// Details of the data files used by this engine.
        /// </summary>
        IReadOnlyList<IAspectEngineDataFile> DataFiles { get; }

        /// <summary>
        /// Causes the engine to reload data from the file at 
        /// <see cref="IAspectEngineDataFile.DataFilePath"/> for the
        /// data file matching the given identifier.
        /// </summary>
        /// <remarks>
        /// Implementors should consider thread-safety to ensure that
        /// parallel calls to 'Process' will resolve as normal.
        /// </remarks>
        /// <param name="dataFileIdentifier">
        /// The identifier of the data file to update. Must match the 
        /// value in <see cref="IAspectEngineDataFile.Identifier"/>.
        /// If the engine only has a single data file, this parameter 
        /// is ignored.
        /// If null is passed then all data files should be refreshed.
        /// </param>
        void RefreshData(string dataFileIdentifier);

        /// <summary>
        /// Causes the engine to reload data from the supplied
        /// <see cref="Stream"/> for the file matching the given identifier.
        /// </summary>
        /// <remarks>
        /// Implementors should consider thread-safety to ensure that
        /// parallel calls to 'Process' will resolve as normal.
        /// </remarks>
        /// <param name="dataFileIdentifier">
        /// The identifier of the data file to update. Must match the 
        /// value in <see cref="IAspectEngineDataFile.Identifier"/>.
        /// If the engine only has a single data file, this parameter 
        /// is ignored.
        /// </param>
        /// <param name="data">
        /// A <see cref="Stream"/> containing the data to use when refreshing.
        /// </param>
        void RefreshData(string dataFileIdentifier, Stream data);

        /// <summary>
        /// The complete file path to the directory that is used by the
        /// engine to store temporary copies of any data files that it uses.
        /// </summary>
        string TempDataDirPath { get; }

        /// <summary>
        /// Get the details of a specific data file used by this engine.
        /// </summary>
        /// <param name="dataFileIdentifier">
        /// The identifier of the data file to get meta data for.
        /// This parameter is ignored if the engine only has one data file.
        /// </param>
        /// <returns>
        /// The meta data associated with the specified data file.
        /// Returns null if the engine has no associated data files.
        /// </returns>
        IAspectEngineDataFile GetDataFileMetaData(string dataFileIdentifier = null);

        /// <summary>
        /// Add the specified data file to the engine.
        /// </summary>
        /// <param name="dataFile">
        /// The data file to add.
        /// </param>
        void AddDataFile(IAspectEngineDataFile dataFile);
    }
}
