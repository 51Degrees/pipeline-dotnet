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

using FiftyOne.Caching;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Engines.Configuration;
using System;

namespace FiftyOne.Pipeline.Engines.Caching
{
    /// <summary>
    /// Abstract base class for caches that use <see cref="IFlowData"/> as 
    /// the key.
    /// Internally, the cache actually uses a <see cref="DataKey"/> instance
    /// derived from <see cref="IFlowData"/>.
    /// </summary>
    /// <typeparam name="TValue">
    /// The type of data to store in the cache.
    /// </typeparam>
    public abstract class DataKeyedCacheBase<TValue> : IDataKeyedCache<TValue>
    {
        /// <summary>
        /// The cache that is actually used to store the data internally.
        /// </summary>
        private IPutCache<DataKey, TValue> _internalCache;
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="configuration">
        /// The cache configuration to use when creating the internal cache.
        /// </param>
        public DataKeyedCacheBase(CacheConfiguration configuration)
        {
            if(configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            _internalCache = configuration.Builder.Build<DataKey, TValue>(configuration.Size) 
                as IPutCache<DataKey, TValue>;
            if(_internalCache == null)
            {
                throw new PipelineConfigurationException(
                    $"Cache builder '{configuration.Builder.GetType().Name}' " +
                    $"does not produce caches conforming to 'IPutCache'");
            }
        }

        /// <summary>
        /// Get the <code>TValue</code> associated with the key generated 
        /// from the supplied <see cref="IFlowData"/> instance.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> to use as a key.
        /// </param>
        /// <returns>
        /// If a matching item exists in the cache then the 
        ///  <code>TValue</code> is returned. If not, the default value
        /// is returned. (i.e. null for reference types, 0 for int, etc) 
        /// </returns>
#pragma warning disable CA1043 // Use Integral Or String Argument For Indexers
        // At a lower level, the flow data is converted to a string 
        // key that is used on the cache itself.
        // We allow an IFlowData instance to be supplied as the key
        // at this level for convenience.
        public virtual TValue this[IFlowData data]
#pragma warning restore CA1043 // Use Integral Or String Argument For Indexers
        {
            get
            {
                if (data == null)
                {
                    throw new ArgumentNullException(nameof(data));
                }
                return _internalCache[data.GenerateKey(GetFilter())];
            }
        }

        /// <summary>
        /// Add the specified data to the cache.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> to use as a key.
        /// </param>
        /// <param name="value">
        /// The <code>TValue</code> to store with the key.
        /// </param>
        public virtual void Put(IFlowData data, TValue value)
        {
            if(data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            _internalCache.Put(data.GenerateKey(GetFilter()), value);
        }

        /// <summary>
        /// Returns the <see cref="IEvidenceKeyFilter"/> to use when 
        /// generating a key from <see cref="IFlowData"/> instances.
        /// Only evidence values that the filter includes will be used to 
        /// create the key.
        /// </summary>
        /// <returns>
        /// An <see cref="IEvidenceKeyFilter"/> instance.
        /// </returns>
        protected abstract IEvidenceKeyFilter GetFilter();

        /// <summary>
        /// IDisposable support
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~DataKeyedCacheBase()
        {
            Dispose(false);
        }

        /// <summary>
        /// Dispose of this instance
        /// </summary>
        /// <param name="disposing">
        /// True if this is called from the Dispose method.
        /// False if it is called from the finalizer.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            _internalCache.Dispose();
        }
    }
}
