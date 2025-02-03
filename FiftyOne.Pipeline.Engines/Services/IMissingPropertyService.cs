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

using FiftyOne.Pipeline.Engines.FlowElements;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Engines.Services
{
    /// <summary>
    /// Service that determines the reason for a property not being populated 
    /// by an engine.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/properties.md#missing-properties">Specification</see>
    /// </summary>
    public interface IMissingPropertyService
    {
        /// <summary>
        /// Get the reason that the specified property is not available
        /// in the results from the specified engine.
        /// </summary>
        /// <param name="propertyName">
        /// The property name to check.
        /// </param>
        /// <param name="engine">
        /// The engine that was expected to populate the property.
        /// </param>
        /// <returns>
        /// A <see cref="MissingPropertyResult"/> instance explaining 
        /// why the property is not populated.
        /// </returns>
        MissingPropertyResult GetMissingPropertyReason(string propertyName, IAspectEngine engine);
        /// <summary>
        /// Get the reason that the specified property is not available
        /// in the results from the specified engines.
        /// </summary>
        /// <param name="propertyName">
        /// The property name to check.
        /// </param>
        /// <param name="engines">
        /// The engines that were expected to populate the property.
        /// </param>
        /// <returns>
        /// A <see cref="MissingPropertyResult"/> instance explaining 
        /// why the property is not populated.
        /// </returns>
        MissingPropertyResult GetMissingPropertyReason(string propertyName, IReadOnlyList<IAspectEngine> engines);
    }

    /// <summary>
    /// Details of why a property is not populated by an engine.
    /// </summary>
    public class MissingPropertyResult
    {
        /// <summary>
        /// The reason the property was not populated.
        /// </summary>
        public MissingPropertyReason Reason { get; set; }
        /// <summary>
        /// A text description of the reason the property was not populated.
        /// </summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// The reason a property is not populated by an engine.
    /// </summary>
    public enum MissingPropertyReason
    {
        /// <summary>
        /// The property was not populated because the data file being 
        /// used does not contain that property.
        /// For example, the free, lite 51Degrees device detection data file
        /// has only a handful of properties of the 250+ that are available
        /// in the paid-for versions.
        /// </summary>
        DataFileUpgradeRequired,
        /// <summary>
        /// The property was not included in the list of properties that
        /// were passed in when the engine was created.
        /// Restricting the list of properties that you want an engine to
        /// populate can result in improved performance. However, if you
        /// then request a property that is not in that list then it 
        /// will not be populated.
        /// </summary>
        PropertyExcludedFromEngineConfiguration,
        /// <summary>
        /// 51Degrees cloud engines use a 'resourceKey' to determine
        /// the properties that should be returned in the response.
        /// This reason indicates that the resource key probably does
        /// not include the requested property.
        /// A new resource key will need to be created that does 
        /// include the property before you will be able to access it.
        /// </summary>
        PropertyNotAccessibleWithResourceKey,
        /// <summary>
        /// 51Degrees cloud engines use a 'resourceKey' to determine
        /// the properties that should be returned in the response.
        /// This reason indicates that the resource key does
        /// not include any properties for the requested engine.
        /// A new resource key will need to be created that does 
        /// include the property before you will be able to access it.
        /// </summary>
        ProductNotAccessibleWithResourceKey,
        //CloudEngine,
        /// <summary>
        /// The reason for the property not being present could not 
        /// be determined.
        /// </summary>
        Unknown
    }
}
