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
using FiftyOne.Pipeline.Engines.Caching;
using FiftyOne.Pipeline.Engines.Configuration;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Engines.FlowElements
{

    /// <summary>
    /// Abstract base class that exposes the common options that all
    /// 51Degrees engine builders should make use of.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/conceptual-overview.md#aspect-engine">Specification</see>
    /// </summary>
    /// <typeparam name="TBuilder">
    /// The specific builder type to use as the return type from the fluent 
    /// builder methods.
    /// </typeparam>
    /// <typeparam name="TEngine">
    /// The type of the engine that this builder will build
    /// </typeparam>
    public abstract class AspectEngineBuilderBase<TBuilder, TEngine>
        where TBuilder : AspectEngineBuilderBase<TBuilder, TEngine>
        where TEngine : IAspectEngine
    {
        /// <summary>
        /// A list of the string keys of properties that the user wants
        /// the engine to determine values for.
        /// Where this is an empty list, all properties should be
        /// included.
        /// </summary>
        protected List<string> Properties { get; } = new List<string>();
        private CacheConfiguration _cacheConfig;
        private LazyLoadingConfiguration _lazyLoadingConfig;
        private bool _cacheHitOrMiss = Constants.CACHE_DEFAULT_HIT_OR_MISS_ENABLED;

        /// <summary>
        /// Configure lazy loading of results.
        /// </summary>
        /// <param name="lazyLoadingConfig">
        /// The configuration to use.
        /// </param>
        /// <returns>
        /// This engine builder instance.
        /// </returns>
        [CodeConfigOnly]
        public TBuilder SetLazyLoading(LazyLoadingConfiguration lazyLoadingConfig)
        {
            _lazyLoadingConfig = lazyLoadingConfig;
            return this as TBuilder;
        }

        /// <summary>
        /// Configure lazy loading of results.
        /// </summary>
        /// <param name="timeoutMs">
        /// The timeout length in milliseconds.
        /// </param>
        /// <returns>
        /// This engine builder instance.
        /// </returns>
        [DefaultValue(Constants.LAZY_LOADING_DEFAULT_TIMEOUT_MS)]
        public TBuilder SetLazyLoadingTimeout(int timeoutMs)
        {
            if (_lazyLoadingConfig == null) _lazyLoadingConfig = new LazyLoadingConfiguration();
            _lazyLoadingConfig.PropertyTimeoutMs = timeoutMs;
            return this as TBuilder;
        }

        /// <summary>
        /// Configure the results cache that will be used by the Pipeline to
        /// cache results from this engine.
        /// </summary>
        /// <param name="cacheConfig">
        /// The cache configuration to use.
        /// </param>
        /// <returns>
        /// This engine builder instance.
        /// </returns>
        [CodeConfigOnly]
        public virtual TBuilder SetCache(CacheConfiguration cacheConfig)
        {
            _cacheConfig = cacheConfig;
            return this as TBuilder;
        }

        /// <summary>
        /// Configure the results cache that will be used by the Pipeline to
        /// cache results from this engine.
        /// </summary>
        /// <param name="cacheSize">
        /// The cache size to use.
        /// </param>
        /// <returns>
        /// This engine builder instance.
        /// </returns>
        [DefaultValue(Constants.CACHE_DEFAULT_SIZE)]
        public virtual TBuilder SetCacheSize(int cacheSize)
        {
            if (_cacheConfig == null) _cacheConfig = new CacheConfiguration();
            _cacheConfig.Size = cacheSize;
            return this as TBuilder;
        }

        /// <summary>
        /// Configure the engine to flag whether an element data originates from 
        /// the engine cache. 
        /// </summary>
        /// <param name="cacheHitOrMiss">
        /// Whether to flag cache hits or not.
        /// </param>
        /// <returns>
        /// This engine builder instance.
        /// </returns>
        [DefaultValue(Constants.CACHE_DEFAULT_HIT_OR_MISS_ENABLED)]
        public virtual TBuilder SetCacheHitOrMiss(bool cacheHitOrMiss)
        {
            _cacheHitOrMiss = cacheHitOrMiss;
            return this as TBuilder;
        }

        /// <summary>
        /// Configure the properties that the engine will populate in the 
        /// response.
        /// By default all properties will be populated.
        /// </summary>
        /// <param name="properties">
        /// The properties that we want the engine to populate.
        /// </param>
        /// <returns>
        /// This engine builder instance.
        /// </returns>
        [DefaultValue("All properties")]
        public TBuilder SetProperties(List<string> properties)
        {
            Properties.AddRange(properties);
            return this as TBuilder;
        }

        /// <summary>
        /// Add a property to the list of properties that the engine will
        /// populate in the response.
        /// By default all properties will be populated.
        /// </summary>
        /// <param name="property">
        /// The property that we want the engine to populate.
        /// </param>
        /// <returns>
        /// This engine builder instance.
        /// </returns>
        [DefaultValue("All properties")]
        public TBuilder SetProperty(string property)
        {
            Properties.Add(property);
            return this as TBuilder;
        }

        /// <summary>
        /// Called by the <see cref="BuildEngine()"/> method to handle
        /// configuration of the engine after it is built.
        /// Can be overridden by derived classes to add additional
        /// configuration, but the base method should always be called.
        /// </summary>
        /// <param name="engine">
        /// The engine to configure.
        /// </param>
        protected virtual void ConfigureEngine(TEngine engine)
        {
            if (_cacheConfig != null)
            {
                // Create and register the results cache.
#pragma warning disable CA2000 // Dispose objects before losing scope
                // The engine manages the cache lifetime.
                engine.SetCache(new DefaultFlowCache(_cacheConfig));
#pragma warning restore CA2000 // Dispose objects before losing scope
            }
            if(_lazyLoadingConfig != null)
            {
                engine.SetLazyLoading(_lazyLoadingConfig);
            }
            engine.SetCacheHitOrMiss(_cacheHitOrMiss);
        }

        /// <summary>
        /// Called by the <see cref="BuildEngine()"/> method to handle
        /// anything that needs doing before the engine is built.
        /// By default, nothing needs to be done.
        /// </summary>
        protected virtual void PreCreateEngine() { }

        /// <summary>
        /// Called by the <see cref="BuildEngine()"/> method to handle
        /// creation of the engine instance.
        /// </summary>
        /// <param name="properties"></param>
        /// <returns>
        /// An <see cref="IAspectEngine"/>.
        /// </returns>
        protected abstract TEngine NewEngine(List<string> properties);
                
        /// <summary>
        /// Build an engine using the configured options.
        /// Derived classes should call this method when building an
        /// engine to ensure it is configured correctly all down the
        /// class hierarchy.
        /// </summary>
        /// <returns>
        /// An <see cref="IAspectEngine"/>.
        /// </returns>
        protected TEngine BuildEngine()
        {
            PreCreateEngine();
            var engine = NewEngine(Properties);
            ConfigureEngine(engine);
            return engine;
        }

    }
}
