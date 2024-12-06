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

namespace FiftyOne.Pipeline.Core.Data
{
    /// <summary>
    /// Represents property values
    /// </summary>
    public interface IData
    {
        /// <summary>
        /// Get the data contained in this instance as an 
        /// <see cref="IReadOnlyDictionary{TKey, TValue}"/>
        /// </summary>
        /// <returns>
        /// The data
        /// </returns>
        IReadOnlyDictionary<string, object> AsDictionary();

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
        /// The key-value pairs to transfer to this data instance.
        /// </param>
        void PopulateFrom(IEnumerable<KeyValuePair<string, object>> values);

        /// <summary>
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
        void PopulateFromDictionary(IDictionary<string, object> values);

        /// <summary>
        /// Get or set a data value
        /// </summary>
        /// <param name="key">
        /// The name of the property
        /// </param>
        /// <returns>
        /// The property value
        /// </returns>
        object this[string key] { get; set;  }
    }
}
