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
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;

/// <summary>
/// @example UsageSharing/Program.cs
///
/// @include{doc} example-usage-sharing-intro.txt
/// 
/// Usage sharing is enabled by default if using a pipeline builder 
/// that is derived from FiftyOnePipelineBuilder.
/// For instance, the DeviceDetectionPipelineBuilder.
/// In this example, we show how to add a ShareUsageElement to a 
/// Pipeline using configuration.
/// 
/// As with all ElementBuilders, this can also be handled in code, 
/// using the ShareUsageBuilder. The commented section in the example 
/// demonstrates this.
/// 
/// The appsettings.json file contains all the configuration options.
/// These are all optional, so each can be omitted if the default 
/// for that option is sufficient:
/// 
/// @include UsageSharing/appsettings.json
/// 
/// For further details of what each setting does, see the 
/// [share usage builder reference](https://51degrees.com/pipeline-dotnet/class_fifty_one_1_1_pipeline_1_1_engines_1_1_fifty_one_1_1_flow_elements_1_1_share_usage_builder_base.html) 
///
/// This example is available in full on [GitHub](https://github.com/51Degrees/pipeline-dotnet/blob/master/Examples/UsageSharing/Program.cs).
/// 
/// Expected output
/// ```
/// Constructing pipeline from configuration file.
/// 
/// Pipeline created with share usage element, evidence processed 
/// with this pipeline will now be shared with 51Degrees using the 
/// specified configuration.
/// ```
/// </summary>
namespace Examples.UsageSharing
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var instance = new Program();
            instance.RunExample();

            Console.WriteLine("==========================================");
            Console.WriteLine("Example complete. Press any key to exit.");
            // Wait for user to press a key.
            Console.ReadKey();
        }

        /// <summary>
        /// Run the example
        /// </summary>
        public void RunExample()
        {
            Console.WriteLine($"Constructing pipeline from configuration file.");
            Console.WriteLine();

            // Create the configuration object
            var config = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json")
                    .Build();
            // Bind the configuration to a pipeline options instance
            PipelineOptions options = new PipelineOptions();
            config.Bind("PipelineOptions", options);

            // We need to create an IServiceProvider that will allow
            // the PipelineBuilder to create the element builder instances 
            // it is going to use.
            // In this case, a ShareUsageBuilder.
            // As ShareUsageBuilder requires an ILoggerFactory and
            // an HttpClient instance, we need to add these to the 
            // service provider.
            IServiceProvider serviceProvider = new ServiceCollection()
                .AddSingleton<ILoggerFactory>(new LoggerFactory())
                .AddSingleton<HttpClient>()
                .AddSingleton<ShareUsageBuilder>()
                .BuildServiceProvider();
            var factory = serviceProvider.GetRequiredService<ILoggerFactory>();

            // Create the pipeline using the options object
            var pipeline = new PipelineBuilder(factory, serviceProvider)
                .BuildFromConfiguration(options);

            // Alternatively, the commented code below shows how to
            // configure the ShareUsageElement in code, rather than
            // using a configuration file.
            //var http = new System.Net.Http.HttpClient();
            //var sharingElement = new ShareUsageBuilder(factory, http)
            //    .SetSharePercentage(0.1)
            //    .SetMinimumEntriesPerMessage(2500)
            //    .Build();
            //var pipeline = new PipelineBuilder(factory)
            //    .AddFlowElement(sharingElement);

            Console.WriteLine($"Pipeline created with share usage " +
                $"element, evidence processed with this pipeline will " +
                $"now be periodically shared with 51Degrees using " +
                $"the specified configuration.");
        }
    }
}
