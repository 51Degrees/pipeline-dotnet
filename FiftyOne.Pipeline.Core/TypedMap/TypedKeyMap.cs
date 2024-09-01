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

using FiftyOne.Pipeline.Core.Exceptions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.Pipeline.Core.TypedMap
{
    /// <summary>
    /// A collection that stores data of multiple different types.
    /// Data is accessed using an <see cref="ITypedKey{T}"/> that specifies
    /// the unique 'name' to store the data under and the type of the data
    /// being stored.
    /// </summary>
    internal class TypedKeyMap : ITypedKeyMap
    {
        /// <summary>
        /// The internal data store.
        /// </summary>
        private IDictionary<string, object> _data;

        /// <summary>
        /// Default constructor.
        /// Creates a non-thread safe <see cref="TypedKeyMap"/>
        /// </summary>
        public TypedKeyMap() : this(false)
        { }

        /// <summary>
        /// Constructor.
        /// Creates a thread-safe or non-thread safe <see cref="TypedKeyMap"/>
        /// </summary>
        /// <param name="threadSafe">
        /// If true then the internal collection is thread-safe. If false
        /// then it is not.
        /// </param>
        public TypedKeyMap(bool threadSafe)
        {
            if (threadSafe)
            {
                _data = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                _data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            }
        }

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
        public void Add<T>(ITypedKey<T> key, T data)
        {
            if (_data.ContainsKey(key.Name) == false)
            {
                _data.Add(key.Name, data);
            }
            else
            {
                _data[key.Name] = data;
            }
        }

        /// <summary>
        /// Get the data associated with the specified key.
        /// If the key is not present or the data value is null then the 
        /// return value will be <code>default(T)</code>.
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
        /// <exception cref="InvalidCastException">
        /// Thrown if the data object stored under the name of the key 
        /// cannot be cast to the expected type T.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the key argument is null
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if the Name property of the key argument is null
        /// </exception>
        /// <exception cref="KeyNotFoundException">
        /// Thrown if the map does not contain an entry for the key
        /// </exception>
        public T Get<T>(ITypedKey<T> key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            if (key.Name == null)
            {
                throw new ArgumentException(Messages.ExceptionKeyNameNull, 
                    nameof(key));
            }

            T result = default(T);
            if (_data.ContainsKey(key.Name))
            {
                object obj = _data[key.Name];
                if (obj != null)
                {
                    result = (T)obj;
                }
            }
            else
            {
                throw new KeyNotFoundException(GetKeyMissingMessage(key.Name));
            }
            return result;
        }

        public T Get<T>()
        {
            var matches = _data
                .Where(kvp => kvp.Value.GetType() is T);
            if (matches.Any() == false)
            {
                matches = _data.Where(kvp => typeof(T)
                    .IsAssignableFrom(kvp.Value.GetType()));
            }

            if (matches.Count() == 1)
            {
                return (T)matches.Single().Value;
            }
            else if (matches.Any() == false)
            {
                throw new PipelineDataException(
                    $"This map contains no data matching type " +
                    $"'{typeof(T).Name}'");
            }
            else
            {
                throw new PipelineDataException($"This map contains " +
                    $"multiple data instances matching type '{typeof(T).Name}'");
            }
        }

        private String GetKeyMissingMessage(string key)
        {
            return "There is no data for '" + key + "' against this " +
                "instance of '" + this.GetType().Name +
                "'. Available keys are: " +
                string.Join(", ", GetKeys());
        }

        /// <summary>
        /// Return string values of the keys in the map.
        /// </summary>
        /// <returns>
        /// Key strings
        /// </returns>
        public ICollection<string> GetKeys()
        {
            return _data.Keys;
        }

        /// <summary>
        /// Return the entire collection as a
        /// <see cref="IDictionary{TKey, TValue}"/> object.
        /// Note that this is the actual internal dictionary instance so any 
        /// changes to it will be reflected in the TypedKeyMap object. 
        /// </summary>
        /// <returns>
        /// The data as a <see cref="IDictionary{TKey, TValue}"/>.
        /// </returns>
        public IDictionary<string, object> AsStringKeyDictionary()
        {
            return _data;
        }

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
        public bool TryGetValue<T>(ITypedKey<T> key, out T value)
        {
            bool result = false;
            value = default(T);

            object valueObj = null;
            if(_data.TryGetValue(key.Name, out valueObj))
            {
                if(typeof(T).IsAssignableFrom(valueObj.GetType()))
                {
                    value = (T)valueObj;
                    result = true;
                }
            }

            return result;
        }
    }
}
