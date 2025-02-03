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

namespace FiftyOne.Pipeline.Core.Data
{
    /// <summary>
    /// Represents a class that can create new <see cref="DataKey"/> 
    /// instances.
    /// </summary>
    public interface IDataKeyBuilder
    {
        /// <summary>
        /// Add a key
        /// </summary>
        /// <param name="order">
        /// The order of precedence with lower values indicating that a 
        /// key is more likely to provide differentiation between instances.
        /// </param>
        /// <param name="keyName">
        /// The name of the key. This is used to order keys when they have
        /// the same order of precedence.
        /// </param>
        /// <param name="keyValue">
        /// The value of the key.
        /// </param>
        /// <returns>
        /// This instance of the <see cref="IDataKeyBuilder"/>.
        /// </returns>
        IDataKeyBuilder Add(int order, string keyName, object keyValue);


        /// <summary>
        /// Create and return a new DataKey based on the keys that 
        /// have been added.
        /// </summary>
        /// <returns>
        /// A new <see cref="DataKey"/> instance that can be used as a key 
        /// combining the values that have been supplied to this builder.
        /// </returns>
        DataKey Build();
    }
}
