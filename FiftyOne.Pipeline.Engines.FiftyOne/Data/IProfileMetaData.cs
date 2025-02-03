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

using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Data
{
    /// <summary>
    /// Meta data relating to a profile within the data set.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/data-model-specification/README.md#profile">Specification</see>
    /// </summary>
    public interface IProfileMetaData : IEquatable<IProfileMetaData>, IComparable<IProfileMetaData>, IDisposable
    {
        /// <summary>
        /// Unique id of the profile.
        /// </summary>
        uint ProfileId { get; }

        /// <summary>
        /// The values which are defined in the profile (for some Engines 
        /// multiple profiles are required to build a full set of results).
        /// </summary>
        IEnumerable<IValueMetaData> GetValues();

        /// <summary>
        /// Gets the values associated with the profile and the property name.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns>Enumerable of values matching the property</returns>
        IEnumerable<IValueMetaData> GetValues(string propertyName);

        /// <summary>
        /// If there is a value for the profile with the property name and value
        /// then return an instance of it.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="valueName"></param>
        /// <returns>Value instance for property and value, or null if doesn't 
        /// exist</returns>
        IValueMetaData GetValue(string propertyName, string valueName);
        
        /// <summary>
        /// The component which the profile belongs to.
        /// </summary>
        IComponentMetaData Component { get; }

        /// <summary>
        /// The name of the profile. Usually indicates the type of device.
        /// </summary>
        string Name { get; }
    }
}
