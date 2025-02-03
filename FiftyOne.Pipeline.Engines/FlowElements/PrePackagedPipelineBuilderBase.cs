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
using Microsoft.Extensions.Logging;
using System;
using System.Threading;

namespace FiftyOne.Pipeline.Engines.FlowElements
{
    /// <summary>
    /// Base class for pipeline builders that will produce a pipeline
    /// with specific flow elements.
    /// </summary>
    /// <typeparam name="TBuilder"></typeparam>
    public abstract class PrePackagedPipelineBuilderBase<TBuilder> : PipelineBuilderBase<TBuilder>
        where TBuilder : PrePackagedPipelineBuilderBase<TBuilder>
    {
        /// <summary>
        /// True if lazy loading is enabled for the relevant engine(s).
        /// False if not.
        /// </summary>
        protected bool LazyLoading { get; set; } = false;
        /// <summary>
        /// True if a results cache should be added to the relevant engine(s).
        /// False if not.
        /// </summary>
        protected bool ResultsCache { get; set; } = false;

        /// <summary>
        /// The timeout to be used by the lazy loading feature.
        /// </summary>
        protected TimeSpan LazyLoadingTimeout { get; set; } = TimeSpan.FromSeconds(5);
        /// <summary>
        /// The cancellation token to be used by the lazy loading feature.
        /// </summary>
        protected CancellationToken LazyLoadingCancellationToken { get; set; } = default(CancellationToken);
        /// <summary>
        /// The cache size to use for the results cache
        /// </summary>
        protected int ResultsCacheSize { get; set; } = 1000;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="loggerFactory"></param>
        public PrePackagedPipelineBuilderBase(
            ILoggerFactory loggerFactory) : base(loggerFactory)
        {

        }

        /// <summary>
        /// Enable lazy loading of results.
        /// Uses a default timeout of 5 seconds.
        /// </summary>
        /// <returns>
        /// This builder instance.
        /// </returns>
        public TBuilder UseLazyLoading()
        {
            LazyLoading = true;
            return this as TBuilder;
        }

        /// <summary>
        /// Enable lazy loading of results.
        /// </summary>
        /// <param name="timeoutMilliseconds">
        /// The timeout to use when attempting to access lazy-loaded values.
        /// Default is 5 seconds.
        /// </param>
        /// <returns>
        /// This builder instance.
        /// </returns>
        public TBuilder UseLazyLoading(int timeoutMilliseconds)
        {
            LazyLoading = true;
            LazyLoadingTimeout = TimeSpan.FromSeconds(timeoutMilliseconds);
            return this as TBuilder;
        }

        /// <summary>
        /// Enable lazy loading of results.
        /// </summary>
        /// <param name="timeout">
        /// The timeout to use when attempting to access lazy-loaded values.
        /// Default is 5 seconds.
        /// </param>
        /// <returns>
        /// This builder instance.
        /// </returns>
        public TBuilder UseLazyLoading(TimeSpan timeout)
        {
            LazyLoading = true;
            LazyLoadingTimeout = timeout;
            return this as TBuilder;
        }

        /// <summary>
        /// Enable lazy loading of results.
        /// Uses a default timeout of 5 seconds.
        /// </summary>
        /// <param name="cancellationToken">
        /// The cancellation token to observe while attempting to access 
        /// lazy-loaded values.
        /// </param>
        /// <returns>
        /// This builder instance.
        /// </returns>
        public TBuilder UseLazyLoading(CancellationToken cancellationToken)
        {
            LazyLoading = true;
            LazyLoadingCancellationToken = cancellationToken;
            return this as TBuilder;
        }

        /// <summary>
        /// Enable lazy loading of results.
        /// </summary>
        /// <param name="timeout">
        /// The timeout to use when attempting to access lazy-loaded values.
        /// Default is 5 seconds.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token to observe while attempting to access 
        /// lazy-loaded values.
        /// </param>
        /// <returns>
        /// This builder instance.
        /// </returns>
        public TBuilder UseLazyLoading(TimeSpan timeout, CancellationToken cancellationToken)
        {
            LazyLoading = true;
            LazyLoadingTimeout = timeout;
            LazyLoadingCancellationToken = cancellationToken;
            return this as TBuilder;
        }

        /// <summary>
        /// Enable caching of results.
        /// Uses a default cache size of 1000.
        /// </summary>
        /// <returns>
        /// This builder instance.
        /// </returns>
        public TBuilder UseResultsCache()
        {
            ResultsCache = true;
            return this as TBuilder;
        }

        /// <summary>
        /// Enable caching of results.
        /// </summary>
        /// <param name="size">
        /// The maximum number of results to hold in the device 
        /// detection cache.
        /// Default is 1000.
        /// </param>
        /// <returns>
        /// This builder instance.
        /// </returns>
        public TBuilder UseResultsCache(int size)
        {
            ResultsCache = true;
            ResultsCacheSize = size;
            return this as TBuilder;
        }
    }
}

