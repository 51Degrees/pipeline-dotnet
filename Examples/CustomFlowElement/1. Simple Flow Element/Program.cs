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

using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging;
using Examples.CustomFlowElement.Data;
using Examples.CustomFlowElement.FlowElements;
using System;

namespace Examples.CustomFlowElement
{
    public class Program
    {
        private static ILoggerFactory _loggerFactory = new LoggerFactory();

        public void RunExample()
        {
            //! [usage]
            // Construct the engine.
            var starSignElement = new SimpleFlowElementBuilder(_loggerFactory)
                .Build();
            // Construct the pipeline with the example engine.
            var pipeline = new PipelineBuilder(_loggerFactory)
                .AddFlowElement(starSignElement)
                .Build();

            var dob = new DateTime(1992, 12, 18);
            // Create a new flow data.
            using (var flowData = pipeline.CreateFlowData())
            {
                // Add the evidence and process the data.
                flowData
                    .AddEvidence("date-of-birth", dob)
                    .Process();
                // Now get the result of the processing.
                Console.WriteLine($"With a date of birth of " +
                    $"{dob.ToString("dd/MM/yyy")}" +
                    $", your star sign is " +
                    $"{flowData.Get<IStarSignData>().StarSign}.");
            }
            //! [usage]
        }

        static void Main(string[] args)
        {
            var instance = new Program();
            instance.RunExample();

            Console.WriteLine("==========================================");
            Console.WriteLine("Example complete. Press any key to exit.");
            // Wait for user to press a key.
            Console.ReadKey();
        }

    }
}
