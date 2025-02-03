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

using FiftyOne.Common.TestHelpers;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.TestHelpers;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;

namespace FiftyOne.Pipeline.Engines.Tests.FlowElements
{
    [TestClass]
    public class AspectEngineMultiEngineDataTests
    {
        private EmptyEngine _engine;
        private IPipeline _pipeline;

        private TestLoggerFactory _loggerFactory;

        private int _timeoutMS = 1000;
        private CancellationTokenSource _cancellationTokenSource =
            new CancellationTokenSource();

        [TestInitialize]
        public void Init()
        {
            _loggerFactory = new TestLoggerFactory();
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Check that no errors or warnings were logged.
            foreach (var logger in _loggerFactory.Loggers)
            {
                logger.AssertMaxErrors(0);
                logger.AssertMaxWarnings(0);
            }

            _engine.Dispose();
            _pipeline.Dispose();
        }

        private void BuildEngine(bool lazyLoading)
        {
            var builder = new EmptyEngineBuilder(_loggerFactory);
            if (lazyLoading)
            {
                builder.SetLazyLoading(new LazyLoadingConfiguration(_timeoutMS,
                        _cancellationTokenSource.Token));
            }
            _engine = builder.Build();

            _pipeline = new PipelineBuilder(_loggerFactory)
                .AddFlowElement(_engine)
                .Build();
        }

        /// <summary>
        /// Check that a property set before processing an engine will not
        /// be reset by that engine unless it actively makes a change.
        /// ValueOne is set to 1 by the engine but ValueThree should be 
        /// unchanged.
        /// </summary>
        [TestMethod]
        public void MultiEngineData_SimpleTest()
        {
            BuildEngine(false);

            using (IFlowData data = _pipeline.CreateFlowData())
            {
                EmptyEngineData engineData = data.GetOrAdd(
                    _engine.ElementDataKeyTyped,
                    (f) => new EmptyEngineData(
                    _loggerFactory.CreateLogger<EmptyEngineData>(),
                    f,
                    _engine,
                    null));
                engineData.ValueOne = 0;
                engineData.ValueThree = 50;
                data.Process();

                var result = data.Get<EmptyEngineData>();
                Assert.AreEqual(1, result.ValueOne);
                Assert.AreEqual(50, result.ValueThree);
            }
        }

        /// <summary>
        /// Check that a property set before processing a engine with
        /// lazy loading enabled will not be reset by that engine unless 
        /// it actively makes a change.
        /// ValueOne is set to 1 by the engine but ValueThree should be 
        /// unchanged.
        /// </summary>
        [TestMethod]
        public void MultiEngineData_LazyLoadingTest()
        {
            BuildEngine(true);

            using (IFlowData data = _pipeline.CreateFlowData())
            {
                EmptyEngineData engineData = data.GetOrAdd(
                    _engine.ElementDataKeyTyped,
                    (f) => new EmptyEngineData(
                    _loggerFactory.CreateLogger<EmptyEngineData>(),
                    f,
                    _engine,
                    null));
                engineData.ValueThree = 50;

                data.Process();

                var result = data.Get<EmptyEngineData>();
                Assert.AreEqual(1, result.ValueOne);
                Assert.AreEqual(50, result.ValueThree);
            }
        }

    }
}
