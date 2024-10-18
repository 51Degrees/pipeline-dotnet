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
using System.Collections.ObjectModel;
using System.Linq;

namespace FiftyOne.Pipeline.Core.Data
{
    /// <summary>
    /// This evidence filter will only include keys that are in a list
    /// that is specified at construction time.
    /// </summary>
    public class EvidenceKeyFilterWhitelist : IEvidenceKeyFilter
    {
#pragma warning disable CA1051 // Do not declare visible instance fields
        // Extending classes can make direct use of these fields.

        /// <summary>
        /// The dictionary containing all keys to be included by the filter
        /// and the order of precedence.
        /// </summary>
        protected Dictionary<string, int> _inclusionList;
        /// <summary>
        /// The equality comparer that is used to determine if a supplied
        /// string key is in the inclusion list or not.
        /// By default, a case insensitive comparison is used.
        /// </summary>
        protected IEqualityComparer<string> _comparer = 
            StringComparer.OrdinalIgnoreCase;
#pragma warning restore CA1051 // Do not declare visible instance fields

        /// <summary>
        /// Get the keys in the inclusion list as a read only dictionary.
        /// </summary>
        public IReadOnlyDictionary<string, int> Whitelist
        {
            get
            {
                return new ReadOnlyDictionary<string, int>(_inclusionList);
            }
        }

        /// <summary>
        /// Get the equality comparer that is used to determine if a supplied
        /// string key is in the inclusion list or not.
        /// </summary>
        public IEqualityComparer<string> Comparer => _comparer;

        /// <summary>
        /// Constructor
        /// The filter will be case-insensitive. For a case-sensitive filter
        /// use the overload that takes an <see cref="IEqualityComparer{T}"/>.
        /// </summary>
        /// <param name="inclusionList">
        /// The list of evidence keys that is filter will include.
        /// By default, all keys will have the same order of precedence.
        /// </param>
        public EvidenceKeyFilterWhitelist(IEnumerable<string> inclusionList)
        {
            PopulateFromList(inclusionList);
        }

        /// <summary>
        /// Backwards compatible constructor
        /// The filter will be case-insensitive. For a case-sensitive filter
        /// use the overload that takes an <see cref="IEqualityComparer{T}"/>.
        /// </summary>
        /// <param name="inclusionList">
        /// The list of evidence keys that is filter will include.
        /// By default, all keys will have the same order of precedence.
        /// </param>
        public EvidenceKeyFilterWhitelist(List<string> inclusionList) :
            this((IEnumerable<string>)inclusionList) 
        { 
        }

        /// <summary>
        /// Backwards compatible constructor
        /// </summary>
        /// <param name="inclusionList">
        /// The list of evidence keys that is filter will include.
        /// By default, all keys will have the same order of precedence.
        /// </param>
        /// <param name="comparer">
        /// Comparator to use when comparing the keys.
        /// </param>
        public EvidenceKeyFilterWhitelist(
            List<string> inclusionList,
            IEqualityComparer<string> comparer)
            : this((IEnumerable<string>)inclusionList, comparer)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="inclusionList">
        /// The list of evidence keys that is filter will include.
        /// By default, all keys will have the same order of precedence.
        /// </param>
        /// <param name="comparer">
        /// Comparator to use when comparing the keys.
        /// </param>
        public EvidenceKeyFilterWhitelist(
            IEnumerable<string> inclusionList,
            IEqualityComparer<string> comparer)
        {
            _comparer = comparer;
            PopulateFromList(inclusionList);
        }

        /// <summary>
        /// Constructor
        /// The filter will be case-insensitive. For a case-sensitive filter
        /// use the overload that takes an <see cref="IEqualityComparer{T}"/>.
        /// </summary>
        /// <param name="inclusionList">
        /// The dictionary of evidence keys that is filter will include.
        /// The order of precedence of each key is given by the value of
        /// the key/value pair.
        /// </param>
        public EvidenceKeyFilterWhitelist(IReadOnlyDictionary<string, int> inclusionList)
        {
            PopulateFromDictionary(inclusionList);
        }

        /// <summary>
        /// Backwards compatible constructor
        /// The filter will be case-insensitive. For a case-sensitive filter
        /// use the overload that takes an <see cref="IEqualityComparer{T}"/>.
        /// </summary>
        /// <param name="inclusionList">
        /// The dictionary of evidence keys that is filter will include.
        /// The order of precedence of each key is given by the value of
        /// the key/value pair.
        /// </param>
        public EvidenceKeyFilterWhitelist(Dictionary<string, int> inclusionList)
            : this((IReadOnlyDictionary<string, int>)inclusionList)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="inclusionList">
        /// The dictionary of evidence keys that is filter will include.
        /// The order of precedence of each key is given by the value of
        /// the key/value pair.
        /// </param>
        /// <param name="comparer">
        /// Comparator to use when comparing the keys.
        /// </param>
        public EvidenceKeyFilterWhitelist(
            IReadOnlyDictionary<string, int> inclusionList,
            IEqualityComparer<string> comparer)
        {
            _comparer = comparer;
            PopulateFromDictionary(inclusionList);
        }

        /// <summary>
        /// Backwards compatible constructor
        /// </summary>
        /// <param name="inclusionList">
        /// The dictionary of evidence keys that is filter will include.
        /// The order of precedence of each key is given by the value of
        /// the key/value pair.
        /// </param>
        /// <param name="comparer">
        /// Comparator to use when comparing the keys.
        /// </param>
        public EvidenceKeyFilterWhitelist(
            Dictionary<string, int> inclusionList,
            IEqualityComparer<string> comparer)
            : this((IReadOnlyDictionary<string, int>)inclusionList, comparer)
        {
        }


        private void PopulateFromList(IEnumerable<string> inclusionList)
        {
            _inclusionList = inclusionList.ToDictionary(w => w, w => 0, _comparer);
        }

        private void PopulateFromDictionary(IReadOnlyDictionary<string, int> inclusionList)
        {
            _inclusionList = inclusionList.ToDictionary(w => w.Key, w => w.Value, _comparer);
        }

        /// <summary>
        /// Check if the specified evidence key is included by this filter.
        /// </summary>
        /// <param name="key">
        /// The key to check
        /// </param>
        /// <returns>
        /// True if the key is included and false if not.
        /// </returns>
        public virtual bool Include(string key)
        {
            return _inclusionList.ContainsKey(key);
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
        /// Null if the key is not in the inclusion list.
        /// </returns>
        public virtual int? Order(string key)
        {
            int? result = 0;
            int temp;
            if(_inclusionList.TryGetValue(key, out temp) == false)
            {
                result = null;
            }
            else
            {
                result = temp;
            }
            return result;
        }
    }
}
