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
using FiftyOne.Pipeline.DerivedProperty.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace FiftyOne.Pipeline.DerivedProperty.Tests;

/// <summary>
/// Proves the compiled model is safe to share between threads, and
/// reports how long one request takes.
/// </summary>
[TestClass]
public class ConcurrencyAndPerformanceTests
{
    /// <summary>
    /// Set by MSTest, and used to report the benchmark numbers.
    /// </summary>
    public TestContext TestContext { get; set; }

    /// <summary>
    /// The script the concurrency test and the fallback benchmark run. It
    /// is the shape of HumanConfidence, being several named checks, an
    /// aggregate over them and a run of rules, so the test exercises every
    /// part of the evaluator rather than one comparison.
    /// </summary>
    private const string ScriptText =
        "Format: 1\n" +
        "Name: Confidence\n" +
        "Version: 1.0.0\n" +
        "Output:\n" +
        "  Name: Confidence\n" +
        "  Description: The confidence that a human is viewing the page.\n" +
        "  ValueType: string\n" +
        "  DefaultValue: Unknown\n" +
        "  IsList: false\n" +
        "  Category: General\n" +
        "  Values:\n" +
        "    - Name: High\n" +
        "      Description: High confidence.\n" +
        "    - Name: Medium\n" +
        "      Description: Mixed evidence.\n" +
        "    - Name: Low\n" +
        "      Description: Almost certainly no human.\n" +
        "    - Name: Unknown\n" +
        "      Description: Not enough evidence.\n" +
        "Optional:\n" +
        "  - device.IsCrawler\n" +
        "  - device.IsHeadless\n" +
        "  - device.WebDriver\n" +
        "  - device.IsVisible\n" +
        "  - device.BrowserReleaseYear\n" +
        "  - device.BrowserReleaseAge\n" +
        "  - ip.HumanProbability\n" +
        "Checks:\n" +
        "  NotCrawler:  { Property: device.IsCrawler,  Eq: false }\n" +
        "  NotHeadless: { Property: device.IsHeadless, Eq: false }\n" +
        "  NoWebDriver: { Property: device.WebDriver,  Eq: \"None\" }\n" +
        "  Visible:     { Property: device.IsVisible,  Eq: true }\n" +
        "  Current:\n" +
        "    All:\n" +
        "      - { Property: device.BrowserReleaseYear, Gt: 0 }\n" +
        "      - { Property: device.BrowserReleaseAge,  Lt: 2 }\n" +
        "  Human:       { Property: ip.HumanProbability, Ge: 8 }\n" +
        "Rules:\n" +
        "  - When: { Property: device.IsCrawler,  Eq: true }\n" +
        "    Then: Low\n" +
        "  - When: { Property: device.IsHeadless, Eq: true }\n" +
        "    Then: Low\n" +
        "  - When: { Evaluated: Checks, Eq: 0 }\n" +
        "    Then: Unknown\n" +
        "  - When:\n" +
        "      All:\n" +
        "        - { Failed: Checks, Eq: 0 }\n" +
        "        - { Evaluated: Checks, Ge: 4 }\n" +
        "    Then: High\n" +
        "  - When: { Failed: Checks, Le: 1 }\n" +
        "    Then: Medium\n" +
        "  - Else: Low\n";

    private const int ThreadCount = 32;

    private const int IterationsPerThread = 10000;

    /// <summary>
    /// Processes the same request on many threads at once through one
    /// pipeline, and checks every answer against the answer one thread
    /// gives on its own. One pipeline is built and shared, because a
    /// pipeline built for each thread would prove nothing about the
    /// compiled model being safe to share.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public void Concurrency_ManyThreadsGiveTheSingleThreadedAnswer()
    {
        // A logger that records nothing, because the flow element base
        // writes a debug entry for every call when debug logging is on,
        // and holding several hundred thousand of them would measure the
        // logger rather than the element.
        var loggerFactory = NullLoggerFactory.Instance;
        using (var element = new DerivedPropertyElementBuilder(loggerFactory)
            .AddScript("Confidence", ScriptText)
            .Build())
        using (var pipeline = BuildPipeline(loggerFactory, element))
        {
            var expected = Answer(pipeline);
            Assert.AreEqual("High", expected);

            var failures = new ConcurrentBag<string>();
            var completed = 0;
            var threads = new Thread[ThreadCount];
            for (var i = 0; i < ThreadCount; i++)
            {
                threads[i] = new Thread(() =>
                {
                    try
                    {
                        for (var j = 0; j < IterationsPerThread; j++)
                        {
                            var answer = Answer(pipeline);
                            if (string.Equals(
                                answer, expected, StringComparison.Ordinal)
                                == false)
                            {
                                failures.Add(answer ?? "(no value)");
                                return;
                            }
                            Interlocked.Increment(ref completed);
                        }
                    }
                    catch (Exception exception)
                    {
                        failures.Add(exception.ToString());
                    }
                });
                threads[i].IsBackground = true;
            }

            foreach (var thread in threads)
            {
                thread.Start();
            }
            foreach (var thread in threads)
            {
                thread.Join();
            }

            Assert.IsTrue(
                failures.IsEmpty,
                string.Join(Environment.NewLine, failures));
            Assert.AreEqual(ThreadCount * IterationsPerThread, completed);
        }
    }

