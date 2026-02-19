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
using FiftyOne.Pipeline.Engines.FiftyOne.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Engines.FiftyOne.FlowElements
{
    /// <summary>
    /// Index of values and profile ids for a single property. Used by 
    /// <see cref="PropertyKeyedEngine{TData, TProfile}"/> to quickly look up
    /// profile ids from property values.
    /// </summary>
    public class PropertyKeyedIndex
    {
        /// <summary>
        /// The evidence keys to use for the property.
        /// </summary>
        public readonly string[] EvidenceKeys;

        /// <summary>
        /// The meta data of the indexed property.
        /// </summary>
        public readonly IFiftyOneAspectPropertyMetaData MetaData;

        /// <summary>
        /// Map of values for the property and the profile ids that relate
        /// to the value. Used to return the properties for the profile.
        /// The key is the property value and the value is the ordered list
        /// of profile ids associated with the value.
        /// </summary>
        public IReadOnlyDictionary<string, List<uint>> ValueData => _valueData;

        /// <summary>
        /// Internal dictionary used to add the values and profiles.
        /// </summary>
        private ConcurrentDictionary<string, List<uint>> _valueData =
            new ConcurrentDictionary<string, List<uint>>(
                StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Constructs a new instance of <see cref="PropertyKeyedIndex"/>.
        /// </summary>
        /// <param name="metaData"></param>
        public PropertyKeyedIndex(
            IFiftyOneAspectPropertyMetaData metaData)
        {
            MetaData = metaData;
            EvidenceKeys = new string[]
            {   
                Pipeline.Core.Constants.EVIDENCE_QUERY_PREFIX +
                Pipeline.Core.Constants.EVIDENCE_SEPERATOR +
                metaData.Name.ToLowerInvariant() 
            };
        }

        /// <summary>
        /// Adds the value and profile id to the property index.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="profileId"></param>
        public void Add(string value, uint profileId)
        {
            var existing = true;
            var list = _valueData.GetOrAdd(
                value,
                k =>
                {
                    existing = false;
                    return new List<uint>() { profileId };
                });
            if (existing)
            {
                lock(list)
                {
                    list.Add(profileId);
                }
            }
        }

        /// <summary>
        /// Returns the property name.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return MetaData.Name;
        }
    }
}
