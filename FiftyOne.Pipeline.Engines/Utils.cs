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

using FiftyOne.Pipeline.Engines.Data;
using System;

namespace FiftyOne.Pipeline.Engines
{
    /// <summary>
    /// Static utility methods
    /// </summary>
    public static class Utils
    {
        /// <summary>
        /// Check if the specified type is a specific type 'T' or an
        /// implementation of <see cref="IAspectPropertyValue{T}"/>
        /// that wraps 'T'.
        /// </summary>
        /// <typeparam name="T">
        /// The type to check for
        /// </typeparam>
        /// <param name="type">
        /// The type to check
        /// </param>
        /// <returns>
        /// True if 'type' is of type 'T' or a wrapper around 'T' 
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if required parameters are null
        /// </exception>
        public static bool IsTypeOrAspectPropertyValue<T>(Type type)
        {
            if(type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }
            return type.Equals(typeof(T)) ||
                typeof(IAspectPropertyValue<T>).IsAssignableFrom(type);
        }


    }
}
