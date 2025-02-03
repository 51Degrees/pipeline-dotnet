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

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace FiftyOne.Pipeline.CloudRequestEngine.Data
{
    /// <summary>
    /// Custom JsonConverter for use when deserialising data from 51Degrees
    /// cloud service.
    /// The converter converts integer values to Int32 instead of Int64 and
    /// converts Json arrays to <![CDATA[List<string>]]>.
    /// </summary>
    public class CloudJsonConverter : JsonConverter
    {
        /// <summary>
        /// Returns true if the converter can convert objects of the 
        /// supplied type.
        /// </summary>
        /// <param name="objectType">
        /// The type to check against.
        /// </param>
        /// <returns>
        /// True if the converter can convert the supplied type.
        /// False if not.
        /// </returns>
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Dictionary<string, object>);
        }

        /// <summary>
        /// If true then this converter can be used for writing as well
        /// as reading JSON.
        /// </summary>
        public override bool CanWrite => false;

        /// <summary>
        /// Convert the data from the given reader.
        /// </summary>
        /// <param name="reader">
        /// The reader to read from.
        /// </param>
        /// <param name="objectType">
        /// The type of the object to return.
        /// </param>
        /// <param name="existingValue">
        /// ??
        /// </param>
        /// <param name="serializer">
        /// The serializer to use when reading the JSON.
        /// </param>
        /// <returns>
        /// The data object.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if a required parameter is null.
        /// </exception>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));

            Dictionary<string, object> result = new Dictionary<string, object>();
            reader.Read();

            while (reader.TokenType == JsonToken.PropertyName)
            {
                string propertyName = reader.Value as string;
                reader.Read();
                object value;

                // If the value is an array then create a new string list 
                // and add each item in turn.
                if (reader.TokenType == JsonToken.StartArray)
                {
                    var list = new List<string>();
                    reader.Read();
                    while (reader.TokenType != JsonToken.EndArray)
                    {
                        list.Add(reader.Value.ToString());
                        reader.Read();
                    }
                    value = list;
                }
                else if (reader.TokenType == JsonToken.Integer)
                {
                    // convert to Int32 instead of Int64
                    value = Convert.ToInt32(reader.Value,
                        CultureInfo.InvariantCulture);
                }
                else
                {
                    // let the serializer handle all other cases
                    value = serializer.Deserialize(reader);
                }

                result.Add(propertyName, value);
                reader.Read();
            }

            return result;
        }

        /// <summary>
        /// Write the given value to JSON
        /// </summary>
        /// <param name="writer">
        /// The writer to use when writing the output.
        /// </param>
        /// <param name="value">
        /// The value to be converted to JSON
        /// </param>
        /// <param name="serializer">
        /// Not used.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if one of the supplied parameters is null.
        /// </exception>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
