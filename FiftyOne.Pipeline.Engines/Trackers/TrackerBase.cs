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
using FiftyOne.Pipeline.Engines.Caching;
using FiftyOne.Pipeline.Engines.Configuration;

namespace FiftyOne.Pipeline.Engines.Trackers
{
    /// <summary>
    /// The abstract base class for trackers.
    /// A tracker is a data structure that stores meta-data relating to a 
    /// key derived from a given <see cref="IFlowData"/> instance.
    /// The details of key creation and the specifics of the meta-data are
    /// determined by the tracker implementation.
    /// The key will always be a <see cref="DataKey"/> instance as defined
    /// by <see cref="DataKeyedCacheBase{TValue}"/>.
    /// The meta-data can be any type and is specified using the generic
    /// type parameter <code>TValue</code>.
    /// </summary>
    /// <remarks>
    /// As an example, a tracker could create a key using the source IP 
    /// address from the <see cref="IFlowData"/> evidence and use the 
    /// associated meta-data to store a count of the number of times a given 
    /// source IP has been seen.
    /// </remarks>
    /// <typeparam name="TValue">
    /// The type of the meta-data object that the tracker stores with each
    /// key value.
    /// </typeparam>
    public abstract class TrackerBase<TValue> : 
        DataKeyedCacheBase<TValue>, ITracker
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="configuration">
        /// The cache configuration to use when building the cache that
        /// is used internally by the tracker.
        /// </param>
        public TrackerBase(CacheConfiguration configuration) 
            : base(configuration)
        {
        }

        /// <summary>
        /// Create a tracker value instance.
        /// Called when a new key is added to the tracker.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> that is being added to the tracker.
        /// </param>
        /// <returns>
        /// A new meta-data instance to be stored with the 
        /// <see cref="DataKey"/> created from the flow data instance.
        /// </returns>
        protected abstract TValue NewValue(IFlowData data);

        /// <summary>
        /// Update the tracker value with relevant details.
        /// Called when a tracked item matches an instance already in 
        /// the tracker.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> that has matched an existing entry
        /// in the tracker.
        /// </param>
        /// <param name="value">
        /// The meta-data instance that the tracker holds for the key 
        /// generated from the data.
        /// </param>
        /// <returns>
        /// True if the tracker's logic allows further processing of this
        /// flow data instance. False otherwise.
        /// </returns>
        protected abstract bool Match(IFlowData data, TValue value);

        /// <summary>
        /// Track the specified <see cref="IFlowData"/> instance.
        /// If the key created from the data does not yet exist in the 
        /// tracker then it will be added. If it does already exist
        /// then the meta-data will be updated according to the tracker
        /// implementation's logic.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> to track.
        /// </param>
        /// <returns>
        /// True if no matching entry existed in the tracker or if the 
        /// entry existed but further processing is allowed by the tracker's 
        /// logic.
        /// False if a matching entry was found and the tracker's logic does
        /// not allow further processing of this flow data instance.
        /// </returns>
        public bool Track(IFlowData data)
        {
            bool result = true;
            TValue value = this[data];
            if (value == null)
            {
                // If the tracker does not already have a matching item
                // then create one and store it.
                Put(data, NewValue(data));
            }
            else
            {
                // If the tracker does already have a matching item then
                // call Match to update it based on the new data.
                result = Match(data, value);
            }

            return result;
        }
    }
}