    /// <summary>
    /// Times a long run of requests through one element and reports
    /// nanoseconds per request with the machine the run happened on.
    ///
    /// Nothing here fails on a timing. A build agent that is busy is not a
    /// defect, and a test that fails on a slow machine would be turned off
    /// rather than read, so the only thing asserted is that the work was
    /// done. The numbers go to the test output for a person to read.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public void Performance_ReportNanosecondsPerRequest()
    {
        const int warmUp = 20000;
        const int iterations = 200000;

        var path = FindHumanConfidenceScript();
        var text = path == null ? ScriptText : File.ReadAllText(path);
        var name = path == null
            ? "Confidence"
            : Path.GetFileNameWithoutExtension(path);
        var source = path ?? "the inline script of the same shape";

        // As above, a logger that records nothing, so the timing is of
        // the element rather than of the log.
        var loggerFactory = NullLoggerFactory.Instance;
        using (var element = new DerivedPropertyElementBuilder(loggerFactory)
            .AddScript(name, text)
            .Build())
        using (var pipeline = BuildPipeline(loggerFactory, element))
        using (var data = pipeline.CreateFlowData())
        {
            // One pass through the pipeline fills in the source element
            // data, and the loop below then times the derived property
            // element on its own.
            data.Process();

            for (var i = 0; i < warmUp; i++)
            {
                element.Process(data);
            }

            var watch = Stopwatch.StartNew();
            for (var i = 0; i < iterations; i++)
            {
                element.Process(data);
            }
            watch.Stop();

            var perRequest =
                watch.Elapsed.TotalMilliseconds * 1000000.0 / iterations;

            TestContext.WriteLine(
                "Derived property element benchmark");
            TestContext.WriteLine(
                "  Script            {0}", element.Scripts[0].Name);
            TestContext.WriteLine(
                "  Script source     {0}", source);
            TestContext.WriteLine(
                "  Warm up           {0} requests",
                warmUp.ToString("N0", CultureInfo.InvariantCulture));
            TestContext.WriteLine(
                "  Timed             {0} requests",
                iterations.ToString("N0", CultureInfo.InvariantCulture));
            TestContext.WriteLine(
                "  Elapsed           {0} ms",
                watch.Elapsed.TotalMilliseconds.ToString(
                    "N2", CultureInfo.InvariantCulture));
            TestContext.WriteLine(
                "  Per request       {0} ns",
                perRequest.ToString("N1", CultureInfo.InvariantCulture));
            // The configuration is reported because dotnet test builds
            // Debug unless told otherwise, and a Debug number read as a
            // Release one would be wrong by a wide margin.
#if DEBUG
            TestContext.WriteLine("  Build             Debug");
#else
            TestContext.WriteLine("  Build             Release");
#endif
            TestContext.WriteLine(
                "  Processors        {0}", Environment.ProcessorCount);
            TestContext.WriteLine(
                "  Operating system  {0}", RuntimeInformation.OSDescription);
            TestContext.WriteLine(
                "  Framework         {0}",
                RuntimeInformation.FrameworkDescription);

            // The only assertion is that the run happened and produced a
            // value, never how long it took.
            Assert.IsGreaterThan(0, watch.ElapsedTicks);
            var derived = data.Get(
                DerivedPropertyElement.DerivedElementDataKey);
            var value = (IAspectPropertyValue)
                derived[element.Scripts[0].Output.Name];
            Assert.IsTrue(value.HasValue, value.NoValueMessage);
        }
    }

    // -----------------------------------------------------------------
    // Helpers.
    // -----------------------------------------------------------------

    /// <summary>
    /// A pipeline holding the source elements the script reads and the
    /// derived property element under test.
    /// </summary>
    private static IPipeline BuildPipeline(
        ILoggerFactory loggerFactory,
        DerivedPropertyElement element)
    {
        return new PipelineBuilder(loggerFactory)
            .AddFlowElement(Source(loggerFactory, "device",
                new Dictionary<string, object>
                {
                    { "IsCrawler", false },
                    { "WebDriver", "None" },
                    { "IsVisible", true },
                    { "BrowserReleaseYear", 2025 },
                    { "BrowserReleaseAge", 1 }
                }))
            .AddFlowElement(Source(loggerFactory, "ip",
                new Dictionary<string, object>
                {
                    { "HumanProbability", 9 }
                }))
            .AddFlowElement(element)
            .Build();
    }

    private static StubSourceElement Source(
        ILoggerFactory loggerFactory,
        string elementDataKey,
        IReadOnlyDictionary<string, object> values)
    {
        return new StubSourceElement(
            loggerFactory.CreateLogger<
                FlowElementBase<StubSourceData, ElementPropertyMetaData>>(),
            elementDataKey,
            values);
    }

    /// <summary>
    /// Processes one request and gives back the answer, or null where
    /// there is no value.
    /// </summary>
    private static string Answer(IPipeline pipeline)
    {
        using (var data = pipeline.CreateFlowData())
        {
            data.Process();
            var derived = data.Get(
                DerivedPropertyElement.DerivedElementDataKey);
            var value = (IAspectPropertyValue)derived["Confidence"];
            return value.HasValue ? (string)value.Value : null;
        }
    }

    /// <summary>
    /// The HumanConfidence script in the derived-properties submodule,
    /// found by walking up from the test assembly, or null where the
    /// submodule is not checked out on this machine.
    /// </summary>
    private static string FindHumanConfidenceScript()
    {
        var relative = Path.Combine(
            "FiftyOne.Pipeline.Elements",
            "FiftyOne.Pipeline.DerivedProperty",
            "Scripts",
            "scripts",
            "HumanConfidence.yaml");
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder != null)
        {
            var candidate = Path.Combine(folder.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            folder = folder.Parent;
        }
        return null;
    }
}
