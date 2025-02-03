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

using FiftyOne.Pipeline.Web.Framework.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.JavaScriptBuilder.FlowElement;
using FiftyOne.Pipeline.JsonBuilder.FlowElement;
using FiftyOne.Pipeline.Math;

namespace FiftyOne.Pipeline.Web.Framework.Tests
{

    /// <summary>
    /// Tests used to verify that the dependencies are available and don't
    /// cause loading errors.
    /// </summary>
    [TestClass]
    public class Dependencies
    {
        /// <summary>
        /// Test configuration used to initialize the web pipeline.
        /// </summary>
        private const string JSON_CONTEXT =
            "{\"PipelineOptions\":{\"Elements\":[{\"BuilderName\": \"MathElement\"}]}}";

        /// <summary>
        /// Verifies that the singleton instance of the pipeline is created 
        /// with the math element present and that results are returned. A 
        /// simple check to ensure all dependencies are available and work.
        /// </summary>
        [TestMethod]
        public void MathExample()
        {
            // This is needed in order from BuildFromConfiguration to be able
            // to find the relevant builder types when using reflection.
            AppDomain.CurrentDomain.Load(
                typeof(MathElementBuilder).Assembly.GetName());
            AppDomain.CurrentDomain.Load(
                typeof(JavaScriptBuilderElement).Assembly.GetName());
            AppDomain.CurrentDomain.Load(
                typeof(JsonBuilderElement).Assembly.GetName());
            AppDomain.CurrentDomain.Load(
                typeof(SequenceElementBuilder).Assembly.GetName());

            // Override the function used to obtain the base directory for
            // configuration files. Avoids the use of the HttpContext which
            // won't be available in this test.
            Extensions.BaseDirectory =
                () => AppDomain.CurrentDomain.BaseDirectory;

            // Delete any other configuration files to ensure the one about
            // to be written is used.
            foreach (var file in Constants.ConfigFileNames.SelectMany(i =>
                Constants.JsonFileExtensions.Concat(
                    Constants.XmlFileExtensions).Select(s => Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        i + "." + s))).Where(i => File.Exists(i)))
            {
                File.Delete(file);
            }

            // Write a test pipeline configuration options.
            File.WriteAllText(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                Constants.ConfigFileNames.First() + "." +
                Constants.JsonFileExtensions.First()),
                JSON_CONTEXT);

            // Create a pipeline and then check the math element.
            var instance = WebPipeline.GetInstance();
            using (var flowData = instance.Pipeline.CreateFlowData())
            {
                flowData.AddEvidence(
                    Math.Constants.EVIDENCE_OPERATION_KEY,
                    "1plus1");
                flowData.Process();
                var result = flowData.Get<IMathData>();
                Assert.AreEqual("1+1", result.Operation);
                Assert.AreEqual(result.Result, 2);
            }
        }
    }
}
