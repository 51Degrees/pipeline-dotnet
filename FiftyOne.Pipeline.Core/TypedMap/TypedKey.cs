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

namespace FiftyOne.Pipeline.Core.TypedMap
{
    /// <summary>
    /// A key to a <see cref="ITypedKeyMap"/> data store.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the data that is associated with this key.
    /// </typeparam>
    public class TypedKey<T> : ITypedKey<T>
    {
        private string _name;

        /// <summary>
        /// The name of the key, used to identify and access the
        /// data in the store.
        /// </summary>
        public string Name => _name;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name">
        /// The name of the key, used to identify and access the
        /// data in the store.
        /// </param>
        public TypedKey(string name)
        {
            _name = name;
        }
    }
}
