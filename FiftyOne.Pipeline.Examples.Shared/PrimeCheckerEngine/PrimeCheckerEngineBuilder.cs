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
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Examples.Shared
{
    /// <summary>
    /// Builder for the <see cref="PrimeCheckerEngine"/>
    /// </summary>
    public class PrimeCheckerEngineBuilder : 
        AspectEngineBuilderBase<PrimeCheckerEngineBuilder, PrimeCheckerEngine>
    {
        ILoggerFactory _loggerFactory;

        /// <summary>
        /// Constructor
        /// </summary>
        public PrimeCheckerEngineBuilder() : this(new LoggerFactory())
        {
        }
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="loggerFactory">
        /// The logger factory to use when creating instances.
        /// </param>
        public PrimeCheckerEngineBuilder(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        /// <summary>
        /// Create the engine with the current configuration.
        /// </summary>
        /// <returns>
        /// A new <see cref="PrimeCheckerEngine"/> instance.
        /// </returns>
        public PrimeCheckerEngine Build()
        {
            return BuildEngine();
        }

        /// <summary>
        /// Create a new instance of the engine.
        /// </summary>
        /// <param name="properties">
        /// The properties the engine should return.
        /// Leave blank to return all properties.
        /// </param>
        /// <returns>
        /// A new <see cref="PrimeCheckerEngine"/> instance.
        /// </returns>
        protected override PrimeCheckerEngine NewEngine(List<string> properties)
        {
            return new PrimeCheckerEngine(
                _loggerFactory.CreateLogger<PrimeCheckerEngine>(),
                CreateElementData);
        }

        /// <summary>
        /// Factory method to create <see cref="PrimeCheckerData"/>
        /// instances.
        /// </summary>
        /// <param name="flowData">
        /// The <see cref="IFlowData"/> this <see cref="PrimeCheckerData"/>
        /// will be attached to.
        /// </param>
        /// <param name="engine">
        /// The <see cref="IAspectEngine"/> generating this 
        /// <see cref="PrimeCheckerData"/>
        /// </param>
        /// <returns>
        /// A new <see cref="PrimeCheckerData"/> instances
        /// </returns>
        private IPrimeCheckerData CreateElementData(IPipeline pipeline,
            FlowElementBase<IPrimeCheckerData, AspectPropertyMetaData> engine)
        {
            return new PrimeCheckerData(
                _loggerFactory.CreateLogger<PrimeCheckerData>(),
                pipeline,
                engine as IAspectEngine);
        }
    }
}
