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
using System;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.Pipeline.Engines.FiftyOne.FlowElements
{
    /// <summary>
    /// Contains the property indexes, pipeline, and inner engine used by
    /// <see cref="PropertyKeyedEngine{TData, TProfile}"/> to look up 
    /// profile ids from property values.
    /// </summary>
    public class PropertyKeyedDataSet : IDisposable
    {
        /// <summary>
        /// An engine that returns many profiles must have the prefix 
        /// `profiles` before the property name. This name is added to the
        /// primary property that contains the sub properties that related to
        /// the profile data type.
        /// </summary>
        public const string PROPERTY_PREFIX_NAME = "Profiles";

        /// <summary>
        /// The element data key for this data set, constructed from the 
        /// inner engine's element data key and the indexed property names.
        /// </summary>
        public string ElementDataKey { get; private set; }

        /// <summary>
        /// The data source tier from the inner engine.
        /// </summary>
        public string DataSourceTier { get; private set; }

        /// <summary>
        /// Pipeline used exclusively by this data set to look up profiles.
        /// </summary>
        public IPipeline Pipeline { get; private set; }

        /// <summary>
        /// The property key prefixed with query.
        /// </summary>
        public EvidenceKeyFilterWhitelist EvidenceKeyFilter 
            { get; private set; }

        /// <summary>
        /// List of property keys and the value and profile ids they're 
        /// associated with.
        /// </summary>
        public IReadOnlyList<PropertyKeyedIndex> PropertyIndexes
            { get; private set; }

        /// <summary>
        /// Properties that can be used as keys for lookups.
        /// </summary>
        public IList<IFiftyOneAspectPropertyMetaData> KeyProperties
            { get; private set; }

        /// <summary>
        /// Properties that can be returned in results.
        /// </summary>
        public IList<IFiftyOneAspectPropertyMetaData> Properties
            { get; private set; }

        /// <summary>
        /// True when the managed instances have been disposed of.
        /// </summary>
        private bool _disposedValue;

        /// <summary>
        /// Constructs a new instance of <see cref="PropertyKeyedDataSet"/>.
        /// </summary>
        /// <param name="pipeline">
        /// The pipeline used to look up profile ids.
        /// </param>
        /// <param name="elementDataKey">
        /// The element data key from the inner engine.
        /// </param>
        /// <param name="dataSourceTier">
        /// The data source tier from the inner engine.
        /// </param>
        /// <param name="propertyKeyedEngine">
        /// The engine that the data set will be used with. Needed to ensure
        /// the properties in the data set relate to the engine in the 
        /// pipeline and not the internal engine.
        /// </param>
        /// <param name="propertyIndexes">
        /// List of property keys and the values and profile ids associated 
        /// with each.
        /// </param>
        /// <param name="buildProperties">
        /// Function to build the list of result properties from the key 
        /// properties. Implementations can customise which properties are
        /// available in the results.
        /// </param>
        public PropertyKeyedDataSet(
            IPipeline pipeline,
            string elementDataKey,
            string dataSourceTier,
            IFlowElement propertyKeyedEngine,
            List<PropertyKeyedIndex> propertyIndexes,
            Func<IList<IFiftyOneAspectPropertyMetaData>, 
                IFlowElement,
                IList<IFiftyOneAspectPropertyMetaData>> buildProperties)
        {
            Pipeline = pipeline;
            DataSourceTier = dataSourceTier;
            KeyProperties = propertyIndexes.Select(i => 
                i.MetaData).ToList();
            
            ElementDataKey = elementDataKey + 
                String.Concat(KeyProperties);

            EvidenceKeyFilter = new EvidenceKeyFilterWhitelist(
                propertyIndexes.SelectMany(i => 
                i.EvidenceKeys).ToList());
            PropertyIndexes = propertyIndexes;
            Properties = buildProperties(KeyProperties, propertyKeyedEngine);
        }

        /// <summary>
        /// Dispose of managed resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposedValue)
            {
                EvidenceKeyFilter = null;
                PropertyIndexes = null;
                KeyProperties = null;
                Properties = null;
                Pipeline?.Dispose();
                _disposedValue = true;
            }
        }
    }
}
