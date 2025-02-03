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
using Examples.OnPremiseEngine.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using FiftyOne.Pipeline.Engines;

namespace Examples.OnPremiseEngine.FlowElements
{
    //! [class]
    public class SimpleOnPremiseEngineBuilder : SingleFileAspectEngineBuilderBase<SimpleOnPremiseEngineBuilder, SimpleOnPremiseEngine>
    {
        public SimpleOnPremiseEngineBuilder(
            ILoggerFactory loggerFactory,
            IDataUpdateService dataUpdateService) : base(dataUpdateService)
        {
            _loggerFactory = loggerFactory;
            _dataLogger = _loggerFactory.CreateLogger<StarSignData>();
        }

        private ILoggerFactory _loggerFactory;

        private ILogger<StarSignData> _dataLogger;

        protected override SimpleOnPremiseEngine NewEngine(List<string> properties)
        {
            if (DataFileConfigs.Count != 1)
            {
                throw new Exception(
                    "This builder requires one and only one configured file " +
                    $"but it has {DataFileConfigs.Count}");
            }
            var config = DataFileConfigs.First();

            return new SimpleOnPremiseEngine(
                config.DataFilePath,
                _loggerFactory.CreateLogger<SimpleOnPremiseEngine>(),
                CreateData,
                TempDir);
        }
        public IStarSignData CreateData(
            IPipeline pipeline,
            FlowElementBase<IStarSignData, IAspectPropertyMetaData> aspectEngine)
        {
            return new StarSignData(
                _dataLogger,
                pipeline,
                (SimpleOnPremiseEngine)aspectEngine,
                MissingPropertyService.Instance);
        }

        public override SimpleOnPremiseEngineBuilder SetPerformanceProfile(PerformanceProfiles profile)
        {
            // Lets not implement multiple performance profiles in this example.
            return this;
        }
    }
    //! [class]
}
