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

using Examples.DerivedProperty.Data;
using Examples.DerivedProperty.FlowElements;
using FiftyOne.Pipeline.Core.Configuration;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.DerivedProperty.Data;
using FiftyOne.Pipeline.DerivedProperty.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

/// <summary>
/// @example DerivedProperty/Program.cs
///
/// The derived property element computes new properties from properties
/// that other elements in the pipeline have already produced, following a
/// script written in YAML or in JSON. The element holds no data file,
/// sends no request over the network and needs no resource key, so this
/// example runs offline.
///
/// The example shows the two ways a script reaches the element.
///
/// 1. A JSON configuration file naming the ScriptFiles build parameter,
///    with the Scripts build parameter alongside it for the scripts that
///    ship inside the package.
/// 2. The fluent builder in code, adding one script from a file and one
///    held as text.
///
/// The source properties come from a small element written inside this
/// example, so that nothing outside is needed. A real pipeline puts
/// device detection or IP intelligence in that position instead.
///
/// This example is available in full on [GitHub](https://github.com/51Degrees/pipeline-dotnet/blob/master/Examples/DerivedProperty/Program.cs).
///
/// The script the example runs is
/// @include DerivedProperty/ReaderEngagement.yaml
///
/// and the configuration file is
/// @include DerivedProperty/derived-property-options.json
/// </summary>
namespace Examples.DerivedProperty
{
    public class Program
    {
        /// <summary>
        /// The script file the project copies to the output directory. Both
        /// halves of the example use the file, one by naming it in
        /// configuration and one by naming it on the builder.
        /// </summary>
        private const string ScriptFile = "ReaderEngagement.yaml";

        /// <summary>
        /// The property the script in ScriptFile writes.
        /// </summary>
        private const string EngagementProperty = "ReaderEngagement";

        /// <summary>
        /// The property the script held below writes.
        /// </summary>
        private const string EveryTestProperty = "ReaderEngagementIsHigh";

        /// <summary>
        /// A second script, held as text rather than in a file, to show
        /// that a script can come straight from your own code. The text is
        /// YAML here, and JSON is read just as well.
        ///
        /// The script reads the same three source properties and publishes
        /// whether every one of the tests passed, so an element can run
        /// more than one script over one set of source properties.
        /// </summary>
        private const string EveryTestScript = @"
Format: 1
Name: ReaderEngagementIsHigh
Version: ""1.0.0""
Output:
  Name: ReaderEngagementIsHigh
  Description: Whether every one of the ReaderEngagement tests passed on this request.
  ValueType: bool
  IsList: false
  Category: Example
Checks:
  SeveralPages: { Property: session.PagesViewed, Ge: 3 }
  LongEnough:   { Property: session.SecondsSincePageLoad, Ge: 30 }
  PointerMoved: { Property: session.PointerMoved, Eq: true }
Rules:
  - When: { Failed: Checks, Eq: 0 }
    Then: true
  - Else: false
";

