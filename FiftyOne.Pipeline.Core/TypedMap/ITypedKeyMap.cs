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

namespace FiftyOne.Pipeline.Core.TypedMap
{
    /// <summary>
    /// Represents a collection that stores data of multiple different
    /// types.
    /// Data is accessed using an <see cref="ITypedKey{T}"/> that specifies
    /// the unique 'name' to store the data under and the type of the data
    /// being stored.
    /// </summary>
    internal interface ITypedKeyMap
    {
        /// <summary>
        /// Add the specified data to the collection using the specified key.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the data being stored.
        /// </typeparam>
        /// <param name="key">
        /// The key used to identify the data.
        /// </param>
        /// <param name="data">
        /// The data to store.
        /// </param>
        void Add<T>(ITypedKey<T> key, T data);

        /// <summary>
        /// Get the data associated with the specified key.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the data to return.
        /// </typeparam>
        /// <param name="key">
        /// The key used to access the data.
        /// </param>
        /// <returns>
        /// The data.
        /// </returns>
        T Get<T>(ITypedKey<T> key);

        /// <summary>
        /// Get the data associated with the specified type.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the data to return.
        /// </typeparam>
        /// <returns>
        /// The data.
        /// </returns>
        T Get<T>();

        /// <summary>
        /// Return string values of the keys in the map.
        /// </summary>
        /// <returns>
        /// Key strings.
        /// </returns>
        ICollection<string> GetKeys();

        /// <summary>
        /// Return the entire collection as a
        /// <see cref="IDictionary{TKey, TValue}"/> object.
        /// Note that this is the actual internal dictionary instance so any 
        /// changes to it will be reflected in the ITypedKeyMap object. 
        /// </summary>
        /// <returns>
        /// The data as a <see cref="IDictionary{TKey, TValue}"/>.
        /// </returns>
        IDictionary<string, object> AsStringKeyDictionary();

        /// <summary>
        /// Check if the map contains an item with the specified
        /// key name and type. If it does exist, retrieve it.
        /// </summary>
        /// <param name="key">
        /// The key to check for.
        /// </param>
        /// <param name="value">
        /// The value associated with the key.
        /// </param>
        /// <returns>
        /// True if an entry matching the key exists in the map. 
        /// False otherwise.
        /// </returns>
        bool TryGetValue<T>(ITypedKey<T> key, out T value);
    }
}
