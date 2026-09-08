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

using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Engines.Data;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace FiftyOne.Pipeline.DerivedProperty.Data
{
    /// <summary>
    /// Reads a source property as the string the data actually holds,
    /// rather than as the type the element declares for it.
    /// </summary>
    /// <remarks>
    /// A Boolean property read as a Boolean cannot say that the data holds
    /// no answer yet. The native conversion turns any stored value that is
    /// not the literal True into False and reports it as a value, so a
    /// placeholder such as Unknown arrives as a confident False. Reading
    /// the same property as a string recovers what is stored, because the
    /// string conversion hands the value over untouched.
    ///
    /// The accessor that takes the type to read as is protected, so it is
    /// reached by reflection and the result cached against the concrete
    /// data type. An element data type that does not offer one, which is
    /// every element that keeps its values in the base dictionary, reads
    /// as before.
    /// </remarks>
    internal static class SourceStringReader
    {
        /// <summary>
        /// The accessor for each concrete element data type met so far, or
        /// null against a type that has none. Element data types are few
        /// and fixed for the life of a pipeline, so the map stops growing
        /// almost immediately.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, MethodInfo>
            _accessors = new ConcurrentDictionary<Type, MethodInfo>();

        /// <summary>
        /// The value the data holds for a property, as a string.
        /// </summary>
        /// <param name="elementData">
        /// The element data to read from.
        /// </param>
        /// <param name="propertyName">
        /// The name of the property to read.
        /// </param>
        /// <param name="value">
        /// The stored value, where one was read.
        /// </param>
        /// <returns>
        /// True where the data could be read as a string.
        /// </returns>
        internal static bool TryRead(
            IElementData elementData,
            string propertyName,
            out string value)
        {
            value = null;
            var accessor = _accessors.GetOrAdd(
                elementData.GetType(),
                Accessor);
            if (accessor == null)
            {
                return false;
            }
            AspectPropertyValue<string> result;
            try
            {
                result = accessor.Invoke(
                    elementData,
                    new object[] { propertyName })
                    as AspectPropertyValue<string>;
            }
            // The reason a property cannot be read as a string is the
            // element's to give and not this element's to interpret, and
            // the caller has a value to fall back on either way, so any
            // failure here leaves the source read as it was.
            catch (Exception)
            {
                return false;
            }
            if (result == null || result.HasValue == false)
            {
                return false;
            }
            value = result.Value;
            return value != null;
        }

        /// <summary>
        /// The closed accessor for a data type, or null where the type does
        /// not offer one.
        /// </summary>
        /// <param name="type">
        /// The concrete element data type.
        /// </param>
        /// <returns>
        /// The method to call, or null.
        /// </returns>
        private static MethodInfo Accessor(Type type)
        {
            for (var declaring = type;
                declaring != null;
                declaring = declaring.BaseType)
            {
                var candidate = declaring
                    .GetMethods(
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly)
                    .FirstOrDefault(m =>
                        m.Name == "GetAs" &&
                        m.IsGenericMethodDefinition &&
                        m.GetGenericArguments().Length == 1 &&
                        m.GetParameters().Length == 1 &&
                        m.GetParameters()[0].ParameterType == typeof(string));
                if (candidate != null)
                {
                    try
                    {
                        return candidate.MakeGenericMethod(
                            typeof(AspectPropertyValue<string>));
                    }
                    catch (ArgumentException)
                    {
                        return null;
                    }
                }
            }
            return null;
        }
    }
}
