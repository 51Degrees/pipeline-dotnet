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
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Core.Configuration
{
    /// <summary>
    /// Configuration object that describes how to build a 
    /// <see cref="Pipeline"/> using a <see cref="PipelineBuilder"/>
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/pipeline-configuration.md">Specification</see>
    /// </summary>
    public class PipelineOptions
    {
        /// <summary>
        ///  Default constructor.
        /// </summary>
        public PipelineOptions()
        {
            Elements = new List<ElementOptions>();
            BuildParameters = new Dictionary<string, object>();
        }

        /// <summary>
        /// Configuration information for the <see cref="IFlowElement"/>s 
        /// that the Pipeline will contain.
        /// </summary>
        /// <remarks>
        /// The order of elements is important as the pipeline will execute
        /// them sequentially in the order they are supplied.
        /// To execute elements in parallel, the 
        /// <see cref="ElementOptions.SubElements"/> property should be used.
        /// </remarks>
        public IList<ElementOptions> Elements { get; internal set; }

        /// <summary>
        /// A dictionary where the keys are method names and the values
        /// are parameter values.
        /// The method names can be exact matches, 'set' + name or match
        /// an AlternateNameAttribute.
        /// </summary>
        [Obsolete("Please use 'BuildParameters' instead.")]
        public IDictionary<string, object> PipelineBuilderParameters
        {
            get
            {
                return BuildParameters;
            }
            internal set
            {
                BuildParameters = PipelineBuilderParameters;
            }
        }

        /// <summary>
        /// A dictionary where the keys are method names and the values
        /// are parameter values.
        /// The method names can be exact matches, 'set' + name or match
        /// an AlternateNameAttribute.
        /// </summary>
        public IDictionary<string, object> BuildParameters { get; internal set; }
    }
}
