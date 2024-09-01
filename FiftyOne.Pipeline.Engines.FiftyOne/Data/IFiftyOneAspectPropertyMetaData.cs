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

using FiftyOne.Pipeline.Engines.Data;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Data
{
    /// <summary>
    /// 51Degrees specific meta data. This adds meta data properties which are
    /// available in 51Degrees Engines.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/data-model-specification/README.md#property">Specification</see>
    /// </summary>
    public interface IFiftyOneAspectPropertyMetaData : IAspectPropertyMetaData, 
        IEquatable<IFiftyOneAspectPropertyMetaData>, 
        IComparable<IFiftyOneAspectPropertyMetaData>, IEquatable<string>, IDisposable
    {
        /// <summary>
        /// URL relating to the property.
        /// </summary>
#pragma warning disable CA1056 // Uri properties should not be strings
        string Url { get; }
#pragma warning restore CA1056 // Uri properties should not be strings

        /// <summary>
        /// Order to display the property.
        /// </summary>
        byte DisplayOrder { get; }

        /// <summary>
        /// True if the property is mandatory.
        /// </summary>
        bool Mandatory { get; }

        /// <summary>
        /// True if the property value type is a list.
        /// </summary>
        bool List { get; }

        /// <summary>
        /// True if the property is obsolete.
        /// </summary>
        bool Obsolete { get; }

        /// <summary>
        /// True if the property should be shown.
        /// </summary>
        bool Show { get; }

        /// <summary>
        /// True if the values of the property should be shown.
        /// </summary>
        bool ShowValues { get; }

        /// <summary>
        /// The component which the property belongs to.
        /// </summary>
#pragma warning disable CA1721 // Property names should not match get methods
        // Method marked as obsolete. warning suppression can
        // be removed once the method is removed.
        IComponentMetaData Component { get; }
#pragma warning restore CA1721 // Property names should not match get methods

        /// <summary>
        /// The list of values which relate to the property i.e. the values
        /// which can be returned for this property.
        /// </summary>
#pragma warning disable CA1721 // Property names should not match get methods
        // This would be a breaking change.
        // Note that 'GetValues' has a slightly different purpose as
        // it returns an IEnumerable where 'Value' returns an IReadOnlyList.
        IReadOnlyList<IValueMetaData> Values { get; }
#pragma warning restore CA1721 // Property names should not match get methods

        /// <summary>
        /// The default value for this property.
        /// </summary>
#pragma warning disable CA1721 // Property names should not match get methods
        // Method marked as obsolete. warning suppression can
        // be removed once the method is removed.
        IValueMetaData DefaultValue { get; }
#pragma warning restore CA1721 // Property names should not match get methods

        /// <summary>
        /// Get the component which the property belongs to.
        /// </summary>
        /// <returns>The component</returns>
        [Obsolete("Will be removed in a future version")]
        IComponentMetaData GetComponent();

        /// <summary>
        /// Get the values which relate to the property i.e. the values which
        /// can be returned for this property.
        /// </summary>
        /// <returns>Values enumerable</returns>
        IEnumerable<IValueMetaData> GetValues();

        /// <summary>
        /// Get the value from the property which has the name provided.
        /// Null is returned if the property does not contain a value with the
        /// name provided
        /// </summary>
        /// <param name="valueName">
        /// Name of the value to return
        /// </param>
        /// <returns>The value or null if not in this property</returns>
        IValueMetaData GetValue(string valueName);

        /// <summary>
        /// Get the default value for this property.
        /// </summary>
        /// <returns>Default value</returns>
        [Obsolete("Will be removed in a future version")]
        IValueMetaData GetDefaultValue();
    }
}
