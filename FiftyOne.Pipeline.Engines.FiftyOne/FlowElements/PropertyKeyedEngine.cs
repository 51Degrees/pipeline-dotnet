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
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FiftyOne.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Engines.FiftyOne.FlowElements
{
    /// <summary>
    /// Base class for engines that use an index of property values to 
    /// profile ids to populate one or more profile instances if a valid
    /// value for the property is provided in the evidence with a 'query' 
    /// prefix.
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
        OnPremiseAspectEngineBase<TData, IFiftyOneAspectPropertyMetaData>, 
        IDisposable
        where TData : IMultiProfileData<TProfile>
        where TProfile : IAspectData
    {
        /// <summary>
        /// Functions used to convert values from the inner engine that will
        /// form indexes into strings.
        /// </summary>
        public static readonly Func<IAspectPropertyValue, IEnumerable<string>>[]
            ValueConverters = 
            {
                GetValues<string>,
                GetValues<bool>,
                GetValues<byte>,
                GetValues<short>,
                GetValues<ushort>,
                GetValues<int>,
                GetValues<uint>,
                GetValues<long>,
                GetValues<ulong>,
                GetValues<float>,
                GetValues<double>,
            };

        /// <inheritdoc/>
        public override string DataSourceTier => 
            DataSet.DataSourceTier;

        /// <inheritdoc/>
        public override string ElementDataKey => DataSet.ElementDataKey;

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
        public PropertyKeyedDataSet DataSet { get; private set; }

        /// <summary>
        /// Logger factory used to create loggers and inner engines.
        /// </summary>
        protected readonly ILoggerFactory LoggerFactory;

        /// <summary>
        /// The properties in the data file to index.
        /// </summary>
        private readonly IReadOnlyList<string> _indexedProperties;

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
            null,
            null)
        {
            LoggerFactory = loggerFactory;
            _indexedProperties = indexedProperties;
        }

        /// <summary>
        /// Ensure the data set and the associated pipeline and engine are
        /// disposed.
        /// </summary>
        protected override void ManagedResourcesCleanup()
        {
            DataSet.Dispose();
            base.ManagedResourcesCleanup();
        }

        /// <summary>
        /// Gets property and values from the flow data evidence. The 
        /// implementation is responsible for updating the flow data error 
        /// collection if an invalid value is provided.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        protected virtual IEnumerable<KeyValuePair<PropertyKeyedIndex, string>>
            GetQueryValues(IFlowData data)
        {
            foreach (var property in DataSet.PropertyIndexes)
            {
                foreach (var key in property.EvidenceKeys)
                {
                    if (data.TryGetEvidence(key, out string value))
                    {
                        yield return new KeyValuePair<PropertyKeyedIndex, string>(
                            property,
                            value);
                    }
                }
            }
        }

        /// <summary>
        /// Checks that at least one of the evidence keys are present.
        /// </summary>
        /// <param name="data"></param>
        public override void Process(IFlowData data)
        {
            if (DataSet.EvidenceKeyFilter.Whitelist.Any(i =>
                data.TryGetEvidence(i.Key, out string _)))
            {
                base.Process(data);
            }
        }

        /// <summary>
        /// If there is a valid query in the flow data evidence then see if 
        /// there are related values in the index. If so then add the 
        /// profile(s) to the aspectData.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="aspectData"></param>
        protected override void ProcessEngine(
            IFlowData data, 
            TData aspectData)
        {
            foreach (var query in GetQueryValues(data))
            {
                if (query.Key.ValueData.TryGetValue(
                    query.Value,
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
                            query.Value,
                            query.Key.MetaData.Name)),
                        this);
                }
            }
        }

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

        /// <inheritdoc/>
        public override void RefreshData(string dataFileIdentifier)
        {
            if (DataSet != null)
            {
                throw new Exception("Data can not be refreshed");
            }
            DataSet = BuildDataSet(
                DataFiles.First().DataFilePath);
        }

        /// <inheritdoc/>
        public override void RefreshData(
            string dataFileIdentifier, 
            Stream data)
        {
            if (DataSet != null)
            {
                throw new Exception("Data can not be refreshed");
            }
            DataSet = BuildDataSet(data);
        }

        /// <inheritdoc/>
        protected override void UnmanagedResourcesCleanup()
        {
            // No unmanaged resources to clean up.
        }

        /// <summary>
        /// Creates the inner engine from a data file path.
        /// The returned engine must support profile iteration and 
        /// provide property metadata.
        /// </summary>
        /// <param name="dataFilePath">Path to the data file.</param>
        /// <returns>
        /// A tuple of: the inner engine (as IFiftyOneAspectEngine),
        /// the pipeline wrapping it, the profiles enumerable,
        /// the evidence key for profile id lookups, and the properties.
        /// </returns>
        protected abstract InnerEngineContext CreateInnerEngine(
            string dataFilePath);

        /// <summary>
        /// Creates the inner engine from a data stream.
        /// </summary>
        /// <param name="data">Data stream.</param>
        /// <returns>Same as <see cref="CreateInnerEngine(string)"/>.</returns>
        protected abstract InnerEngineContext CreateInnerEngine(
            Stream data);

        /// <summary>
        /// Gets the values of the indexed properties from a profile 
        /// resolved by the inner engine.
        /// </summary>
        /// <param name="data">
        /// The aspect data for a single profile.
        /// </param>
        /// <param name="property">The property to read.</param>
        /// <returns>String representations of the property values.</returns>
        protected abstract IEnumerable<string> GetProfilePropertyValues(
            IElementData data,
            IFiftyOneAspectPropertyMetaData property);

        /// <summary>
        /// Builds the list of result properties from the key properties 
        /// and inner engine. Subclasses can override to customise which
        /// properties appear in results.
        /// </summary>
        /// <param name="keyProperties">The indexed key properties.</param>
        /// <param name="engine">The property keyed engine.</param>
        /// <returns>The list of properties.</returns>
        protected abstract IList<IFiftyOneAspectPropertyMetaData> 
            BuildResultProperties(
                IList<IFiftyOneAspectPropertyMetaData> keyProperties,
                IFlowElement engine);

        /// <summary>
        /// Creates a new <see cref="PropertyKeyedIndex"/> for a property.
        /// Subclasses can override to use custom property metadata types.
        /// </summary>
        /// <param name="source">
        /// The source property metadata from the inner engine.
        /// </param>
        /// <returns>A new property index.</returns>
        protected abstract PropertyKeyedIndex CreatePropertyIndex(
            IFiftyOneAspectPropertyMetaData source);

        /// <summary>
        /// Builds the data set which contains properties and indexes that
        /// will be consumed by the engine during processing.
        /// </summary>
        /// <param name="dataFilePath">Path to the data file.</param>
        /// <returns>A new <see cref="PropertyKeyedDataSet"/>.</returns>
        private PropertyKeyedDataSet BuildDataSet(string dataFilePath)
        {
            var context = CreateInnerEngine(dataFilePath);
            return BuildDataSetFromContext(context);
        }

        /// <summary>
        /// Builds the data set from a stream.
        /// </summary>
        /// <param name="data">The data stream.</param>
        /// <returns>A new <see cref="PropertyKeyedDataSet"/>.</returns>
        private PropertyKeyedDataSet BuildDataSet(Stream data)
        {
            var context = CreateInnerEngine(data);
            return BuildDataSetFromContext(context);
        }

        /// <summary>
        /// Builds the data set from an inner engine context.
        /// </summary>
        /// <param name="context">The inner engine context.</param>
        /// <returns>A new <see cref="PropertyKeyedDataSet"/>.</returns>
        private PropertyKeyedDataSet BuildDataSetFromContext(
            InnerEngineContext context)
        {
            var propertyIndexes = context.Properties
                .Where(i => _indexedProperties.Contains(i.Name))
                .Select(i => CreatePropertyIndex(i))
                .ToList();

            Parallel.ForEach(
                context.Profiles,
                profile =>
                {
                    using (var data = context.Pipeline.CreateFlowData())
                    {
                        data.AddEvidence(
                            context.ProfileIdEvidenceKey,
                            profile.ProfileId.ToString());
                        data.Process();
                        var profileData = data.Get(context.InnerEngineDataKey);
                        foreach (var index in propertyIndexes)
                        {
                            foreach (var value in GetProfilePropertyValues(
                                profileData, index.MetaData))
                            {
                                index.Add(value, profile.ProfileId);
                            }
                        }
                    }
                });

            if (Logger.IsEnabled(LogLevel.Information))
            {
                Logger.LogInformation(
                    "Indexed '{0}' properties",
                    propertyIndexes.Count);
                foreach (var property in propertyIndexes)
                {
                    Logger.LogInformation(
                        "Indexed '{0}' values and '{1}' profiles for '{2}'",
                        property.ValueData.Count,
                        property.ValueData.SelectMany(i =>
                            i.Value).Distinct().Count(),
                        property.MetaData.Name);
                }
            }

            return new PropertyKeyedDataSet(
                context.Pipeline,
                context.ElementDataKey,
                context.DataSourceTier,
                this,
                propertyIndexes,
                BuildResultProperties);
        }

        /// <summary>
        /// Tries the value converters and returns the values from the 
        /// first one that matches.
        /// </summary>
        /// <param name="value">The aspect property value.</param>
        /// <returns>String representations.</returns>
        protected static IEnumerable<string> ConvertValues(
            IAspectPropertyValue value)
        {
            if (value.HasValue)
            {
                foreach (var convert in ValueConverters)
                {
                    var matched = false;
                    foreach (var item in convert(value))
                    {
                        yield return item;
                        matched = true;
                    }
                    if (matched)
                    {
                        break;
                    }
                }
            }
        }

        private static IEnumerable<string> GetValues<T>(
            IAspectPropertyValue value)
        {
            if (value is IAspectPropertyValue<IReadOnlyList<T>>)
            {
                foreach(var item in 
                    ((IAspectPropertyValue<IReadOnlyList<T>>)value).Value)
                {
                    yield return item.ToString();
                }
            }
            else if (value is IAspectPropertyValue<T>)
            {
                yield return ((IAspectPropertyValue<T>)value).Value.ToString();
            }
        }
    }

    /// <summary>
    /// Context returned by 
    /// <see cref="PropertyKeyedEngine{TData, TProfile}.CreateInnerEngine(string)"/>
    /// containing all information needed to build the data set.
    /// </summary>
    public class InnerEngineContext
    {
        /// <summary>
        /// The pipeline wrapping the inner engine.
        /// </summary>
        public IPipeline Pipeline { get; set; }

        /// <summary>
        /// The element data key for the inner engine.
        /// </summary>
        public string InnerEngineDataKey { get; set; }

        /// <summary>
        /// The element data key for constructing the data set's key.
        /// </summary>
        public string ElementDataKey { get; set; }

        /// <summary>
        /// The data source tier from the inner engine.
        /// </summary>
        public string DataSourceTier { get; set; }

        /// <summary>
        /// The profiles from the inner engine to iterate over.
        /// </summary>
        public IEnumerable<IProfileMetaData> Profiles { get; set; }

        /// <summary>
        /// The evidence key to use when looking up a profile by id.
        /// </summary>
        public string ProfileIdEvidenceKey { get; set; }

        /// <summary>
        /// The properties from the inner engine.
        /// </summary>
        public IList<IFiftyOneAspectPropertyMetaData> Properties { get; set; }
    }
}
