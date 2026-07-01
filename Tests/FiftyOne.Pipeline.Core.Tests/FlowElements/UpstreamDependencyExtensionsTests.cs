/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2026 51 Degrees Mobile Experts Limited, Davidson House,
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace FiftyOne.Pipeline.Core.Tests.FlowElements
{
    [TestClass]
    public class UpstreamDependencyExtensionsTests
    {
        private static Mock<IFlowElement> MockElement(string dataKey)
        {
            var element = new Mock<IFlowElement>();
            element.SetupGet(e => e.ElementDataKey).Returns(dataKey);
            return element;
        }

        private static IPipeline PipelineWith(params IFlowElement[] elements)
        {
            var pipeline = new Mock<IPipeline>();
            pipeline.SetupGet(p => p.FlowElements).Returns(elements);
            return pipeline.Object;
        }

        private static IFlowData FlowDataFor(IPipeline pipeline)
        {
            var data = new Mock<IFlowData>();
            data.SetupGet(d => d.Pipeline).Returns(pipeline);
            return data.Object;
        }

        private static IFlowElement Consumer(string dataKey, params string[] deps)
        {
            var mock = MockElement(dataKey);
            mock.As<IDeclaresUpstreamDependencies>()
                .SetupGet(c => c.RequiredUpstreamProperties)
                .Returns(deps);
            return mock.Object;
        }

        [TestMethod]
        public void ProvidedBy_ResolvesByElementDataKey()
        {
            var device = MockElement("device").Object;
            var pipeline = PipelineWith(device);
            Assert.AreSame(device, pipeline.ProvidedBy("device.DeviceId"));
        }

        [TestMethod]
        public void ProvidedBy_UnknownKey_ReturnsNull()
        {
            var device = MockElement("device").Object;
            var pipeline = PipelineWith(device);
            Assert.IsNull(pipeline.ProvidedBy("ip.CountryCodesGeographical"));
        }

        [TestMethod]
        public void IsNeededFor_DirectlyWanted_ReturnsTrue()
        {
            var device = MockElement("device").Object;
            var data = FlowDataFor(PipelineWith(device));
            Assert.IsTrue(data.IsNeededFor(device, e => e == device));
        }

        [TestMethod]
        public void IsNeededFor_WantedConsumerDeclaresIt_ReturnsTrue()
        {
            var device = MockElement("device").Object;
            var consumer = Consumer("fodid", "device.DeviceId");
            var data = FlowDataFor(PipelineWith(consumer, device));
            // device is not directly wanted, only the consumer is.
            Assert.IsTrue(data.IsNeededFor(device, e => e == consumer));
        }

        [TestMethod]
        public void IsNeededFor_ConsumerNotWanted_ReturnsFalse()
        {
            var device = MockElement("device").Object;
            var consumer = Consumer("fodid", "device.DeviceId");
            var data = FlowDataFor(PipelineWith(consumer, device));
            // Nothing is wanted, so the provider is not needed.
            Assert.IsFalse(data.IsNeededFor(device, e => false));
        }

        [TestMethod]
        public void IsNeededFor_NoConsumerNeedsIt_ReturnsFalse()
        {
            var device = MockElement("device").Object;
            var other = MockElement("location").Object;
            var data = FlowDataFor(PipelineWith(other, device));
            Assert.IsFalse(data.IsNeededFor(device, e => e == other));
        }

        [TestMethod]
        public void UnresolvedUpstreamDependencies_FindsMissingElement()
        {
            var device = MockElement("device").Object;
            // The consumer asks for device.DeviceId (present) and
            // ip.CountryCodesGeographical (no "ip" element in the pipeline).
            var consumer = Consumer(
                "fodid", "device.DeviceId", "ip.CountryCodesGeographical");
            var pipeline = PipelineWith(consumer, device);

            var unresolved = pipeline.UnresolvedUpstreamDependencies();

            Assert.HasCount(1, unresolved);
            Assert.AreEqual("ip.CountryCodesGeographical", unresolved[0]);
        }
    }
}
