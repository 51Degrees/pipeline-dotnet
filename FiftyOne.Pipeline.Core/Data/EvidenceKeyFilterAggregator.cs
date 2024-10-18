/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2023 51 Degrees Mobile Experts Limited, Davidson House,
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

using System;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.Pipeline.Core.Data
{
    /// <summary>
    /// This implementation of <see cref="IEvidenceKeyFilter"/>
    /// aggregates multiple other filters using a logical OR approach.
    /// I.e. if any one of the child filters would allow the inclusion
    /// of an evidence key then this aggregator will allow it as well, even
    /// if none of the other child filters do.
    /// </summary>
    public class EvidenceKeyFilterAggregator : EvidenceKeyFilterWhitelist, IEvidenceKeyFilter
    {
        private List<IEvidenceKeyFilter> _filters;

        /// <summary>
        /// Constructor
        /// </summary>
        public EvidenceKeyFilterAggregator() :
            base(Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase)
        {
            _filters = new List<IEvidenceKeyFilter>();
        }

        /// <summary>
        /// Add a child filter to this aggregator.
        /// </summary>
        /// <param name="filter">
        /// The filter to add.
        /// </param>
        public void AddFilter(IEvidenceKeyFilter filter)
        {
            var inclusionListFilter = filter as EvidenceKeyFilterWhitelist;
            bool addFilter = true;

            if (inclusionListFilter != null)
            {
                // If the filter is an inclusion list filter using the 
                // OrdinalIgnoreCase comparer then add it's list to this 
                // instance's inclusion list to give better performance.
                if (inclusionListFilter.Comparer == StringComparer.OrdinalIgnoreCase)
                { 
                    addFilter = false;
                    foreach (var entry in inclusionListFilter.Whitelist)
                    {
                        if (_inclusionList.ContainsKey(entry.Key) == false)
                        {
                            _inclusionList.Add(entry.Key, entry.Value);
                        }
                    }

                    // If the filter is not directly a white list filter
                    // but a sub class then we still want to add it to the 
                    // list of filters.
                    if (filter.GetType().IsSubclassOf(typeof(EvidenceKeyFilterWhitelist)))
                    {
                        addFilter = true;
                    }
                }
            }
            // Add the filter to the list of child filters.
            if (addFilter)
            {
                _filters.Add(filter);
            }
        }
        
        /// <summary>
        /// Check if the specified evidence key is included by this filter.
        /// </summary>
        /// <param name="key">
        /// The evidence key to check.
        /// </param>
        /// <returns>
        /// True if the key is included and false if not.
        /// </returns>
        public override bool Include(string key)
        {
            // First check the inclusionList as this will be faster than
            // almost anything else (check against a hash table)
            bool include = base.Include(key);

            // Keep checking against child filters until we find the key
            // is included or we run out of filters.
            int index = 0;
            while(include == false && index < _filters.Count)
            {
                include = _filters[index].Include(key);
                index++;
            }

            return include;
        }

        /// <summary>
        /// Get the order of precedence of the specified key
        /// </summary>
        /// <param name="key">
        /// The key to check
        /// </param>
        /// <returns>
        /// The order, where lower values indicate a higher order of 
        /// precedence. 
        /// Null if the key is not included by the filter.
        /// </returns>
        public override int? Order(string key)
        {
            int? order = base.Order(key);

            int index = 0;
            while (order.HasValue == false && index < _filters.Count)
            {
                order = _filters[index].Order(key);
                index++;
            }
            return order;
        }
    }
}
