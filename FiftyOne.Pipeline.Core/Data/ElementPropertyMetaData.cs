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

using FiftyOne.Pipeline.Core.Data.Types;
using FiftyOne.Pipeline.Core.FlowElements;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.Pipeline.Core.Data
{
    /// <summary>
    /// Meta-data relating to properties that are populated by Flow Elements.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/properties.md#property-metadata">Specification</see>
    /// </summary>
    public class ElementPropertyMetaData : IElementPropertyMetaData
    {
        /// <inheritdoc/>
        public string Name { get; private set; }

        /// <inheritdoc/>
        public IFlowElement Element { get; private set; }

        /// <inheritdoc/>
        public string Category { get; private set; }

        /// <inheritdoc/>
        public Type Type { get; private set; }

        /// <inheritdoc/>
        public bool Available { get; private set; }

        /// <inheritdoc/>
        public IReadOnlyList<IElementPropertyMetaData> ItemProperties { get; }


        private Lazy<IReadOnlyDictionary<string, IElementPropertyMetaData>> _itemPropertyDictionary;
        /// <inheritdoc/>
        public IReadOnlyDictionary<string, IElementPropertyMetaData> ItemPropertyDictionary
        {
            get
            {
                return _itemPropertyDictionary.Value;
            }
        }

        /// <inheritdoc/>
        public bool DelayExecution { get; private set; }

        /// <inheritdoc/>
        public IReadOnlyList<string> EvidenceProperties { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="element">
        /// The <see cref="IFlowElement"/> that this property is associated 
        /// with.
        /// </param>
        /// <param name="name">
        /// The name of the property. Must match the string key used to
        /// store the property value in the <see cref="IElementData"/> instance.
        /// </param>
        /// <param name="type">
        /// The type of the property values.
        /// </param>
        /// <param name="category">
        /// The category the property belongs to.
        /// </param>
        /// <param name="available">
        /// True if the property is available.
        /// </param>
        /// <param name="itemProperties">
        /// Where this meta-data instance relates to a list of complex objects, 
        /// this parameter can contain a list of the property meta-data 
        /// for items in that list.
        /// </param>
        /// <param name="delayExecution">
        /// Only relevant if <see cref="Type"/> is <see cref="JavaScript"/>.
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
        public ElementPropertyMetaData(
            IFlowElement element,
            string name,
            Type type,
            bool available,
            string category = "",
            IReadOnlyList<IElementPropertyMetaData> itemProperties = null,
            bool delayExecution = false,
            IReadOnlyList<string> evidenceProperties = null)
        {
            // WARNING: If you are adding properties to this constructor 
            // that need to be exposed through the cloud service then you'll
            // also need to update:
            // - Pipeline.Core.Data.PropertyMetaData
            // - FiftyOne.Pipeline.CloudRequestEngine.FlowElements.CloudRequestEngineBase.LoadProperty()
            Element = element;
            Name = name;
            Type = type;
            Category = category;
            Available = available;
            ItemProperties = itemProperties;
            DelayExecution = delayExecution;
            EvidenceProperties = evidenceProperties;

            _itemPropertyDictionary = new Lazy<IReadOnlyDictionary<string, IElementPropertyMetaData>>(() =>
            {
                return ItemProperties?.ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);
            });
        }
    }
}
