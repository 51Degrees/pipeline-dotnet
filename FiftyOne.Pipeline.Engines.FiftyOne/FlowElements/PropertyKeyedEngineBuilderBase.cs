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

using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging;
using System;
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
        : SingleFileAspectEngineBuilderBase<TBuilder, TEngine>
        where TBuilder : PropertyKeyedEngineBuilderBase<TBuilder, TEngine>
        where TEngine : IOnPremiseAspectEngine
    {
        /// <summary>
        /// Logger factory for the builder and any elements that are 
        /// created.
        /// </summary>
        protected readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// Constructs a new instance of 
        /// <see cref="PropertyKeyedEngineBuilderBase{TBuilder, TEngine}"/>.
        /// </summary>
        /// <param name="loggerFactory"></param>
        /// <param name="dataUpdateService"></param>
        protected PropertyKeyedEngineBuilderBase(
            ILoggerFactory loggerFactory,
            IDataUpdateService dataUpdateService)
            : base(dataUpdateService)
        {
            _loggerFactory = loggerFactory;
        }

        /// <summary>
        /// Cache is not supported by property keyed engines.
        /// </summary>
        /// <param name="cacheSize"></param>
        /// <returns></returns>
        public override TBuilder SetCacheSize(int cacheSize)
        {
            throw new NotSupportedException(
                Messages.PropertyKeyedCacheNotSupported);
        }

        /// <summary>
        /// Cache is not supported by property keyed engines.
        /// </summary>
        /// <param name="cacheHitOrMiss"></param>
        /// <returns></returns>
        public override TBuilder SetCacheHitOrMiss(bool cacheHitOrMiss)
        {
            throw new NotSupportedException(
                Messages.PropertyKeyedCacheNotSupported);
        }

        /// <summary>
        /// Cache is not supported by property keyed engines.
        /// </summary>
        /// <param name="cacheConfig"></param>
        /// <returns></returns>
        public override TBuilder SetCache(CacheConfiguration cacheConfig)
        {
            throw new NotSupportedException(
                Messages.PropertyKeyedCacheNotSupported);
        }

        /// <summary>
        /// Creates a new instance of the engine. Properties must have 
        /// been set using SetProperty before calling Build.
        /// </summary>
        /// <param name="properties"></param>
        /// <returns></returns>
        /// <exception cref="PipelineConfigurationException"></exception>
        protected override TEngine NewEngine(List<string> properties)
        {
            if (properties == null || properties.Count < 1)
            {
                throw new PipelineConfigurationException(
                    $"The '{GetType().Name}' needs to know " +
                    "which properties to key on. Call the 'SetProperties' " +
                    "method on the builder to configure this.");
            }
            return CreateEngine(properties);
        }

        /// <summary>
        /// Creates a new instance of the engine for the given properties.
        /// Subclasses must implement this to construct the concrete engine.
        /// </summary>
        /// <param name="properties">
        /// The properties to index on.
        /// </param>
        /// <returns>A new engine instance.</returns>
        protected abstract TEngine CreateEngine(List<string> properties);
    }
}
