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
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Engines.FlowElements
{
    /// <summary>
    /// Base class for 51Degrees aspect engines. 
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/conceptual-overview.md#aspect-engine">Specification</see>
    /// </summary>
    /// <typeparam name="T">
    /// The type of data that the engine will return. Must implement 
    /// <see cref="IAspectData"/>.
    /// </typeparam>
    /// <typeparam name="TMeta">
    /// The type of meta data that the flow element will supply 
    /// about the properties it populates.
    /// </typeparam>
    public abstract class AspectEngineBase<T, TMeta> : 
        FlowElementBase<T, TMeta>, IAspectEngine<T, TMeta>
        where T : IAspectData
        where TMeta : IAspectPropertyMetaData
    {
        /// <summary>
        /// The results cache to be used to store results against
        /// relevant evidence values.
        /// </summary>
        private IFlowCache _cache;
        private bool _cacheHitOrMiss;

        /// <summary>
        /// The tier to which the current data source belongs.
        /// For 51Degrees this will usually be one of:
        /// Lite
        /// Premium
        /// Enterprise
        /// </summary>
        public abstract string DataSourceTier { get; }

        /// <summary>
        /// The lazy loading configuration to use.
        /// </summary>
        public LazyLoadingConfiguration LazyLoadingConfiguration { get; private set; }

        /// <summary>
        /// Provide an implementation for the non-generic, 
        /// aspect-specific version of the meta-data property.
        /// </summary>
        IList<IAspectPropertyMetaData> IAspectEngine.Properties
        {
            get
            {
                return Properties.Cast<IAspectPropertyMetaData>().ToList();
            }
        }

        /// <summary>
        /// Provide an implementation for the non-generic, 
        /// aspect-specific version of the meta-data property.
        /// </summary>
        public virtual bool HasLoadedProperties { get { return true; } }

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
        public AspectEngineBase(
            ILogger<AspectEngineBase<T, TMeta>> logger,
            Func<IPipeline, FlowElementBase<T, TMeta>, T> aspectDataFactory)
            : base(logger, aspectDataFactory)
        {
        }

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
        /// <exception cref="ArgumentNullException">
        /// Thrown if the parameter is null
        /// </exception>
        public virtual void SetCache(IFlowCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _cache.FlowElement = this;
        }

        /// <summary>
        /// Set the engine to flag when a cache hit occurs by setting a field 
        /// on the cached aspect data.
        /// </summary>
        /// <param name="cacheHitOrMiss">
        /// Whether to flag cache hits or not.
        /// </param>
        public virtual void SetCacheHitOrMiss(bool cacheHitOrMiss)
        {
            _cacheHitOrMiss = cacheHitOrMiss;
        }

        /// <summary>
        /// Configure lazy loading of results.
        /// </summary>
        /// <param name="configuration">
        /// The configuration to use.
        /// </param>
        /// <returns>
        /// This engine builder instance.
        /// </returns>
        public virtual void SetLazyLoading(LazyLoadingConfiguration configuration)
        {
            LazyLoadingConfiguration = configuration;
        }

        /// <summary>
        /// Extending classes must implement this method.
        /// It should perform the required processing and update the 
        /// specified aspect data instance.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> instance that provides the evidence.
        /// </param>
        /// <param name="aspectData">
        /// The <see cref="IAspectData"/> instance to populate with the 
        /// results of processing.
        /// </param>
        protected abstract void ProcessEngine(IFlowData data, T aspectData);

        /// <summary>
        /// Implementation of method from the base class 
        /// <see cref="FlowElementBase{T, TMeta}"/>.
        /// This exists to centralize the results caching logic.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> instance that provides the evidence 
        /// and holds the result.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the parameter is null
        /// </exception>
        protected sealed override void ProcessInternal(IFlowData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            ProcessWithCache(data);
        }

        /// <summary>
        /// Private method that checks if the result is already in the cache
        /// or not.
        /// If it is then the result is added to 'data', if not then 
        /// <see cref="ProcessEngine(IFlowData, T)"/> is called to do so.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> instance that provides the evidence 
        /// and holds the result.
        /// </param>
        private void ProcessWithCache(IFlowData data)
        {
            T cacheResult = default(T);

            // If there is a cache then check if the result 
            // is already in there.
            if (_cache != null)
            {
                try
                {
                    cacheResult = (T)_cache[data];
                }
                catch (InvalidCastException) { }
            }
            // If we don't have a result from the cache then 
            // run through the normal processing.
            if (cacheResult == null)
            {
                // If the flow data already contains an entry for this
                // element's key then use it. Otherwise, create a new
                // aspect data instance and add it to the flow data.
                T aspectData =
                    data.GetOrAdd(ElementDataKeyTyped, CreateElementData);
                if (aspectData.Engines.Contains(this) == false)
                {
                    (aspectData as AspectDataBase).AddEngine(this);
                }

                // Start the engine processing
                if (LazyLoadingConfiguration != null)
                {
                    // If lazy loading is configured then create a task
                    // to do the processing and assign the task to the 
                    // aspect data property.
                    var task = Task.Run(() =>
                    {
                        ProcessEngine(data, aspectData);
                    });
                    (aspectData as AspectDataBase).AddProcessTask(task);
                }
                else
                {
                    // If not lazy loading, just start processing.
                    ProcessEngine(data, aspectData);
                }
                // If there is a cache then add the result 
                // of processing to the cache.
                if (_cache != null)
                {
                    _cache.Put(data, data.GetFromElement(this));
                }
            }
            else
            {
                // If this aspect engine is configured to record cache hits,
                // set the cache hit value on the cached aspect data.
                if (_cacheHitOrMiss) 
                {
                    (cacheResult as AspectDataBase).SetCacheHit();
                }

                // We have a result from the cache so add it 
                // into the flow data.
                data.GetOrAdd(ElementDataKeyTyped, (f) =>
                {
                    return cacheResult;
                });
            }
        }

        /// <summary>
        /// Called by the base class when this instance is disposed.
        /// </summary>
        protected override void ManagedResourcesCleanup()
        {
            // If there is a cache then ensure it and it's contents are
            // disposed of correctly.
            if (_cache != null)
            {
                _cache.Dispose();
            }
        }
    }
}
