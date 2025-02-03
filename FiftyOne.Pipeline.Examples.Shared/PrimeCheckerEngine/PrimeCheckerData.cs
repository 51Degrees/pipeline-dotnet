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
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Examples.Shared
{
    /// <summary>
    /// The data associated with the <see cref="PrimeCheckerEngine"/>
    /// </summary>
    public class PrimeCheckerData : AspectDataBase, IPrimeCheckerData
    {
        /// <summary>
        /// True if the value supplied to the engine is prime. False otherwise.
        /// </summary>
        public bool? IsPrime { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">
        /// Used for logging.
        /// </param>
        /// <param name="pipeline">
        /// The <see cref="IPipeline"/> this instance will be attached to.
        /// </param>
        /// <param name="engine">
        /// The <see cref="IAspectEngine"/> that created this instance.
        /// </param>
        public PrimeCheckerData(
            ILogger<AspectDataBase> logger, 
            IPipeline pipeline, 
            IAspectEngine engine) : 
            base(logger, pipeline, engine)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">
        /// Used for logging.
        /// </param>
        /// <param name="pipeline">
        /// The <see cref="IPipeline"/> this instance will be attached to.
        /// </param>
        /// <param name="engine">
        /// The <see cref="IAspectEngine"/> that created this instance.
        /// </param>
        /// <param name="missingPropertyService">
        /// The <see cref="IMissingPropertyService"/> to use when a requested
        /// key cannot be found.
        /// </param>
        public PrimeCheckerData(
            ILogger<AspectDataBase> logger, 
            IPipeline pipeline, 
            IAspectEngine engine, 
            IMissingPropertyService missingPropertyService) : 
            base(logger, pipeline, engine, missingPropertyService)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">
        /// Used for logging.
        /// </param>
        /// <param name="pipeline">
        /// The <see cref="IPipeline"/> this instance will be attached to.
        /// </param>
        /// <param name="engine">
        /// The <see cref="IAspectEngine"/> that created this instance.
        /// </param>
        /// <param name="missingPropertyService">
        /// The <see cref="IMissingPropertyService"/> to use when a requested
        /// key cannot be found.
        /// </param>
        /// <param name="dictionary">
        /// The dictionary instance to use internally when storing data values.
        /// </param>
        public PrimeCheckerData(
            ILogger<AspectDataBase> logger, 
            IPipeline pipeline, 
            IAspectEngine engine, 
            IMissingPropertyService missingPropertyService, 
            IDictionary<string, object> dictionary) :
            base(logger, pipeline, engine, missingPropertyService, dictionary)
        {
        }
    }
}
