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
using System;

namespace FiftyOne.Pipeline.Engines.Data
{
    /// <summary>
    /// Safe accessors for <see cref="IAspectPropertyValue{T}"/> that return
    /// a fallback instead of throwing when a value cannot be provided.
    /// </summary>
    /// <remarks>
    /// There are two distinct failure modes when reading an aspect
    /// property, and callers commonly need to treat both as "value
    /// unknown":
    /// <list type="number">
    /// <item><description>
    /// The property reference resolves but has no value for this
    /// evidence. <see cref="IAspectPropertyValue.HasValue"/> is false and
    /// reading <see cref="IAspectPropertyValue{T}.Value"/> throws
    /// <see cref="Exceptions.NoValueException"/>.
    /// </description></item>
    /// <item><description>
    /// The property reference itself cannot be resolved, for example
    /// because an upstream cloud request failed or the data file does not
    /// include the property. The typed property accessor (such as a
    /// device-data <c>HardwareName</c> getter) throws
    /// <see cref="PropertyMissingException"/> before any
    /// <see cref="IAspectPropertyValue{T}"/> instance exists, so
    /// extensions on the property value cannot guard it. The selector
    /// overloads below take the accessor as a function and guard the
    /// whole read.
    /// </description></item>
    /// </list>
    /// </remarks>
    public static class AspectPropertyValueExtensions
    {
        /// <summary>
        /// True if <paramref name="property"/> is not null and has a value.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the value stored in the property.
        /// </typeparam>
        /// <param name="property">
        /// The property value instance to check. May be null.
        /// </param>
        /// <returns>
        /// True when a value can be read, false otherwise.
        /// </returns>
        public static bool SafeHasValue<T>(
            this IAspectPropertyValue<T> property)
        {
            try
            {
                return property != null && property.HasValue;
            }
            catch (PropertyMissingException)
            {
                return false;
            }
        }

        /// <summary>
        /// The value of <paramref name="property"/>, or
        /// <paramref name="fallback"/> when the property is null or has
        /// no value.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the value stored in the property.
        /// </typeparam>
        /// <param name="property">
        /// The property value instance to read. May be null.
        /// </param>
        /// <param name="fallback">
        /// The value to return when no value can be read.
        /// </param>
        /// <returns>
        /// The property's value, or <paramref name="fallback"/>.
        /// </returns>
        public static T SafeValue<T>(
            this IAspectPropertyValue<T> property,
            T fallback = default)
        {
            try
            {
                return property != null && property.HasValue
                    ? property.Value
                    : fallback;
            }
            catch (PropertyMissingException)
            {
                return fallback;
            }
        }

        /// <summary>
        /// True if the property selected by
        /// <paramref name="propertySelector"/> can be resolved and has a
        /// value. Unlike calling <see cref="SafeHasValue{T}"/> on the
        /// property directly, this guards the resolution of the property
        /// reference itself, which throws
        /// <see cref="PropertyMissingException"/> when, for example, an
        /// upstream cloud request failed.
        /// </summary>
        /// <typeparam name="TData">
        /// The element data type exposing the property.
        /// </typeparam>
        /// <typeparam name="TValue">
        /// The type of the value stored in the property.
        /// </typeparam>
        /// <param name="data">
        /// The element data to read from. May be null.
        /// </param>
        /// <param name="propertySelector">
        /// A function selecting the property to read, for example
        /// <c>d =&gt; d.HardwareName</c>.
        /// </param>
        /// <returns>
        /// True when a value can be read, false otherwise.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="propertySelector"/> is null.
        /// </exception>
        public static bool SafeHasValue<TData, TValue>(
            this TData data,
            Func<TData, IAspectPropertyValue<TValue>> propertySelector)
            where TData : IElementData
        {
            if (propertySelector == null)
            {
                throw new ArgumentNullException(nameof(propertySelector));
            }
            if (data == null)
            {
                return false;
            }
            try
            {
                return propertySelector(data).SafeHasValue();
            }
            catch (PropertyMissingException)
            {
                return false;
            }
        }

        /// <summary>
        /// The value of the property selected by
        /// <paramref name="propertySelector"/>, or
        /// <paramref name="fallback"/> when it cannot be provided. Unlike
        /// calling <see cref="SafeValue{T}"/> on the property directly,
        /// this guards the resolution of the property reference itself,
        /// which throws <see cref="PropertyMissingException"/> when, for
        /// example, an upstream cloud request failed.
        /// </summary>
        /// <typeparam name="TData">
        /// The element data type exposing the property.
        /// </typeparam>
        /// <typeparam name="TValue">
        /// The type of the value stored in the property.
        /// </typeparam>
        /// <param name="data">
        /// The element data to read from. May be null.
        /// </param>
        /// <param name="propertySelector">
        /// A function selecting the property to read, for example
        /// <c>d =&gt; d.HardwareName</c>.
        /// </param>
        /// <param name="fallback">
        /// The value to return when no value can be read.
        /// </param>
        /// <returns>
        /// The property's value, or <paramref name="fallback"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="propertySelector"/> is null.
        /// </exception>
        public static TValue SafeValue<TData, TValue>(
            this TData data,
            Func<TData, IAspectPropertyValue<TValue>> propertySelector,
            TValue fallback = default)
            where TData : IElementData
        {
            if (propertySelector == null)
            {
                throw new ArgumentNullException(nameof(propertySelector));
            }
            if (data == null)
            {
                return fallback;
            }
            try
            {
                return propertySelector(data).SafeValue(fallback);
            }
            catch (PropertyMissingException)
            {
                return fallback;
            }
        }
    }
}
