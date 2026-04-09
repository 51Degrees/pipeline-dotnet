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

using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FiftyOne.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.Pipeline.Engines.FiftyOne.FlowElements
{
    /// <summary>
    /// Base class for engines that use an index of property values to 
    /// profile ids to populate one or more profile instances if a valid
    /// value for a specific configured property is provided in the evidence.
    /// The specific property to look for is determined by
    /// <see cref="GetKeyPropertyName()"/>.
    /// </summary>
    /// <typeparam name="TData">
    /// The type of multi-profile data returned by the engine. Must 
    /// implement <see cref="IMultiProfileData{TProfile}"/>.
    /// </typeparam>
    /// <typeparam name="TProfile">
    /// The type of profile data contained in the multi-profile result.
    /// Must implement <see cref="IAspectData"/>.
    /// </typeparam>
    public abstract class PropertyKeyedEngine<TData, TProfile> :
        AspectEngineBase<TData, IFiftyOneAspectPropertyMetaData>
        where TData : IMultiProfileData<TProfile>
        where TProfile : IAspectData
    {
        /// <inheritdoc/>
        public override string DataSourceTier => 
            DataSet.DataSourceTier;

        /// <inheritdoc/>
        public override abstract string ElementDataKey { get; }

        /// <inheritdoc/>
        public override IEvidenceKeyFilter EvidenceKeyFilter =>
            DataSet.EvidenceKeyFilter;

        /// <summary>
        /// Each of the properties available from each instance of 
        /// <see cref="IMultiProfileData{TProfile}.Profiles"/>.
        /// </summary>
        public override IList<IFiftyOneAspectPropertyMetaData> Properties =>
            DataSet.Properties;

        /// <summary>
        /// The values and associated profile data for each of the 
        /// properties supported by the engine.
        /// </summary>
        public PropertyKeyedDataSet DataSet { get; protected set; }

        /// <summary>
        /// Logger factory used to create loggers and inner engines.
        /// </summary>
        protected readonly ILoggerFactory LoggerFactory;

        /// <summary>
        /// The properties in the data file to index.
        /// </summary>
        protected IReadOnlyList<string> IndexedProperties { get; }

        /// <summary>
        /// Constructs a new instance of 
        /// <see cref="PropertyKeyedEngine{TData, TProfile}"/>.
        /// </summary>
        /// <param name="loggerFactory"></param>
        /// <param name="indexedProperties">
        /// The properties in the associated data file that should be 
        /// indexed.
        /// </param>
        protected PropertyKeyedEngine(
            ILoggerFactory loggerFactory,
            IReadOnlyList<string> indexedProperties) : base(
                loggerFactory.CreateLogger<PropertyKeyedEngine<TData, TProfile>>(),
                // Aspect data factory is not used by property keyed engines.
                // CreateElementData is overridden by concrete subclasses.
                aspectDataFactory: null)
        {
            if (indexedProperties == null || indexedProperties.Count == 0)
            {
                throw new ArgumentException(
                    "At least one property must be indexed.",
                    nameof(indexedProperties));
            }
            LoggerFactory = loggerFactory;
            IndexedProperties = indexedProperties;
        }

        /// <summary>
        /// Gets the property name by which the lookups will be done.
        /// This is provided by the inheriting class.
        /// </summary>
        /// <returns>The string name of the property to key on.</returns>
        protected abstract string GetKeyPropertyName();

        /// <summary>
        /// Validates the value of the key property.
        /// The inheriting class should implement validation logic here and add any
        /// relevant errors to the flow data.
        /// </summary>
        /// <param name="keyPropertyValue">The value to validate.</param>
        /// <param name="data">The flow data to add errors to.</param>
        /// <returns>True if the value is valid, false otherwise.</returns>
        protected abstract bool Validate(string keyPropertyValue, IFlowData data);

        /// <summary>
        /// Called for each profile id that matches the query. Subclasses
        /// must implement this to convert the profile id into profile data
        /// and add it to the aspect data.
        /// </summary>
        /// <param name="data">The flow data.</param>
        /// <param name="aspectData">The aspect data to add results to.</param>
        /// <param name="profileId">The matched profile id.</param>
        protected abstract void ProcessProfileMatch(
            IFlowData data,
            TData aspectData,
            uint profileId);

        /// <summary>
        /// Ensure the data set and the associated pipeline and engine are
        /// disposed.
        /// </summary>
        protected override void ManagedResourcesCleanup()
        {
            DataSet?.Dispose();
            base.ManagedResourcesCleanup();
        }

        /// <summary>
        /// Checks that at least one of the evidence keys are present.
        /// </summary>
        /// <param name="data"></param>
        public override void Process(IFlowData data)
        {
            if (DataSet.EvidenceKeyFilter.Whitelist.Any(i =>
                data.GetEvidence().AsDictionary().ContainsKey(i.Key)))
            {
                base.Process(data);
            }
        }

        /// <summary>
        /// If there is valid evidence for the specific configured property 
        /// then see if there are related values in the index. If so then add the 
        /// profile(s) to the aspectData.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="aspectData"></param>
        protected override void ProcessEngine(
            IFlowData data, 
            TData aspectData)
        {
            string keyProperty = GetKeyPropertyName();
            var propertyIndex = DataSet.PropertyIndexes.FirstOrDefault(
                i => i.MetaData.Name.Equals(keyProperty, StringComparison.OrdinalIgnoreCase));

            if (propertyIndex == null) return;

            foreach (var key in propertyIndex.EvidenceKeys)
            {
                if (data.TryGetEvidence(key, out string value))
                {
                    if (Validate(value, data))
                    {
                        if (propertyIndex.ValueData.TryGetValue(
                            value,
                            out var results))
                        {
                            foreach (var result in results)
                            {
                                ProcessProfileMatch(data, aspectData, result);
                            }
                        }
                        else
                        {
                            data.AddError(
                                new ArgumentException(String.Format(
                                    Messages.PropertyKeyedMissingValue,
                                    value,
                                    propertyIndex.MetaData.Name)),
                                this);
                        }
                    }
                }
            }
        }

        /// <inheritdoc/>
        protected override void UnmanagedResourcesCleanup()
        {
            // No unmanaged resources to clean up.
        }
    }
}
