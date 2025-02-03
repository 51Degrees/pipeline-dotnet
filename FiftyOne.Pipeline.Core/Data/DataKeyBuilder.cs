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

using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.Pipeline.Core.Data
{
    /// <summary>
    /// Class to assist with the creation of <see cref="DataKey"/> instances
    /// </summary>
    public class DataKeyBuilder : IDataKeyBuilder
    {
        /// <summary>
        /// A collection used to store the keys that have been added.
        /// The first key is the order of precedence for the key with lower 
        /// values indicating that a key is more likely to provide 
        /// differentiation between instances.
        /// The second is the keyName. This is used to order keys when
        /// they have the same order of precedence.
        /// The value is the key value for this instance.
        /// </summary>
        private List<KeyValuePair<int, KeyValuePair<string, object>>> _keys = 
            new List<KeyValuePair<int, KeyValuePair<string, object>>>();

        /// <summary>
        /// Add a key
        /// </summary>
        /// <param name="order">
        /// The order of precedence with lower values indicating that a 
        /// key is more likely to provide differentiation between instances.
        /// </param>
        /// <param name="keyName">
        /// The name of the key. This is used to order keys when they have
        /// the same order of precedence.
        /// </param>
        /// <param name="keyValue">
        /// The value of the key.
        /// </param>
        /// <returns>
        /// This instance of the <see cref="DataKeyBuilder"/>.
        /// </returns>
        public IDataKeyBuilder Add(int order, string keyName, object keyValue)
        {
            _keys.Add(new KeyValuePair<int, KeyValuePair<string, object>>(order, 
                new KeyValuePair<string, object>(keyName, keyValue)));
            return this;
        }

        /// <summary>
        /// Create and return a new DataKey based on the keys that 
        /// have been added.
        /// </summary>
        /// <returns>
        /// A new <see cref="DataKey"/> instance that can be used as a key 
        /// combining the values that have been supplied to this builder.
        /// </returns>
        public DataKey Build()
        {
            return new DataKey(_keys
                .OrderBy(k => k.Key)
                .ThenBy(k => k.Value.Key)
                .Select(k => k.Value.Value).ToList());
        }
    }
}
