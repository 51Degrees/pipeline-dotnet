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

namespace FiftyOne.Pipeline.Engines.TestHelpers
{
    public class EmptyEngineBuilder : 
        AspectEngineBuilderBase<EmptyEngineBuilder, EmptyEngine>
    {
        private long _processCostTicks;
        private ILoggerFactory _loggerFactory;
        private ILogger<EmptyEngineData> _dataLogger;

        public EmptyEngineBuilder(
            ILoggerFactory loggerFactory) :
            base()
        {
            _loggerFactory = loggerFactory;
            _dataLogger = _loggerFactory.CreateLogger<EmptyEngineData>();
        }

        public EmptyEngine Build()
        {
            return BuildEngine();
        }
        
        private EmptyEngineData CreateAspectData(IPipeline pipeline, FlowElementBase<EmptyEngineData, IAspectPropertyMetaData> engine)
        {
            return new EmptyEngineData(
                _dataLogger,
                pipeline,
                engine as IAspectEngine,
                MissingPropertyService.Instance);
        }

        public EmptyEngineBuilder SetProcessCost(long ticks)
        {
            _processCostTicks = ticks;
            return this;
        }

        protected override EmptyEngine NewEngine(List<string> properties)
        {
            return new EmptyEngine(
                _loggerFactory.CreateLogger<EmptyEngine>(),
                CreateAspectData);
        }

        protected override void ConfigureEngine(EmptyEngine engine)
        {
            engine.SetProcessCost(_processCostTicks);
            base.ConfigureEngine(engine);
        }
    }
}
