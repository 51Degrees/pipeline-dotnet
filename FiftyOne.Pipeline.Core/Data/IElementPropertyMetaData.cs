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

namespace FiftyOne.Pipeline.Core.Data
{
    /// <summary>
    /// Represents meta-data relating to a property that is populated 
    /// by a <see cref="IFlowElement"/> instance.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/properties.md#property-metadata">Specification</see>
    /// </summary>
    public interface IElementPropertyMetaData
    { 
        /// <summary>
        /// The name of the property. Must match the string key used to
        /// store the property value in the <see cref="IElementData"/> instance.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The <see cref="IFlowElement"/> that this property is associated 
        /// with.
        /// </summary>
        IFlowElement Element { get; }

        /// <summary>
        /// The category the property belongs to.
        /// </summary>
        string Category { get; }

        /// <summary>
        /// The type of the property values
        /// </summary>
        Type Type { get; }

        /// <summary>
        /// True if the property is available in the results for the
        /// associated <see cref="IFlowElement"/>, false otherwise.
        /// </summary>
        bool Available { get; }

        /// <summary>
        /// This is only relevant where Type is a collection of complex 
        /// objects. 
        /// It contains a list of the property meta-data for the
        /// items in the value for this property.
        /// For example, if this meta-data instance represents a list of 
        /// hardware devices, ItemProperties will contain a list of the 
        /// meta-data for properties available on each hardware device
        /// element within that list.
        /// </summary>
        IReadOnlyList<IElementPropertyMetaData> ItemProperties { get; }

        /// <summary>
        /// Get the <see cref="ItemProperties"/> as a dictionary keyed
        /// on property name.
        /// </summary>
        IReadOnlyDictionary<string, IElementPropertyMetaData> ItemPropertyDictionary { get; }

        /// <summary>
        /// Only relevant if <see cref="Type"/> is <see cref="JavaScript"/>.
        /// Defaults to false.
        /// If set to true then the JavaScript in this property will
        /// not be executed automatically on the client device.
        /// This is used where executing the JavaScript would result in  
        /// undesirable behavior. 
        /// For example, attempting to access the location of the device 
        /// will cause the browser to show a pop-up confirming if the 
        /// user is happy too allow the website access to their location.
        /// In general, we don't want this to happen immediately when a
        /// user enters a website, but when they try to use a feature that
        /// requires location data (e.g. show restaurants near me).
        /// </summary>
        bool DelayExecution { get; }

        /// <summary>
        /// Get the names of any <see cref="JavaScript"/> properties that,
        /// when executed, will obtain additional evidence that can help
        /// in determining the value of this property.
        /// For example, the ScreenPixelsWidthJavascript property will
        /// get the pixel width of the client-device's screen.
        /// This is used to update the ScreenPixelsWidth property.
        /// As such, ScreenPixelsWidth will have ScreenPixelWidthJavascript 
        /// in its list of evidence properties.
        /// </summary>
        IReadOnlyList<string> EvidenceProperties { get; }
    }
}
