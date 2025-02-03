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

using System.Collections.Generic;

namespace FiftyOne.Pipeline.Engines.Data
{
    /// <summary>
    /// Represents a specific type of <see cref="IAspectData"/> that
    /// contains multiple <see cref="IAspectData"/> instances.
    /// This is used in cases where an engine can return multiple 
    /// results for a single evidence value.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the <see cref="IAspectData"/> instances to store
    /// in the list.
    /// </typeparam>
    public interface IMultiProfileData<T> : IAspectData
        where T : IAspectData
    {
        /// <summary>
        /// Get a list of the <see cref="IAspectData"/> objects that 
        /// have been added to this instance.
        /// </summary>
        IReadOnlyList<T> Profiles { get; }
        
        /// <summary>
        /// Add the specified <see cref="IAspectData"/> object to
        /// this instance.
        /// </summary>
        /// <param name="profile">
        /// The data object to add to the list.
        /// </param>
        void AddProfile(T profile);
    }
}
