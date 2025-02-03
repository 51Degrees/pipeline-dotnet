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

using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Data.Types;
using FiftyOne.Pipeline.Engines.FlowElements;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Engines.Data
{
    /// <summary>
    /// Meta-data relating to properties that are populated by Aspect Engines.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/properties.md#aspect-property-metadata">Specification</see>
    /// </summary>
    public class AspectPropertyMetaData : ElementPropertyMetaData, 
        IAspectPropertyMetaData
    {
        /// <summary>
        /// A list of the data tiers that can be used to determine values 
        /// for this property.
        /// Examples values are:
        /// Lite
        /// Premium
        /// Enterprise
        /// </summary>
        public IList<string> DataTiersWherePresent { get; private set; }

        /// <summary>
        /// Full description of the property.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="element">
        /// The <see cref="IAspectEngine"/> that this property is associated 
        /// with.
        /// </param>
        /// <param name="name">
        /// The name of the property. Must match the string key used to
        /// store the property value in the <see cref="IAspectData"/> instance.
        /// </param>
        /// <param name="type">
        /// The type of the property values.
        /// </param>
        /// <param name="category">
        /// The category the property belongs to.
        /// </param>
        /// <param name="dataTiersWherePresent">
        /// A list of the data tiers that can be used to determine values 
        /// for this property.
        /// </param>
        /// <param name="available">
        /// True if the property is available in the results for the
        /// associated <see cref="IAspectEngine"/>, false otherwise.
        /// </param>
        /// <param name="description">
        /// Full description of the property.
        /// </param>
        /// <param name="itemProperties">
        /// The meta-data for properties that are stored in sub-items.
        /// Only relevant if this meta-data instance relates to a 
        /// collection of complex objects.
        /// </param>
        /// <param name="delayExecution">
        /// Only relevant if `Type` is <see cref="JavaScript"/>.
        /// Defaults to false.
        /// If set to true then the JavaScript in this property will
        /// not be executed automatically on the client device.
        /// </param>
        /// <param name="evidenceProperties">
        /// The names of any <see cref="JavaScript"/> properties that,
        /// when executed, will obtain additional evidence that can help
        /// in determining the value of this property.
        /// Note that these names should include any parts after the 
        /// element data key.
        /// I.e. if the complete property name is 
        /// 'devices.profiles.screenwidthpixelsjavascript' then the
        /// name in this list must be 'profiles.screenwidthpixelsjavascript'
        /// </param>
        public AspectPropertyMetaData(
            IAspectEngine element,
            string name,
            Type type,
            string category,
            IList<string> dataTiersWherePresent,
            bool available,
            string description = "",
            IReadOnlyList<IElementPropertyMetaData> itemProperties = null,
            bool delayExecution = false,
            IReadOnlyList<string> evidenceProperties = null) : 
            base(element, name, type, available, category, itemProperties, delayExecution, evidenceProperties)
        {
            DataTiersWherePresent = dataTiersWherePresent;
            Description = description;
        }
    }
}
