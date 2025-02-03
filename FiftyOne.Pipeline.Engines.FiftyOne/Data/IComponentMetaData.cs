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
    /// Meta data relating to a component of an Engine's results e.g. Hardware.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/data-model-specification/README.md#component">Specification</see>
    /// </summary>
    public interface IComponentMetaData : IEquatable<IComponentMetaData>, IComparable<IComponentMetaData>, IDisposable
    {
        /// <summary>
        /// The unique Id of the component.
        /// </summary>
        byte ComponentId { get; }

        /// <summary>
        /// The name of the component.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The default profile which is used by the Engine for this component.
        /// </summary>
        IProfileMetaData DefaultProfile { get; }

        /// <summary>
        /// List of the properties which come under the umbrella of this
        /// component.
        /// </summary>
#pragma warning disable CA1721 // Property names should not match get methods
        // This would be a breaking change.
        // Note that 'GetProperties' has a slightly different purpose as
        // it returns an IEnumerable where 'Properties' returns an IReadOnlyList.
        IReadOnlyList<IFiftyOneAspectPropertyMetaData> Properties { get; }
#pragma warning restore CA1721 // Property names should not match get methods

        /// <summary>
        /// Get the properties which come under the umbrella of this component.
        /// </summary>
        /// <returns>Properties enumerable</returns>
        IEnumerable<IFiftyOneAspectPropertyMetaData> GetProperties();

        /// <summary>
        /// Get the property from the component which has the name provided.
        /// Null is returned if the component does not contain a property with
        /// the name provided
        /// </summary>
        /// <param name="propertyName">
        /// Name of the property to return
        /// </param>
        /// <returns>The property or null if not in this component</returns>
        IFiftyOneAspectPropertyMetaData GetProperty(string propertyName);
    }
}
