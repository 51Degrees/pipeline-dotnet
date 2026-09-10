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

using System;
using System.Collections.Generic;
using System.Globalization;
#if NET8_0_OR_GREATER
using System.Text.Json;
#else
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
#endif

namespace FiftyOne.Pipeline.AgentSignature.Keys
{
    /// <summary>
    /// Reads a key directory or an agent card into plain dictionaries and
    /// lists.
    /// </summary>
    /// <remarks>
    /// The engines project this element builds on carries System.Text.Json
    /// on net8.0 and Newtonsoft.Json on netstandard2.0, because
    /// System.Text.Json does not load reliably on the .NET Framework
    /// consumers of the netstandard2.0 build. This class follows the same
    /// split so that the element itself needs no JSON package of its own.
    /// Reading into dictionaries rather than into declared classes keeps the
    /// two libraries behind one door, because the field names the drafts use
    /// (for example 'signature_agent' and 'rfc9309-product-token') would
    /// otherwise need a naming attribute from each library on every field.
    /// </remarks>
    internal static class JsonReader
    {
        /// <summary>
        /// Read a JSON document.
        /// </summary>
        /// <param name="json">The document text.</param>
        /// <param name="value">
        /// The document as nested
        /// <see cref="Dictionary{TKey, TValue}"/> and
        /// <see cref="List{T}"/> instances holding strings, whole numbers,
        /// fractional numbers, booleans and nulls.
        /// </param>
        /// <returns>True if the document could be read.</returns>
        public static bool TryParse(string json, out object value)
        {
            value = null;
            if (string.IsNullOrEmpty(json))
            {
                return false;
            }
            try
            {
#if NET8_0_OR_GREATER
                using (var document = JsonDocument.Parse(json))
                {
                    value = Convert(document.RootElement);
                }
#else
                using (var reader = new JsonTextReader(
                    new System.IO.StringReader(json)))
                {
                    reader.DateParseHandling = DateParseHandling.None;
                    value = Convert(JToken.ReadFrom(reader));
                }
#endif
                return true;
            }
            // 'JsonException' names the exception of whichever library the
            // using directives above brought in, so the one catch covers
            // both target frameworks.
            catch (JsonException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

#if NET8_0_OR_GREATER
        private static object Convert(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var map = new Dictionary<string, object>(
                        StringComparer.Ordinal);
                    foreach (var property in element.EnumerateObject())
                    {
                        map[property.Name] = Convert(property.Value);
                    }
                    return map;
                case JsonValueKind.Array:
                    var list = new List<object>();
                    foreach (var item in element.EnumerateArray())
                    {
                        list.Add(Convert(item));
                    }
                    return list;
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    if (element.TryGetInt64(out var whole))
                    {
                        return whole;
                    }
                    return element.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                default:
                    return null;
            }
        }
#else
        private static object Convert(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    var map = new Dictionary<string, object>(
                        StringComparer.Ordinal);
                    foreach (var property in (JObject)token)
                    {
                        map[property.Key] = Convert(property.Value);
                    }
                    return map;
                case JTokenType.Array:
                    var list = new List<object>();
                    foreach (var item in (JArray)token)
                    {
                        list.Add(Convert(item));
                    }
                    return list;
                case JTokenType.String:
                    return token.Value<string>();
                case JTokenType.Integer:
                    return token.Value<long>();
                case JTokenType.Float:
                    return token.Value<double>();
                case JTokenType.Boolean:
                    return token.Value<bool>();
                default:
                    return null;
            }
        }
#endif

        /// <summary>
        /// Read a JSON document that is expected to be an object.
        /// </summary>
        /// <param name="json">The document text.</param>
        /// <param name="value">The object read.</param>
        /// <returns>
        /// True if the document could be read and is an object.
        /// </returns>
        public static bool TryParseObject(
            string json,
            out IDictionary<string, object> value)
        {
            value = null;
            if (TryParse(json, out var parsed) &&
                parsed is IDictionary<string, object> map)
            {
                value = map;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Read a text field.
        /// </summary>
        /// <param name="source">The object to read from.</param>
        /// <param name="name">The field name.</param>
        /// <returns>
        /// The text, or null when the field is absent or is not text.
        /// </returns>
        public static string GetString(
            IDictionary<string, object> source,
            string name)
        {
            if (source != null &&
                source.TryGetValue(name, out var value) &&
                value is string text)
            {
                return text;
            }
            return null;
        }

        /// <summary>
        /// Read a whole number field.
        /// </summary>
        /// <param name="source">The object to read from.</param>
        /// <param name="name">The field name.</param>
        /// <returns>
        /// The number, or null when the field is absent or is not a number.
        /// </returns>
        public static long? GetLong(
            IDictionary<string, object> source,
            string name)
        {
            if (source != null && source.TryGetValue(name, out var value))
            {
                if (value is long whole)
                {
                    return whole;
                }
                if (value is double fraction)
                {
                    return (long)fraction;
                }
                if (value is string text &&
                    long.TryParse(
                        text,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var parsed))
                {
                    return parsed;
                }
            }
            return null;
        }

        /// <summary>
        /// Read an object field.
        /// </summary>
        /// <param name="source">The object to read from.</param>
        /// <param name="name">The field name.</param>
        /// <returns>
        /// The object, or null when the field is absent or is not an object.
        /// </returns>
        public static IDictionary<string, object> GetObject(
            IDictionary<string, object> source,
            string name)
        {
            if (source != null &&
                source.TryGetValue(name, out var value) &&
                value is IDictionary<string, object> map)
            {
                return map;
            }
            return null;
        }

        /// <summary>
        /// Read an array field.
        /// </summary>
        /// <param name="source">The object to read from.</param>
        /// <param name="name">The field name.</param>
        /// <returns>
        /// The array, or null when the field is absent or is not an array.
        /// </returns>
        public static IList<object> GetArray(
            IDictionary<string, object> source,
            string name)
        {
            if (source != null &&
                source.TryGetValue(name, out var value) &&
                value is IList<object> list)
            {
                return list;
            }
            return null;
        }
    }
}
