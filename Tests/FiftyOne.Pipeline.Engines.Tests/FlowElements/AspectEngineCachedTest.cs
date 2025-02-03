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
using FiftyOne.Pipeline.Core.TypedMap;
using FiftyOne.Pipeline.Engines.Caching;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;
using FiftyOne.Common.TestHelpers;
using FiftyOne.Pipeline.Engines.TestHelpers;
using System;
using FiftyOne.Pipeline.Core.FlowElements;

namespace FiftyOne.Pipeline.Engines.Tests.FlowElements
{
    [TestClass]
    public class AspectEngineCachedTest
    {
        private EmptyEngine _engine;

        private TestLoggerFactory _loggerFactory;

        private Mock<IFlowCache> _cache;

        [TestInitialize]
        public void Init()
        {
            _cache = new Mock<IFlowCache>();

            _loggerFactory = new TestLoggerFactory();
            _engine = new EmptyEngineBuilder(_loggerFactory)
                .Build();
            _engine.SetCache(_cache.Object);
            _engine.SetCacheHitOrMiss(true);
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

        /// <summary>
        /// Check that an aspect engine with a configured cache will 
        /// make use of the cache as expected.
        /// In this test, a value is passed to the engine that does not
        /// exist in the cache. The engine should process it as normal
        /// and then add the result to the cache.
        /// </summary>
        [TestMethod]
        public void FlowElementCached_Process_CheckCachePut()
        {
            // Arrange
            var evidence = new Dictionary<string, object>()
            {
                { "user-agent", "1234" }
            };
            Mock<IFlowData> data = MockFlowData.CreateFromEvidence(evidence, false);
            EmptyEngineData aspectData = new EmptyEngineData(
                _loggerFactory.CreateLogger<EmptyEngineData>(),
                data.Object.Pipeline,
                _engine,
                MissingPropertyService.Instance);
            data.Setup(d => d.GetOrAdd(
                It.IsAny<TypedKey<EmptyEngineData>>(),
                It.IsAny<Func<IPipeline, EmptyEngineData>>()))
                .Returns(aspectData);

            _cache.Setup(c => c[It.IsAny<IFlowData>()])
                .Returns<IDictionary<string, object>>(null);

            // Act
            _engine.Process(data.Object);

            // Assert
            // Verify that the cache was checked to see if there was an
            // existing result for this key.
            _cache.Verify(c => c[It.Is<IFlowData>(d => d == data.Object)], Times.Once);
            // Verify that the result was added to the cache.
            _cache.Verify(c => c.Put(It.Is<IFlowData>(d => d == data.Object),
                It.IsAny<EmptyEngineData>()), Times.Once);
            // Verify the CacheHit flag is false.
            Assert.IsFalse(aspectData.CacheHit);
        }

        /// <summary>
        /// Check that an aspect engine with a configured cache will 
        /// make use of the cache as expected.
        /// In this test, a value is passed to the engine that does 
        /// exist in the cache. The engine should simply return the 
        /// cached result.
        /// </summary>
        [TestMethod]
        public void FlowElementCached_Process_CheckCacheGet()
        {
            // Arrange

            var evidence = new Dictionary<string, object>()
            {
                { "user-agent", "1234" }
            };
            var mockData = MockFlowData.CreateFromEvidence(evidence, false);
            IFlowData data = mockData.Object;
            EmptyEngineData cachedData = new EmptyEngineData(
                _loggerFactory.CreateLogger<EmptyEngineData>(),
                data.Pipeline,
                _engine,
                new Mock<IMissingPropertyService>().Object);
            cachedData.ValueOne = 2;
            _cache.Setup(c => c[It.IsAny<IFlowData>()])
                .Returns(cachedData);

            // Act
            _engine.Process(data);

            // Assert
            // Verify that the cached result was added to the flow data.
            mockData.Verify(d => d.GetOrAdd(
                It.IsAny<ITypedKey<EmptyEngineData>>(),
                It.Is<Func<IPipeline, EmptyEngineData>>(f => f(mockData.Object.Pipeline) == cachedData)), Times.Once());
            // Verify that the cache was checked once.
            _cache.Verify(c => c[It.Is<IFlowData>(d => d == data)], Times.Once);
            // Verify that the Put method of the cache was not called.
            _cache.Verify(c => c.Put(It.IsAny<IFlowData>(), It.IsAny<IElementData>()), Times.Never);
            // Verify the CacheHit flag is true.
            Assert.IsTrue(cachedData.CacheHit);
        }
    }
}
