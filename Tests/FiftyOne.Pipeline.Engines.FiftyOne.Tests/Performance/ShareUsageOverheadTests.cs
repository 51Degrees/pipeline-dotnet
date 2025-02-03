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
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Net.Http;
using FiftyOne.Common.TestHelpers;
using FiftyOne.Pipeline.Engines.TestHelpers;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Tests.Performance
{
    [TestClass]
    public class ShareUsageOverheadTests
    {
        private IPipeline _pipeline;
        private IAspectEngine _engine;

        [TestInitialize]
        public void Initialise()
        {
            var loggerFactory = new TestLoggerFactory();
            var builder = new PipelineBuilder(loggerFactory);
            var httpClient = new HttpClient();

            var dataUpdateService = new DataUpdateService(
                loggerFactory.CreateLogger<DataUpdateService>(),
                httpClient);
            _engine = new EmptyEngineBuilder(loggerFactory)
                .Build();

            var shareUsage = new ShareUsageBuilder(loggerFactory, httpClient)
                .Build();

            _pipeline = builder
                .AddFlowElement(_engine)
                .AddFlowElement(shareUsage)
                .Build();
        }

        /// <summary>
        /// Check that the overhead of the share usage element is not too high.
        /// Note that this test is only intended to catch gross performance
        /// problems. In general, the overhead should be well below this 
        /// threshold.
        /// </summary>
        [TestMethod]
        public void ShareUsageOverhead_SingleEvidence()
        {
            int iterations = 10000;
            List<IFlowData> data = new List<IFlowData>();
            for (int i = 0; i < iterations; i++)
            {
                using (var flowData = _pipeline.CreateFlowData())
                {
                    flowData.AddEvidence(@"header.user-agent", @"Mozilla/5.0 (iPad; U; CPU OS 3_2_1 like Mac OS X; en-us) AppleWebKit/531.21.10 (KHTML, like Gecko) Mobile/7B405");
                    data.Add(flowData);
                }
            }
            var start = DateTime.UtcNow;
            foreach (var entry in data)
            {
                entry.Process();
            }
            var end = DateTime.UtcNow;

            double msOverheadPerCall =
                end.Subtract(start).TotalMilliseconds / iterations;
            Assert.IsTrue(msOverheadPerCall < 0.1,
                $"Pipeline with share usage overhead per Process call was " +
                $"{msOverheadPerCall}ms. Maximum permitted is 0.1ms");
        }

        /// <summary>
        /// Check that the overhead of the share usage element is not too high.
        /// Note that this test is only intended to catch gross performance
        /// problems. In general, the overhead should be well below this 
        /// threshold.
        /// </summary>
        [TestMethod]
        public void ShareUsageOverhead_ThousandEvidence()
        {
            int iterations = 1000;
            List<IFlowData> data = new List<IFlowData>();
            for (int i = 0; i < iterations; i++)
            {
                using (var flowData = _pipeline.CreateFlowData())
                {
                    for (int j = 0; j < 1000; j++)
                    {
                        flowData.AddEvidence("header." + j.ToString(), j);
                    }
                    flowData.AddEvidence(@"header.user-agent", @"Mozilla/5.0 (iPad; U; CPU OS 3_2_1 like Mac OS X; en-us) AppleWebKit/531.21.10 (KHTML, like Gecko) Mobile/7B405");
                    data.Add(flowData);
                }
            }
            var start = DateTime.UtcNow;
            foreach (var entry in data)
            {
                entry.Process();
            }
            var end = DateTime.UtcNow;

            double msOverheadPerCall =
                end.Subtract(start).TotalMilliseconds / iterations;
            Assert.IsTrue(msOverheadPerCall < 10,
                $"Pipeline with share usage overhead per Process call was " +
                $"{msOverheadPerCall}ms. Maximum permitted is 10ms");
        }

    }
}
