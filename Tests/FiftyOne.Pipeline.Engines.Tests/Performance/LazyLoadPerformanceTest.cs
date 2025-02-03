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
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.TestHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FiftyOne.Pipeline.Engines.Tests.Performance
{
    [TestClass]
    public class LazyLoadPerformanceTest
    {
        private EmptyEngine _engine;

        private TestLoggerFactory _loggerFactory;

        private int _timeoutMS = 10000;

        [TestInitialize]
        public void Init()
        {
            _loggerFactory = new TestLoggerFactory();
            _engine = new EmptyEngineBuilder(_loggerFactory)
                .SetLazyLoading(new LazyLoadingConfiguration(_timeoutMS, new System.Threading.CancellationToken()))
                .Build();
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
        }

        // This test doesn't work on the Azure build agent.
        /// <summary>
        /// Check that 10000 process requests to a lazy loading engine 
        /// will not cause performance problems (in terms of the 
        /// overhead of the generated tasks.)
        /// </summary>
        //[TestMethod]
        //public void LazyLoad_Performance()
        //{
        //    // Arrange
        //    int iterations = 10000;
        //    // Set the process time to 1 ms
        //    _engine.SetProcessCost(TimeSpan.TicksPerMillisecond * 1);

        //    // Act
        //    var evidence = new Dictionary<string, object>()
        //    {
        //        { "user-agent", "1234" }
        //    };
        //    var mockData = MockFlowData.CreateFromEvidence(evidence, false);
        //    // Each call to the process method will end up adding it's
        //    // engine data for that request to this list using the mocked
        //    // 'Add' method below.
        //    List<EmptyEngineData> engineData = new List<EmptyEngineData>();
        //    mockData.Setup(d => d.GetOrAdd(It.IsAny<ITypedKey<EmptyEngineData>>(),
        //        It.IsAny<Func<IFlowData, EmptyEngineData>>()))
        //        .Callback((ITypedKey<EmptyEngineData> k, Func<IFlowData, EmptyEngineData> f) =>
        //        {
        //            engineData.Add(f(mockData.Object));
        //        })
        //        .Returns((ITypedKey<EmptyEngineData> k, Func<IFlowData, EmptyEngineData> f) =>
        //        {
        //            return engineData.Last();
        //        });
        //    var data = mockData.Object;
            
        //    List<double> processTimes = new List<double>();
        //    DateTime start = DateTime.Now;
        //    for (int i = 0; i < iterations; i++)
        //    {
        //        // Call the process method 10000 times. 
        //        // We can use the same flow data object over and over because
        //        // it's not a real one, just a mock.
        //        start = DateTime.Now;
        //        _engine.Process(data);
        //        // Record the time taken to return from the 
        //        // process method call
        //        processTimes.Add(DateTime.Now.Subtract(start).TotalMilliseconds);
        //    }

        //    // Assert
        //    // Check that we actually have 1 engine data object for 
        //    // each call to process.
        //    Assert.AreEqual(iterations, engineData.Count);
        //    // Check that the last process call populates a result in 
        //    // good time. (The timeout of 10,000 ms will cause this to 
        //    // fail with an exception if it is too slow)
        //    Assert.AreEqual(1, engineData.Last().ValueOne);
        //    // Check that the calls to the process method returned pretty
        //    // much immediately every time.
        //    Assert.IsTrue(processTimes.Average() < 10, 
        //        $"Average process time expected to be < 10 ms. " +
        //        $"Min={processTimes.Min()}, Max={processTimes.Max()}, " +
        //        $"Avg={processTimes.Average()}");
        //}
    }
}
