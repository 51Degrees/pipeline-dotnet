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

using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Engines.FiftyOne.FlowElements
{
    /// <summary>
    /// Builder for engines derived from 
    /// <see cref="PropertyKeyedEngine{TData, TProfile}"/>.
    /// </summary>
    /// <typeparam name="TBuilder">
    /// The specific type of builder (for fluent API).
    /// </typeparam>
    /// <typeparam name="TEngine">
    /// The type of engine this builder creates.
    /// </typeparam>
    public abstract class PropertyKeyedEngineBuilderBase<TBuilder, TEngine>
        : AspectEngineBuilderBase<TBuilder, TEngine>
        where TBuilder : PropertyKeyedEngineBuilderBase<TBuilder, TEngine>
        where TEngine : IAspectEngine
    {
        /// <summary>
        /// Logger factory for the builder and any elements that are 
        /// created.
        /// </summary>
        protected readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// Logger for the builder base class.
        /// </summary>
        protected readonly ILogger<PropertyKeyedEngineBuilderBase<TBuilder, TEngine>> _logger;

        /// <summary>
        /// Constructs a new instance of
        /// <see cref="PropertyKeyedEngineBuilderBase{TBuilder, TEngine}"/>.
        /// </summary>
        /// <param name="loggerFactory"></param>
        protected PropertyKeyedEngineBuilderBase(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<PropertyKeyedEngineBuilderBase<TBuilder, TEngine>>();
        }

        /// <summary>
        /// Build the engine using the configured options.
        /// Property-keyed engines do not use a data file — they resolve
        /// their data from another engine already in the pipeline at
        /// runtime, via the concrete subclass's AddPipeline override.
        /// </summary>
        /// <returns>The built engine.</returns>
        public TEngine Build()
        {
            return BuildEngine();
        }

        /// <summary>
        /// Creates a new instance of the engine for the given properties.
        /// Subclasses must implement this to construct the concrete engine
        /// and perform any required validation.
        /// </summary>
        /// <param name="properties">
        /// The properties the engine should populate in results, as
        /// configured via SetProperty/SetProperties. An empty list
        /// means all properties are included.
        /// </param>
        /// <returns>A new engine instance.</returns>
        protected abstract TEngine CreateEngine(List<string> properties);

        /// <summary>
        /// Cache is not supported by property keyed engines.
        /// This setting is ignored.
        /// </summary>
        /// <param name="cacheSize"></param>
        /// <returns></returns>
        public override TBuilder SetCacheSize(int cacheSize)
        {
            _logger.LogWarning(Messages.PropertyKeyedCacheNotSupported);
            return (TBuilder)this;
        }

        /// <summary>
        /// Cache is not supported by property keyed engines.
        /// This setting is ignored.
        /// </summary>
        /// <param name="cacheHitOrMiss"></param>
        /// <returns></returns>
        public override TBuilder SetCacheHitOrMiss(bool cacheHitOrMiss)
        {
            _logger.LogWarning(Messages.PropertyKeyedCacheNotSupported);
            return (TBuilder)this;
        }

        /// <summary>
        /// Cache is not supported by property keyed engines.
        /// This setting is ignored.
        /// </summary>
        /// <param name="cacheConfig"></param>
        /// <returns></returns>
        public override TBuilder SetCache(CacheConfiguration cacheConfig)
        {
            _logger.LogWarning(Messages.PropertyKeyedCacheNotSupported);
            return (TBuilder)this;
        }

        /// <summary>
        /// Creates a new instance of the engine. The properties list
        /// retains its base-class meaning (properties to return in
        /// results). An empty list means all properties are included.
        /// Concrete implementations of <see cref="CreateEngine"/> are
        /// responsible for their own validation if needed.
        /// </summary>
        /// <param name="properties">
        /// The properties the engine should populate in results.
        /// An empty list means all properties are included.
        /// </param>
        /// <returns>A new engine instance.</returns>
        protected override TEngine NewEngine(List<string> properties)
        {
            return CreateEngine(properties);
        }
    }
}
