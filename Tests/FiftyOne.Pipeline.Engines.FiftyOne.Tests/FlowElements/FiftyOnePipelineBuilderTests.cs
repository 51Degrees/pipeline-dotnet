/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2023 51 Degrees Mobile Experts Limited, Davidson House,
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

using FiftyOne.Pipeline.Core.Configuration;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Tests.FlowElements
{
    [TestClass]
    public class FiftyOnePipelineBuilderTests
    {
        /// <summary>
        /// Verify that the pipeline-level 'ShareUsage' option will cause the share usage element
        /// to be added or not added based on the value passed to it.
        /// </summary>
        /// <param name="shareUsageEnabled"></param>
        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void TestShareUsageOption(bool shareUsageEnabled)
        {
            var pipeline = new FiftyOnePipelineBuilder()
                .SetShareUsage(shareUsageEnabled)
                .Build();

            Assert.AreEqual(shareUsageEnabled, pipeline.FlowElements
                .Any(e => e.ElementDataKey == ShareUsageBase.DEFAULT_ELEMENT_DATA_KEY));
        }

        /// <summary>
        /// Verify that the pipeline-level 'ShareUsage' option will be respected when building
        /// from configuration.
        /// </summary>
        /// <param name="shareUsageEnabled"></param>
        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void TestShareUsageOption_BuildFromConfig(bool shareUsageEnabled)
        {
            var options = new PipelineOptions();
            options.BuildParameters.Add("ShareUsage", shareUsageEnabled);

            var pipeline = new FiftyOnePipelineBuilder()
                .BuildFromConfiguration(options);

            Assert.AreEqual(shareUsageEnabled, pipeline.FlowElements
                .Any(e => e.ElementDataKey == ShareUsageBase.DEFAULT_ELEMENT_DATA_KEY));
        }

        /// <summary>
        /// Verify that if a share usage element is specified in configuration, it will be added 
        /// regardless of the pipeline level 'ShareUsage' setting.
        /// </summary>
        /// <param name="shareUsageEnabled"></param>
        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void TestShareUsageConfigOverridesOption(bool shareUsageEnabled)
        {
            var options = new PipelineOptions();
            options.BuildParameters.Add("ShareUsage", shareUsageEnabled);
            options.Elements.Add(new ElementOptions() { BuilderName = "ShareUsage" });

            using (var serviceProvider = new ServiceCollection()
                // Add the configuration to the services collection.
                .AddSingleton(options)
                // Make sure we're logging to the console.
                .AddLogging(l => l.AddConsole())
                // Add an HttpClient instance. This is used for making requests to the
                // cloud service.
                .AddSingleton<HttpClient>()
                .AddSingleton<ShareUsageBuilder>()
                .BuildServiceProvider())
            {
                var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

                var pipeline = new FiftyOnePipelineBuilder(loggerFactory, serviceProvider)
                    .BuildFromConfiguration(options);

                Assert.IsTrue(pipeline.FlowElements
                    .Any(e => e.ElementDataKey == ShareUsageBase.DEFAULT_ELEMENT_DATA_KEY));
            }
        }

    }
}
