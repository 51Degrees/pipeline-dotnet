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

using FiftyOne.Caching;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Caching;
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.TestHelpers;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Engines.Tests.Performance
{
    [TestClass]
    /// <summary>
    /// These tests use a pipeline with an engine that does nothing to check 
    /// various aspects of the performance overhead of the pipeline as a whole.
    /// </summary>
    public class PipelineOverheadTests
    {
        private IPipeline _pipeline;
        private EmptyEngine _engine;

        [TestInitialize]
        public void Initialise()
        {
            var loggerFactory = new LoggerFactory();
            var builder = new PipelineBuilder(loggerFactory);
            _engine = new EmptyEngineBuilder(loggerFactory)
                .Build() as EmptyEngine;

            _pipeline = builder.AddFlowElement(_engine)
                .Build();
        }

        private Timer CreateTimer(TimerCallback callback, object state, TimeSpan interval)
        {
            return new Timer(callback, state, interval, TimeSpan.FromMilliseconds(-1));
        }

        /// <summary>
        /// Check that the overhead of the pipeline is not too high.
        /// Note that this test is only intended to catch gross performance
        /// problems. In general, the overhead of the pipeline should be
        /// well below this threshold.
        /// </summary>
        [TestMethod]
        public void PipelineOverhead_NoCache()
        {
            int iterations = 10000;
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            for (int i = 0; i < iterations; i++)
            {
                using (var flowData = _pipeline.CreateFlowData())
                {
                    flowData.Process();
                }
            }
            stopwatch.Stop();

            double msOverheadPerCall =
                stopwatch.ElapsedMilliseconds / (double)iterations;
            Assert.IsTrue(msOverheadPerCall < 0.1,
                $"Pipeline overhead per Process call was " +
                $"{msOverheadPerCall}ms. Maximum permitted is 0.1ms");
        }

        /// <summary>
        /// Check that the overhead of the pipeline is not too high.
        /// Note that this test is only intended to catch gross performance
        /// problems. In general, the overhead of the pipeline should be
        /// well below this threshold.
        /// </summary>
        [TestMethod]
        public void PipelineOverhead_Cache()
        {
            _engine.SetCache(new DefaultFlowCache(new CacheConfiguration()
            {
                Builder = new LruPutCacheBuilder(),
                Size = 100
            }));
            // Set process cost to 0.2 ms. Therefore the test cannot be passed 
            // unless the cache is mitigating this cost as it should do.
            _engine.SetProcessCost(2000);

            int iterations = 10000;
            var start = DateTime.UtcNow;
            for (int i = 0; i < iterations; i++)
            {
                using (var flowData = _pipeline.CreateFlowData()) { 
                    flowData.AddEvidence("test.value", 10)
                        .Process();
                }
            }
            var end = DateTime.UtcNow;

            double msOverheadPerCall =
                end.Subtract(start).TotalMilliseconds / (double)iterations;
            Assert.IsTrue(msOverheadPerCall < 0.1,
                $"Pipeline overhead per Process call was " +
                $"{msOverheadPerCall}ms. Maximum permitted is 0.1ms");
        }

        /// <summary>
        /// Check that the overhead of the pipeline is not too high when
        /// running in multi-threaded environment.
        /// Note that this test is only intended to catch gross performance
        /// problems. In general, the overhead of the pipeline should be
        /// well below this threshold.
        /// </summary>
        [TestMethod]
        public void PipelineOverhead_Concurrency()
        {
            int iterations = 10000;
            int threads = Environment.ProcessorCount;
            List<Task<TimeSpan>> tasks = new List<Task<TimeSpan>>();

            // Create the threads.
            // Each will create a FlowData instance and process it.
            for (int i = 0; i < threads; i++)
            {
                tasks.Add(new Task<TimeSpan>(() =>
                {
                    var start = DateTime.UtcNow;
                    for (int j = 0; j < iterations; j++)
                    {
                        using (var flowData = _pipeline.CreateFlowData()) {
                            flowData.Process();
                        }
                    }
                    return DateTime.UtcNow.Subtract(start);
                }));
            }
            // Start all tasks together
            foreach (var task in tasks)
            {
                task.Start();
            }
            // Wait for tasks to complete.
            Task.WaitAll(tasks.ToArray());

            // Calculate the time per call from the task results.
            List<double> results = tasks
                .Select(t => t.Result.TotalMilliseconds / (double)iterations).ToList();
            Assert.IsTrue(results.All(r => r < 0.1),
                    $"Pipeline overhead per Process call was too high for " +
                    $"{results.Count(r => r > 0.1)} out of {threads} threads." +
                    $" Maximum permitted is 0.1ms. Actual results: " +
                    $"{string.Join(",", results.OrderByDescending(r => r))}");
        }


    }
}
