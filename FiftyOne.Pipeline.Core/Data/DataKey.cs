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

using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.Pipeline.Core.Data
{
    /// <summary>
    /// A DataKey is a multi-field key intended for use in caching and 
    /// similar scenarios.
    /// The key fields are stored in a list within the class.
    /// Create a new instance using a <see cref="DataKeyBuilder"/>.
    /// </summary>
    public class DataKey
    {
        /// <summary>
        /// Key field values, stored in order of precedence with the most
        /// likely to be different between instances coming first.
        /// </summary>
        private IList<object> KeyValues { get; set; }

        /// <summary>
        /// The calculated hash code for this key.
        /// </summary>
        private int _hashCode;

        /// <summary>
        /// Create a new DataKey instance
        /// </summary>
        /// <param name="keyValues">
        /// The values of the keys that make up this instance.
        /// Must be in order of precedence with the most
        /// likely to be different between instances coming first.
        /// </param>
        internal DataKey(IList<object> keyValues)
        {
            KeyValues = keyValues;
            _hashCode = 0;
            foreach(var entry in KeyValues)
            {
                if (entry != null)
                {
                    _hashCode ^= entry.GetHashCode();
                }
            }
        }
        
        /// <summary>
        /// Check if this instance is equal to another.
        /// </summary>
        /// <param name="obj">
        /// The other object to check for equality.
        /// </param>
        /// <returns>
        /// True if obj is a <see cref="DataKey"/> that contains the same 
        /// key fields, in the same order and with the same values as this 
        /// instance. False otherwise.
        /// </returns>
        public override bool Equals(object obj)
        {
            bool result = false;
            DataKey other = obj as DataKey;
            // Check if the object passed in is a DataKey and contains the 
            // same number of key fields.
            if (other != null && other.KeyValues.Count == KeyValues.Count)
            {
                result = true;
                int count = 0;
                // Check each key field in turn until the values fail to match
                // or we run out of key fields.
                while (result == true && count < KeyValues.Count)
                {
                    var thisValue = KeyValues.ElementAt(count);
                    if (thisValue == null)
                    {
                        result = other.KeyValues.ElementAt(count) == null;
                    }
                    else
                    {
                        result = thisValue    
                            .Equals(other.KeyValues.ElementAt(count));
                    }
                    count++;
                }
            }
            return result;
        }

        /// <summary>
        /// Get the hash code for this instance.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return _hashCode;
        }
    }
}
