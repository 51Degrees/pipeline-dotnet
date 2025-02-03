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
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging;

namespace FiftyOne.Pipeline.CloudRequestEngine.Data
{
    /// <summary>
    /// The <see cref="IElementData"/> class for 
    /// <see cref="FlowElements.CloudRequestEngine"/>.
    /// </summary>
    public class CloudRequestData : AspectDataBase
    {
        private const string JSON_RESPONSE_KEY = "json-response";
        private const string PROCESS_STARTED_KEY = "process-started";

        /// <summary>
        /// The raw JSON response returned by the 51Degrees cloud service. 
        /// </summary>
        public string JsonResponse
        {
            get { return base[JSON_RESPONSE_KEY]?.ToString(); }
            set { base[JSON_RESPONSE_KEY] = value; }
        }

        /// <summary>
        /// Flag to confirm that the CloudRequestEngine has started processing.
        /// </summary>
        public bool? ProcessStarted
        {
            get { return base[PROCESS_STARTED_KEY] as bool?; }
            set { base[PROCESS_STARTED_KEY] = value; }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">
        /// The logger for this instance
        /// </param>
        /// <param name="pipeline">
        /// The pipeline this instance was created by.
        /// </param>
        /// <param name="engine">
        /// The engine that created this instance.
        /// </param>
        public CloudRequestData(
            ILogger<AspectDataBase> logger,
            IPipeline pipeline,
            IAspectEngine engine) : base(logger, pipeline, engine)
        {
        }
    }
}
