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

using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Tests.FlowElements
{
    [TestClass]
    public class ShareUsageBuilderTests
    {
        /// <summary>
        /// An implementation of ShareUsageBuilderBase that we can
        /// use for testing.
        /// </summary>
        private class TestBuilder : ShareUsageBuilderBase<ShareUsageElement>
        {
            public TestBuilder() : base(new LoggerFactory())
            {
            }

            public override ShareUsageElement Build()
            {
                throw new NotImplementedException();
            }

            public int GetMaximumQueueSize()
            {
                return MaximumQueueSize;
            }
        }


        TestBuilder _builder = new TestBuilder();

        /// <summary>
        /// Verify that the calculation of the MaximumQueueSize 
        /// setting is correct.
        /// Also verify that specifying the MaximumQueueSize value
        /// will override the calculation.
        /// </summary>
        /// <param name="minEntries"></param>
        /// <param name="maxQueueSize"></param>
        /// <param name="expectedMaxQueueSize"></param>
        [DataTestMethod]
        [DataRow(null, null, 1000)]
        [DataRow(10, null, 1000)]
        [DataRow(100, null, 1000)]
        [DataRow(200, null, 2000)]
        [DataRow(2500, null, 25000)]
        [DataRow(null, 100, 100)]
        [DataRow(10, 100, 100)]
        [DataRow(100, 100, 100)]
        [DataRow(200, 100, 100)]
        [DataRow(2500, 100, 100)]
        [DataRow(null, 10000, 10000)]
        [DataRow(10, 10000, 10000)]
        [DataRow(100, 10000, 10000)]
        [DataRow(200, 10000, 10000)]
        [DataRow(2500, 10000, 10000)]
        public void VerifyMaximumQueueSizeCalculation(
            int? minEntries,
            int? maxQueueSize, 
            int expectedMaxQueueSize)
        {
            if (minEntries.HasValue)
            {
                _builder.SetMinimumEntriesPerMessage(minEntries.Value);
            }
            if (maxQueueSize.HasValue)
            {
                _builder.SetMaximumQueueSize(maxQueueSize.Value);
            }

            Assert.AreEqual(expectedMaxQueueSize, _builder.GetMaximumQueueSize());
        }

    }

}
