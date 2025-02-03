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

using FiftyOne.Pipeline.Core.Attributes;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.Http;

namespace FiftyOne.Pipeline.Engines.FiftyOne.FlowElements
{
    /// <summary>
    /// Pipeline builder class that allows the 51Degrees share usage 
    /// element to be enabled/disabled.
    /// </summary>
    public class FiftyOnePipelineBuilder : PipelineBuilder
    {
        private bool _shareUsageEnabled = Constants.SHARE_USAGE_DEFAULT_ENABLED;

        /// <summary>
        /// Default constructor
        /// </summary>
        public FiftyOnePipelineBuilder()
#pragma warning disable CA2000 // Dispose objects before losing scope
            : base(new LoggerFactory())
#pragma warning restore CA2000 // Dispose objects before losing scope
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="loggerFactory"></param>
        public FiftyOnePipelineBuilder(
            ILoggerFactory loggerFactory) 
            : base(loggerFactory)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="loggerFactory"></param>
        /// <param name="services"></param>
        public FiftyOnePipelineBuilder(
            ILoggerFactory loggerFactory, 
            IServiceProvider services) 
            : base(loggerFactory, services)
        {
        }

        /// <summary>
        /// Set share usage enabled/disabled.
        /// Defaults to enabled.
        /// </summary>
        /// <param name="enabled">
        /// true to enable usage sharing. False to disable.
        /// </param>
        /// <returns>
        /// This builder instance.
        /// </returns>
        [DefaultValue(Constants.SHARE_USAGE_DEFAULT_ENABLED)]
        public FiftyOnePipelineBuilder SetShareUsage(bool enabled)
        {
            _shareUsageEnabled = enabled;
            return this;
        }

        /// <summary>
        /// Called just before the pipeline is built.
        /// </summary>
        protected override void OnPreBuild()
        {
            // Add the sequence element if it doe not exist already, make sure 
            // it is added at the beginning as some engines depend on it.
            if (FlowElements.Any(e => e.GetType() == typeof(SequenceElement)) == false)
            {
#pragma warning disable CA2000 // Dispose objects before losing scope
                // Element lifetimes are managed by the Pipeline
                FlowElements.Insert(0, new SequenceElementBuilder(LoggerFactory).Build());
#pragma warning restore CA2000 // Dispose objects before losing scope
            }
            // If share usage is enabled then add the share usage element if it
            // does not exist in the list.
            if (_shareUsageEnabled &&
                FlowElements.Any(e => e.GetType() == typeof(ShareUsageElement)) == false)
            {
                FlowElements.Add(new ShareUsageBuilder(LoggerFactory, new HttpClient()).Build());
            }  
        }

    }
}
