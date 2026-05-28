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

using FiftyOne.Pipeline.Core.Configuration;
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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FiftyOne.Pipeline.Web.Tests
{
    [TestClass]
    public class ExtensionTests
    {
        /// <summary>
        /// Generates test cases for all AddFiftyOne overloads combined with configuration variants.
        /// </summary>
        public static IEnumerable<object[]> AddFiftyOneTestCases =>
            from overload in new (string Name, bool UsesDefaultBuilder, Action<IServiceCollection, IConfiguration> Add)[]
            {
                ("Default", true, (s, c) => s.AddFiftyOne(c)),
                ("WithFactory", false, (s, c) => s.AddFiftyOne(c, (config, builder) => 
                    builder.BuildFromConfiguration(config.GetSection("PipelineOptions").Get<PipelineOptions>()))),
                ("WithServiceProviderFactory", false, (s, c) => s.AddFiftyOne(c, (sp, config, builder) => 
                {
                    Assert.IsNotNull(sp, "IServiceProvider should be passed to factory");
                    return builder.BuildFromConfiguration(config.GetSection("PipelineOptions").Get<PipelineOptions>());
                }))
            }
            from shareUsage in new[] { true, false }
            from clientSide in new[] { true, false }
            select new object[] { overload.Name, overload.UsesDefaultBuilder, shareUsage, clientSide, overload.Add };

        /// <summary>
        /// Generates descriptive test display names.
        /// </summary>
        public static string GetTestDisplayName(MethodInfo _, object[] data) =>
            $"TestAddFiftyOne ({data[0]}, ShareUsage={data[2]}, ClientSide={data[3]})";

        /// <summary>
        /// Verify that the 'AddFiftyOne' extension method overloads are adding the expected elements
        /// to the pipeline.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(AddFiftyOneTestCases), DynamicDataDisplayName = nameof(GetTestDisplayName))]
        public void TestAddFiftyOne(
            string overloadName,
            bool usesDefaultBuilder,
            bool shareUsageEnabled,
            bool clientSideEvidenceEnabled,
            Action<IServiceCollection, IConfiguration> addFiftyOne)
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
            
            var configuration = host.Services.GetRequiredService<IConfiguration>();
            addFiftyOne(services, configuration);

            var provider = services.BuildServiceProvider();
            // Get the pipeline that's been created.
            var pipeline = provider.GetRequiredService<IPipeline>();

            // Verify that the expected elements are present in the pipeline.
            // ShareUsage and SequenceElement are added by the builder based on config.
            Assert.AreEqual(shareUsageEnabled, pipeline.FlowElements
                .Any(e => e.ElementDataKey == ShareUsageBase.DEFAULT_ELEMENT_DATA_KEY));
            Assert.IsTrue(pipeline.FlowElements
                .Any(e => e.ElementDataKey == SequenceElement.DEFAULT_ELEMENT_DATA_KEY));
            
            // JavaScriptBuilder, JsonBuilder, and SetHeaders are only added by the default
            // FiftyOnePipelineBuilder, not when using a custom factory.
            if (usesDefaultBuilder)
            {
                Assert.AreEqual(clientSideEvidenceEnabled, pipeline.FlowElements
                    .Any(e => e.ElementDataKey == JavaScriptBuilderElement.DEFAULT_ELEMENT_DATA_KEY));
                Assert.AreEqual(clientSideEvidenceEnabled, pipeline.FlowElements
                    .Any(e => e.ElementDataKey == JsonBuilderElement.DEFAULT_ELEMENT_DATA_KEY));
                Assert.IsTrue(pipeline.FlowElements
                    .Any(e => e.ElementDataKey == SetHeadersElement.DEFAULT_ELEMENT_DATA_KEY));
            }
        }
    }
}
