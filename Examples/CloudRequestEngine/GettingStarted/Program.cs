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

using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Threading;

namespace GettingStarted
{
    class Program
    {
        private static ILoggerFactory _loggerFactory = new LoggerFactory();
        private static HttpClient _httpClient = new HttpClient();

        static void Main(string[] args)
        {
            var ct = new CancellationToken();

            var cfg = new LazyLoadingConfiguration(10000, ct);

            var engine = new CloudRequestEngineBuilder(_loggerFactory, _httpClient)
                // The cloud service to call, read from FOD_CLOUD_API_URL
                // and defaulting to https://cloud.51degrees.com/api/v4/.
                // A host other than cloud.51degrees.com would be used to
                // (a) use an on premise web server, or (b) use a privately
                // hosted version of the 51Degrees cloud for performance
                // reasons. This is the private hosting option of the cloud
                // service. Both run the same service, so this example
                // works unchanged against either.
                .SetEndPoint(GetCloudEndpoint())
                // A resource key with the properties needed by the examples
                // can be created at https://configure.51degrees.com/Wkqxf3Bs?utm_source=code&utm_medium=example&utm_campaign=pipeline-dotnet&utm_content=examples-cloudrequestengine-gettingstarted-program.cs&utm_term=main.
                // The aligned _51DEGREES_RESOURCE_KEY environment variable is
                // checked first, then the legacy RESOURCE_KEY variable.
                .SetResourceKey(GetResourceKey())
                .SetLazyLoading(cfg)
                .Build();


            using (var pipeline = new PipelineBuilder(_loggerFactory).AddFlowElement(engine).Build())
            {
                var data = pipeline.CreateFlowData();
                data.AddEvidence("query.User-Agent", "iPhone");
                data.Process();
                var result = data.GetFromElement(engine).JsonResponse;
                Console.WriteLine(result);
            }

            // Only wait for a key where there is a keyboard to press one
            // on, so the example also runs to completion from a script.
            if (Console.IsInputRedirected == false)
            {
                Console.ReadKey();
            }
        }

        /// <summary>
        /// Get the cloud service to call. The FOD_CLOUD_API_URL environment
        /// variable names the API base including the /api/v4/ segment,
        /// which is the same variable the CloudRequestEngineBuilder honours
        /// when no endpoint is set, and defaults to the public cloud at
        /// https://cloud.51degrees.com/api/v4/. The value is normalised to
        /// end in one slash because the builder appends the endpoint names
        /// to it.
        /// </summary>
        private static string GetCloudEndpoint()
        {
            var endpoint = Environment.GetEnvironmentVariable("FOD_CLOUD_API_URL");
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                endpoint = "https://cloud.51degrees.com/api/v4/";
            }
            return endpoint.TrimEnd('/') + "/";
        }

        /// <summary>
        /// Get the resource key to use for this example. The aligned
        /// _51DEGREES_RESOURCE_KEY environment variable is checked first,
        /// then the legacy RESOURCE_KEY variable. If neither is set then
        /// the hard-coded key below is used, preserving the previous
        /// behaviour of this example.
        /// </summary>
        private static string GetResourceKey()
        {
            var resourceKey =
                Environment.GetEnvironmentVariable("_51DEGREES_RESOURCE_KEY");
            if (string.IsNullOrEmpty(resourceKey))
            {
                resourceKey = Environment.GetEnvironmentVariable("RESOURCE_KEY");
            }
            if (string.IsNullOrEmpty(resourceKey))
            {
                resourceKey = "AQS5HKcyHJbECm6E10g";
            }
            return resourceKey;
        }
    }
}
