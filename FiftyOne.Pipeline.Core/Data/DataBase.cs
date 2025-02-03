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

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Core.Data
{
    /// <summary>
    /// This class stores data values as key/value pairs where
    /// the key is a string and the value can be any type.
    /// </summary>
    public abstract class DataBase : IData
    {
        /// <summary>
        /// The data
        /// </summary>
        private IDictionary<string, object> _data;

        /// <summary>
        /// Logger
        /// </summary>
        private ILogger<DataBase> _logger;

        /// <summary>
        /// Constructor
        /// Creates a <see cref="DataBase"/> instance with a 
        /// non-thread-safe, case-insensitive dictionary.
        /// </summary>
        /// <param name="logger">
        /// Used for logging
        /// </param>
        public DataBase(ILogger<DataBase> logger) :
            this(logger, new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase))
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="dictionary">
        /// The dictionary instance to use internally when storing data values.
        /// </param>
        /// <param name="logger">
        /// Used for logging
        /// </param>
        public DataBase(ILogger<DataBase> logger,
            IDictionary<string, object> dictionary)
        {
            if (dictionary == null)
            {
                dictionary = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            }
            _data = dictionary;
            _logger = logger;
        }

        /// <summary>
        /// Get or set a data value
        /// </summary>
        /// <param name="key">
        /// The name of the property
        /// </param>
        /// <returns>
        /// The property value
        /// </returns>
        public virtual object this[string key]
        {
            get
            {
                return GetAs<object>(key);
            }
            set
            {
                if (_data.ContainsKey(key))
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(
                            $"Data '{GetType().Name}' " +
                            $"overwriting existing value for '{key}' " +
                            $"(old value '{AsTruncatedString(_data[key])}', " +
                            $"new value '{AsTruncatedString(value)}').");
                    }
                    _data[key] = value;
                }
                else
                {
                    _data.Add(key, value);
                }
            }
        }

        /// <summary>
        /// Get the data contained in this instance as an 
        /// <see cref="IReadOnlyDictionary{TKey, TValue}"/>.
        /// </summary>
        /// <returns>
        /// The data
        /// </returns>
        public virtual IReadOnlyDictionary<string, object> AsDictionary()
        {
            return (IReadOnlyDictionary<string, object>)_data;
        }

        /// <summary>
        /// Use the values in the specified enumerable to populate
        /// this data instance.
        /// </summary>
        /// <remarks>
        /// The data will not be cleared before the new values are added.
        /// The new values will overwrite old values if any exist with the
        /// same keys.
        /// </remarks>
        /// <param name="values">
        /// The values to transfer to this data instance.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied dictionary is null
        /// </exception>
        public void PopulateFrom(IEnumerable<KeyValuePair<string, object>> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            foreach (var value in values)
            {
                this[value.Key] = value.Value;
            }
        }
        /// <summary>
        /// Deprecated. Use PopulateFrom method.
        /// Use the values in the specified dictionary to populate
        /// this data instance.
        /// </summary>
        /// <remarks>
        /// The data will not be cleared before the new values are added.
        /// The new values will overwrite old values if any exist with the
        /// same keys.
        /// </remarks>
        /// <param name="values">
        /// The values to transfer to this data instance.
        /// </param>
        [Obsolete("PopulateFromDictionary is deprecated. Use PopulateFrom(IEnumerable<KeyValuePair<string, object>>) instead.")]
        public void PopulateFromDictionary(IDictionary<string, object> values)
        {
            PopulateFrom(values);
        }

        /// <summary>
        /// Get the value associated with the specified key as the 
        /// specified type.
        /// </summary>
        /// <typeparam name="T">
        /// The type to return
        /// </typeparam>
        /// <param name="key">
        /// The key to get the value for.
        /// </param>
        /// <returns>
        /// The value associated with the specified key cast to type 'T'.
        /// If there is no value for the given key then the default value 
        /// for the type is returned.
        /// </returns>
        /// <exception cref="InvalidCastException">
        /// Thrown if the value associated with the specified key cannot be 
        /// cast to type 'T'
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the specified key is null
        /// </exception>
        protected virtual T GetAs<T>(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (_logger != null &&
                _logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug($"Data '{GetType().Name}' " +
                    $"property value requested for key '{key}'.");
            }
            T result = default(T);
            if (_data.ContainsKey(key))
            {
                result = (T)_data[key];
            }
            return result;
        }

        /// <summary>
        /// Get the string representation of the specified object.
        /// If the string is longer than 50 characters then truncate it.
        /// </summary>
        /// <param name="value">
        /// The object to return as a string.
        /// </param>
        /// <returns>
        /// The string representation of the specified object.
        /// </returns>
        private string AsTruncatedString(object value)
        {
            var str = value == null ? "NULL" : value.ToString();
            if (str.Length > 50)
            {
                str = str.Remove(47) + "...";
            }
            return str;
        }
    }
}
