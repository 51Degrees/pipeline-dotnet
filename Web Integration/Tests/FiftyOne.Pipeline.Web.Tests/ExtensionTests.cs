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

using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.Engines.TestHelpers;
using FiftyOne.Pipeline.JavaScriptBuilder.FlowElement;
using FiftyOne.Pipeline.JsonBuilder.FlowElement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.Pipeline.Web.Tests
{
    [TestClass]
    public class ExtensionTests
    {
        /// <summary>
        /// Verify that the 'AddFiftyOne' extension method is adding the expected elements
        /// to the pipeline.
        /// </summary>
        /// <param name="shareUsageEnabled"></param>
        /// <param name="clientSideEvidenceEnabled"></param>
        [DataTestMethod]
        [DataRow(true, true)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(false, false)]
        public void TestAddFiftyOne(bool shareUsageEnabled, bool clientSideEvidenceEnabled)
        {
            // Create configuration overrides.
            var testConfig = new Dictionary<string, string>();
            testConfig.Add("PipelineOptions:BuildParameters:ShareUsage",
                shareUsageEnabled.ToString());
            testConfig.Add("PipelineOptions:Elements:0:BuilderName",
                "EmptyEngine");
            testConfig.Add("PipelineWebIntegrationOptions:ClientSideEvidenceEnabled",
                clientSideEvidenceEnabled.ToString());

            // Create a dummy host using our configuration overrides
            var host = new HostBuilder()
                .ConfigureAppConfiguration((context, builder) =>
                {
                    builder.AddInMemoryCollection(testConfig);
                })
                // Log to console so we can see what happens in the event of a failure.
                .ConfigureLogging(l =>
                {
                    l.ClearProviders().AddConsole();
                })
                .Build();

            // Create the ServiceCollection and call the 'AddFiftyOne' extension method.
            var services = new ServiceCollection();
            services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
            services.AddSingleton<EmptyEngineBuilder>();
            services.AddFiftyOne(host.Services.GetRequiredService<IConfiguration>());

            var provider = services.BuildServiceProvider();
            // Get the pipeline that's been created.
            var pipeline = provider.GetRequiredService<IPipeline>();

            // Verify that the expected elements are present in the pipeline.
            Assert.AreEqual(shareUsageEnabled, pipeline.FlowElements
                .Any(e => e.ElementDataKey == ShareUsageBase.DEFAULT_ELEMENT_DATA_KEY));
            Assert.IsTrue(pipeline.FlowElements
                .Any(e => e.ElementDataKey == SequenceElement.DEFAULT_ELEMENT_DATA_KEY));
            Assert.AreEqual(clientSideEvidenceEnabled, pipeline.FlowElements
                .Any(e => e.ElementDataKey == JavaScriptBuilderElement.DEFAULT_ELEMENT_DATA_KEY));
            Assert.AreEqual(clientSideEvidenceEnabled, pipeline.FlowElements
                .Any(e => e.ElementDataKey == JsonBuilderElement.DEFAULT_ELEMENT_DATA_KEY));
            Assert.IsTrue(pipeline.FlowElements
                .Any(e => e.ElementDataKey == SetHeadersElement.DEFAULT_ELEMENT_DATA_KEY));
        }
    }
}
