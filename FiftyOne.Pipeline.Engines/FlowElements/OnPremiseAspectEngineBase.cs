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

using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FiftyOne.Pipeline.Engines.FlowElements
{
    /// <summary>
    /// Base class for 51Degrees on-premise aspect engines. 
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/conceptual-overview.md#on-premise-engines">Specification</see>
    /// </summary>
    /// <typeparam name="T">
    /// The type of data that the engine will return. Must implement 
    /// <see cref="IAspectData"/>.
    /// </typeparam>   
    /// <typeparam name="TMeta">
    /// The type of meta data that the flow element will supply 
    /// about the properties it populates.
    /// </typeparam>
    public abstract class OnPremiseAspectEngineBase<T, TMeta> :
        AspectEngineBase<T, TMeta>, IOnPremiseAspectEngine
        where T : IAspectData
        where TMeta : IAspectPropertyMetaData
    {
        private List<IAspectEngineDataFile> _dataFiles;
        /// <summary>
        /// Details of the data files used by this engine.
        /// </summary>
        public IReadOnlyList<IAspectEngineDataFile> DataFiles => _dataFiles;

        /// <summary>
        /// Directory to use as a temporary file location when required.
        /// </summary>
        public string TempDataDirPath { get; protected set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">
        /// The logger to use
        /// </param>
        /// <param name="aspectDataFactory">
        /// The factory function to use when the engine creates an
        /// <see cref="AspectDataBase"/> instance.
        /// </param>
        /// <param name="tempDataFilePath">
        /// The directory to use when storing temporary copies of the 
        /// data file(s) used by this engine.
        /// </param>
        public OnPremiseAspectEngineBase(
            ILogger<AspectEngineBase<T, TMeta>> logger,
            Func<IPipeline, FlowElementBase<T, TMeta>, T> aspectDataFactory,
            string tempDataFilePath)
            : base(logger, aspectDataFactory)
        {
            _dataFiles = new List<IAspectEngineDataFile>();
            TempDataDirPath = tempDataFilePath;
        }


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
        public IAspectEngineDataFile GetDataFileMetaData(string dataFileIdentifier = null)
        {
            if (DataFiles.Count == 0)
            {
                return null;
            }
            else if (DataFiles.Count == 1)
            {
                return DataFiles.Single();
            }
            else
            {
                return DataFiles.Single(f => f.Identifier == dataFileIdentifier);
            }
        }

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
        public abstract void RefreshData(string dataFileIdentifier);

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
        public abstract void RefreshData(string dataFileIdentifier, Stream data);

        /// <summary>
        /// Called when this instance is being disposed
        /// </summary>
        protected override void ManagedResourcesCleanup()
        {
            foreach (var dataFile in DataFiles)
            {
                dataFile.Dispose();
            }
            base.ManagedResourcesCleanup();
        }

        /// <summary>
        /// Add the specified data file to the engine.
        /// </summary>
        /// <param name="dataFile">
        /// The data file to add.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if supplied data file is null.
        /// </exception>
        public virtual void AddDataFile(IAspectEngineDataFile dataFile)
        {
            if(dataFile == null)
            {
                throw new ArgumentNullException(nameof(dataFile));
            }

            if (_dataFiles.Any(f => f.Identifier == dataFile.Identifier) == false)
            {
                dataFile.Engine = this;
                _dataFiles.Add(dataFile);
            }
            if (dataFile.Configuration.DataStream != null)
            {
                // The DataStream has been set so call the relevant overload
                // to be clear about our intent.
                RefreshData(dataFile.Identifier, dataFile.Configuration.DataStream);
            }
            else
            {
                RefreshData(dataFile.Identifier);
            }
        }
    }
}
