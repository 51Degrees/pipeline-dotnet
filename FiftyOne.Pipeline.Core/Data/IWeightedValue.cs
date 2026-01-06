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

namespace FiftyOne.Pipeline.Core.Data
{
    /// <summary>
    /// The value with associated weighting.
    /// </summary>
    /// <typeparam name="T">
    /// Type of value stored within.
    /// </typeparam>
    public interface IWeightedValue<T>
    {
        /// <summary>
        /// "Integer" weight factor. 
        /// </summary>
        ushort RawWeighting { get; }
        
        /// <summary>
        /// A specific value stored within.
        /// </summary>
        T Value { get; }
    }

    /// <summary>
    /// Extensions for <see cref="IWeightedValue{T}"/>.
    /// </summary>
    public static class WeightedValueExtensions
    {
        /// <summary>
        /// Recalculates <see cref="IWeightedValue{T}.RawWeighting"/>
        /// into a floating point value in range (0~1).
        /// </summary>
        /// <param name="value">Value to get weighting from.</param>
        /// <typeparam name="T">Type of stored value.</typeparam>
        /// <returns>Weighting as (0~1) multiplier.</returns>
        public static float Weighting<T>(this IWeightedValue<T> value)
        {
            return value.RawWeighting / (float)ushort.MaxValue;
        }
    }
}