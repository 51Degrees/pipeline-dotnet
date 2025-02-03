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
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.TestHelpers;
using FiftyOne.Pipeline.Engines.Trackers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Engines.Tests.Trackers
{
    [TestClass]
    public class TrackerBaseTests
    {
        public class TestTracker : TrackerBase<TestTracker.TrackerCount>
        {
            public class TrackerCount
            {
                public int Count { get; set; }
            }

            private int _limit;

            public TestTracker(
                CacheConfiguration configuration,
                int trackerLimit) :
                base(configuration)
            {
                _limit = trackerLimit;
                _filter = new Mock<IEvidenceKeyFilter>();
                // No need to setup the detail of the filter as the 
                // GenerateKey method will be mocked anyway.
            }

            private Mock<IEvidenceKeyFilter> _filter;
            protected override IEvidenceKeyFilter GetFilter()
            {
                return _filter.Object;
            }

            protected override bool Match(IFlowData data, TrackerCount value)
            {
                value.Count++;
                return value.Count <= _limit;
            }

            protected override TrackerCount NewValue(IFlowData data)
            {
                return new TrackerCount() { Count = 1 };
            }
        }

        /// <summary>
        /// Check that tracker works as expected when two different
        /// objects with the same key are tracked.
        /// The implementation of <see cref="TestTracker"/> means that
        /// the first should return true and the second should return false.
        /// </summary>
        [TestMethod]
        public void TrackerBase_Track()
        {
            TestTracker tracker = new TestTracker(new CacheConfiguration()
            {
                Builder = new LruPutCacheBuilder(),
                Size = 100
            }, 1);

            Dictionary<string, object> keyDictionary = new Dictionary<string, object>()
            {
                { "test.field1", "1.2.3.4" },
                { "test.field2", "abcd" },
            };
            var data1 = MockFlowData.CreateFromEvidence(keyDictionary, true);            
            var data2 = MockFlowData.CreateFromEvidence(keyDictionary, true);

            Assert.IsTrue(tracker.Track(data1.Object));
            Assert.IsFalse(tracker.Track(data2.Object));
        }



    }
}
