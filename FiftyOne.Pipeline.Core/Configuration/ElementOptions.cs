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

using FiftyOne.Pipeline.Core.FlowElements;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Core.Configuration
{
    /// <summary>
    /// Configuration object that describes how to build an 
    /// <see cref="IFlowElement"/>.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/pipeline-configuration.md">Specification</see>
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", 
        "CA2227:Collection properties should be read only", 
        Justification = "This would be a breaking change. " +
        "The ElementOptions class is just a POCO data store for " +
        "configuration information so having settable collections" +
        "is not a problem.")]
    public class ElementOptions
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ElementOptions()
        {
            SubElements = new List<ElementOptions>();
            BuildParameters = new Dictionary<string, object>();
        }

        /// <summary>
        /// The name of the builder to use when creating the 
        /// <see cref="IFlowElement"/>.
        /// </summary>
        /// <remarks>
        /// This does not necessarily have to be the full name of the type.
        /// The system will match on:
        /// 1. Exact match of type name
        /// 2. Convention based match by removing 'Builder' from the end
        /// of the type name. e.g. a BuilderName value of 
        /// 'DeviceDetectionEngine' would match to 'DeviceDetectionEngineBuilder'
        /// 3. Matching on an AlternateNameAttribute. e.g. a BuilderName value
        /// of 'DDEngine' would match to 'DeviceDetectionEngineBuilder' if that
        /// class also had [AlternateNameAttribute(Name = "DDEngine")]
        /// </remarks>
        public string BuilderName { get; set; }


        /// <summary>
        /// The dictionary keys are method names or names of parameters on
        /// the Build method of the builder.
        /// The value is the parameter value.
        /// </summary>
        /// <remarks>
        /// Similar to the BuilderName, the key value does not necessarily 
        /// have to be the full name of the method. The system will match on:
        /// 1. Exact match of method name
        /// 2. Convention based match by removing 'Set' from the start
        /// of the method name. e.g. a key value of 
        /// 'AutomaticUpdatesEnabled' would match to method 
        /// 'SetAutomaticUpdatesEnabled'
        /// 3. Matching on an AlternateNameAttribute. e.g. a key value
        /// of 'AutoUpdates' would match to 'SetAutoUpdateEnabled' if that
        /// class also had [AlternateNameAttribute(Name = "AutoUpdates")]
        /// </remarks>
        public IDictionary<string, object> BuildParameters { get; set; }

        /// <summary>
        /// If this property is populated, the flow element is a 
        /// <see cref="ParallelElements"/> instance. 
        /// <see cref="BuilderName"/> and <see cref="BuildParameters"/> 
        /// should be ignored.
        /// Each options instance within <see cref="SubElements"/> contains
        /// the configuration for an element to be added to a 
        /// <see cref="ParallelElements"/> instance.
        /// </summary>
        /// <remarks>
        /// A <see cref="ParallelElements"/> always executes all it's children
        /// in parallel so the ordering of this elements is irrelevant.
        /// </remarks>
        public IList<ElementOptions> SubElements { get; set; }
    }
}
