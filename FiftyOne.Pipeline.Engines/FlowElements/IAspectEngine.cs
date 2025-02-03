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

using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Caching;
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.Data;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Engines.FlowElements
{
    /// <summary>
    /// Interface representing 51Degrees on-premise aspect engines.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/conceptual-overview.md#aspect-engine">Specification</see> 
    /// </summary>
    public interface IAspectEngine : IFlowElement
    {
        /// <summary>
        /// Set the results cache.
        /// This is used to store the results of queries against the evidence
        /// that was provided.
        /// If the same evidence is provided again then the cached response
        /// is returned without needing to call the engine itself.
        /// </summary>
        /// <param name="cache">
        /// The cache.
        /// </param>
        void SetCache(IFlowCache cache);

        /// <summary>
        /// Set the engine to flag when a cache hit occurs by setting a field 
        /// on the cached aspect data.
        /// </summary>
        /// <param name="cacheHitOrMiss">
        /// Whether to flag cache hits or not.
        /// </param>
        void SetCacheHitOrMiss(bool cacheHitOrMiss);

        /// <summary>
        /// The tier to which the current data source belongs.
        /// For 51Degrees this will usually be one of:
        /// Lite
        /// Premium
        /// Enterprise
        /// </summary>
        string DataSourceTier { get; }

        /// <summary>
        /// Configure lazy loading of results.
        /// </summary>
        /// <param name="configuration">
        /// The configuration to use.
        /// </param>
        /// <returns>
        /// This engine builder instance.
        /// </returns>
        void SetLazyLoading(LazyLoadingConfiguration configuration);

        /// <summary>
        /// The lazy loading configuration to use.
        /// </summary>
        LazyLoadingConfiguration LazyLoadingConfiguration { get; }

        /// <summary>
        /// Aspect engine specific view of the properties so we can
        /// get at the IAspectPropertyMetaData representation without
        /// casting, etc.
        /// </summary>
        new IList<IAspectPropertyMetaData> Properties { get; }

        /// <summary>
        /// Whether `Properties` field already has a meaningful value, 
        /// or one will be lazily loaded upon next access.
        /// </summary>
        bool HasLoadedProperties { get; }
    }

    /// <summary>
    /// Interface representing 51Degrees on-premise aspect engines. 
    /// </summary>
    /// <typeparam name="T">
    /// The type of element data that the flow element will write to 
    /// <see cref="IFlowData"/>.
    /// </typeparam>
    /// <typeparam name="TMeta">
    /// The type of meta data that the flow element will supply 
    /// about the properties it populates.
    /// </typeparam>
    public interface IAspectEngine<T, TMeta> : IAspectEngine, IFlowElement<T, TMeta>
        where T : IAspectData
        where TMeta : IAspectPropertyMetaData
    {
    }
}