        public static void Main(string[] args)
        {
            var instance = new Program();
            instance.RunExample();

            Console.WriteLine("==========================================");
            Console.WriteLine("Example complete.");
            // Only wait for a key where there is a keyboard to press one
            // on, so the example also runs to completion from a script.
            if (Console.IsInputRedirected == false)
            {
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// Run the example.
        /// </summary>
        public void RunExample()
        {
            // A script file path, whether it comes from the configuration
            // file or from the builder, is read relative to the working
            // directory. The example moves the working directory to its
            // own output folder, where the project copies the script and
            // the configuration file, so the example runs the same
            // whichever folder it was started from.
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);

            RunFromConfiguration();
            RunFromCode();
            WriteFindOutMore();
        }

        /// <summary>
        /// Print where to find out more, so that anyone this example is
        /// shown to can follow up from the output itself.
        /// </summary>
        private static void WriteFindOutMore()
        {
            Console.WriteLine("==========================================");
            Console.WriteLine("Find out more");
            Console.WriteLine(
                "  The scripts, the format reference and a page that runs " +
                "a script in your browser:");
            Console.WriteLine(
                "    https://github.com/51Degrees/derived-properties");
            Console.WriteLine(
                "    https://51degrees.github.io/derived-properties/");
            Console.WriteLine(
                "  This repository, where the derived property element " +
                "lives:");
            Console.WriteLine(
                "    https://github.com/51Degrees/pipeline-dotnet");
            Console.WriteLine(
                "  The device detection engine, which supplies most of " +
                "the properties a script reads:");
            Console.WriteLine(
                "    https://github.com/51Degrees/device-detection-dotnet");
            Console.WriteLine(
                "  The IP intelligence engine, which supplies the rest:");
            Console.WriteLine(
                "    https://github.com/51Degrees/ip-intelligence-dotnet");
            Console.WriteLine("  The 51Degrees documentation:");
            Console.WriteLine(
                "    https://51degrees.com/documentation" +
                "?utm_source=code&utm_medium=example" +
                "&utm_campaign=pipeline-dotnet" +
                "&utm_content=examples-derivedproperty-program.cs" +
                "&utm_term=find-out-more");
            Console.WriteLine(
                "  More about 51Degrees and how to get in touch:");
            Console.WriteLine(
                "    https://51degrees.com" +
                "?utm_source=code&utm_medium=example" +
                "&utm_campaign=pipeline-dotnet" +
                "&utm_content=examples-derivedproperty-program.cs" +
                "&utm_term=about-51degrees");
        }

        /// <summary>
        /// The first way a customer configures the element, being a JSON
        /// configuration file. The file names the builder and the script
        /// files, so the pipeline is described entirely outside the code.
        /// </summary>
        private void RunFromConfiguration()
        {
            Console.WriteLine("==========================================");
            Console.WriteLine(
                "1. Configured from derived-property-options.json");
            Console.WriteLine();

            var config = new ConfigurationBuilder()
                .AddJsonFile("derived-property-options.json")
                .Build();
            var options = new PipelineOptions();
            config.Bind("PipelineOptions", options);

            // The pipeline builder makes each element by finding the
            // builder named in the configuration file, so both builders
            // are registered below.
            IServiceProvider serviceProvider = new ServiceCollection()
                .AddSingleton<ILoggerFactory>(new LoggerFactory())
                .AddSingleton<SessionElementBuilder>()
                .AddSingleton<DerivedPropertyElementBuilder>()
                .BuildServiceProvider();
            var factory =
                serviceProvider.GetRequiredService<ILoggerFactory>();

            using (var pipeline =
                new PipelineBuilder(factory, serviceProvider)
                    .BuildFromConfiguration(options))
            {
                var properties = new List<string>() { EngagementProperty };

                Process(
                    pipeline,
                    "Request 1, every source property present.",
                    5,
                    120,
                    true,
                    properties);

                Process(
                    pipeline,
                    "Request 2, one source property absent.",
                    5,
                    120,
                    null,
                    properties);

                Console.WriteLine(
                    "The second request has no value at all, because a " +
                    "property the script");
                Console.WriteLine(
                    "names was not there. The message says which property " +
                    "was missing and");
                Console.WriteLine(
                    "what the element that supplies it said, which is more " +
                    "use than a");
                Console.WriteLine(
                    "confidence band resting on part of the evidence.");
                Console.WriteLine();

                PrintDefinitions(
                    pipeline.GetElement<DerivedPropertyElement>());
            }
        }

        /// <summary>
        /// The second way a customer configures the element, being the
        /// fluent builder in code. One element runs both scripts, one read
        /// from a file and one held as text.
        /// </summary>
        private void RunFromCode()
        {
            Console.WriteLine("==========================================");
            Console.WriteLine("2. Configured with the builder in code");
            Console.WriteLine();

            var loggerFactory = new LoggerFactory();

            var source = new SessionElementBuilder(loggerFactory).Build();

            var derived = new DerivedPropertyElementBuilder(loggerFactory)
                // A script from your own environment. A path may hold a
                // wildcard, so "scripts/*.yaml" adds a whole folder.
                .AddScriptFile(ScriptFile)
                // A script held as text in your own code. The name given
                // here must equal the Name the script itself carries.
                .AddScript(EveryTestProperty, EveryTestScript)
                // AddScript(BuiltInScript.Something) adds a script that
                // ships inside the package. The scripts 51Degrees ships
                // come from the derived-properties repository, and this
                // example uses none of them so that it stands on its own.
                .Build();

            using (var pipeline = new PipelineBuilder(loggerFactory)
                // The derived property element must come after every
                // element that supplies a source property, and the build
                // fails naming the property where one comes later.
                .AddFlowElement(source)
                .AddFlowElement(derived)
                .Build())
            {
                var properties = new List<string>()
                {
                    EngagementProperty,
                    EveryTestProperty
                };

                Process(
                    pipeline,
                    "Request 1, every source property present.",
                    5,
                    120,
                    true,
                    properties);

                Process(
                    pipeline,
                    "Request 2, one source property absent.",
                    5,
                    120,
                    null,
                    properties);

                Console.WriteLine(
                    "One element runs both scripts, so both properties are " +
                    "written under");
                Console.WriteLine(
                    "the key derived, and both say the same thing about " +
                    "the second request");
                Console.WriteLine(
                    "because they read the same source properties.");
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Process one request and print the source properties and the
        /// derived properties it produced.
        /// </summary>
        /// <param name="pipeline">The pipeline to process with.</param>
        /// <param name="title">What the request is showing.</param>
        /// <param name="pagesViewed">Evidence for session.PagesViewed.</param>
        /// <param name="seconds">
        /// Evidence for session.SecondsSincePageLoad.
        /// </param>
        /// <param name="pointerMoved">
        /// Evidence for session.PointerMoved, or null to leave the property
        /// absent on this request.
        /// </param>
        /// <param name="derivedProperties">
        /// The derived properties to read back and print.
        /// </param>
        private static void Process(
            IPipeline pipeline,
            string title,
            int pagesViewed,
            int seconds,
            bool? pointerMoved,
            IReadOnlyList<string> derivedProperties)
        {
            Console.WriteLine(title);
            using (var data = pipeline.CreateFlowData())
            {
                data.AddEvidence(
                    SessionElement.PagesViewedEvidenceKey, pagesViewed);
                data.AddEvidence(
                    SessionElement.SecondsEvidenceKey, seconds);
                if (pointerMoved.HasValue)
                {
                    data.AddEvidence(
                        SessionElement.PointerMovedEvidenceKey,
                        pointerMoved.Value);
                }

                data.Process();

                // The source properties, read the way any element's
                // properties are read.
                var session = data.Get<ISessionData>();
                Print("session.PagesViewed", Text(session.PagesViewed));
                Print(
                    "session.SecondsSincePageLoad",
                    Text(session.SecondsSincePageLoad));
                Print("session.PointerMoved", Text(session.PointerMoved));

                // The derived properties, read out of the element data the
                // derived property element writes under the key derived.
                var derived = data.Get<IDerivedPropertyData>();
                foreach (var property in derivedProperties)
                {
                    Print("derived." + property, Read(derived, property));
                }
            }
            Console.WriteLine();
        }

        /// <summary>
        /// Print the full property definition each script carries, read
        /// off the element rather than written out here, so that everything
        /// a script says about its property is available to a caller.
        /// </summary>
        /// <param name="element">The element to read.</param>
        private static void PrintDefinitions(IDerivedPropertyElement element)
        {
            Console.WriteLine(
                "The property metadata the element publishes to the " +
                "pipeline.");
            foreach (var property in element.Properties)
            {
                Console.WriteLine(
                    "  {0}, type {1}, category {2}, available {3}",
                    property.Name,
                    property.Type.Name,
                    property.Category,
                    property.Available);
            }
            Console.WriteLine();

            foreach (var script in element.Scripts)
            {
                var output = script.Output;
                Console.WriteLine(
                    "The full definition the script '{0}' carries.",
                    script.Name);
                Print("Name", output.Name);
                Print("Description", output.Description);
                Print("ValueType", output.ValueType.ToString());
                Print("DefaultValue", output.DefaultValue);
                Print("Category", output.Category);
                Print("IsList", Text<bool>(output.IsList));
                Print("IsMandatory", Text(output.IsMandatory));
                Print("IsObsolete", Text(output.IsObsolete));
                Print("IsPopular", Text(output.IsPopular));
                Print("ExportValues", Text(output.ExportValues));
                if (output.Dependencies != null)
                {
                    Print(
                        "Dependencies",
                        string.Join(", ", output.Dependencies));
                }
                if (output.Values != null)
                {
                    Console.WriteLine("  Values");
                    foreach (var value in output.Values)
                    {
                        Console.WriteLine(
                            "    {0,-10} {1}",
                            value.Name,
                            value.Description);
                    }
                }
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Read one derived property and say in words what came back,
        /// because a derived property may hold a value or may hold the
        /// reason there is none.
        /// </summary>
        /// <param name="derived">The derived element data.</param>
        /// <param name="name">The property to read.</param>
        /// <returns>The value, or the reason there is none.</returns>
        private static string Read(IDerivedPropertyData derived, string name)
        {
            if (derived.TryGet(name, out var raw) == false)
            {
                return "not present";
            }
            var value = raw as IAspectPropertyValue;
            if (value == null)
            {
                return Convert.ToString(raw, CultureInfo.InvariantCulture);
            }
            return value.HasValue
                ? Convert.ToString(value.Value, CultureInfo.InvariantCulture)
                : "no value, because " + value.NoValueMessage;
        }

        private static void Print(string name, string value)
        {
            Console.WriteLine("  {0,-33} {1}", name, value);
        }

        /// <summary>
        /// The text form of a value that may not be there, so that an
        /// absent source property reads as words rather than as a blank.
        /// </summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="value">The value, or null.</param>
        /// <returns>The value as text, or why there is none.</returns>
        private static string Text<T>(T? value) where T : struct
        {
            return value.HasValue
                ? Convert.ToString(
                    value.Value, CultureInfo.InvariantCulture)
                : "not supplied on this request";
        }
    }
}
