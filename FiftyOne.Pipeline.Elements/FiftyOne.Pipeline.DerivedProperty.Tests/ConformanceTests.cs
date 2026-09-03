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
using FiftyOne.Pipeline.DerivedProperty.Data;
using FiftyOne.Pipeline.DerivedProperty.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace FiftyOne.Pipeline.DerivedProperty.Tests;

/// <summary>
/// Runs the shared conformance cases that live in the derived-properties
/// repository, which is checked out here as a git submodule. Every language
/// implementation of the element runs the same cases and must give the same
/// answer, so a case that passes in the JavaScript reference and fails here
/// is a real difference between the two implementations.
///
/// Two kinds of case are run. A value case names the source properties a
/// request carries and the value the script must produce from them. A
/// rejection case holds the text of a script that must not validate,
/// together with the places and the message fragments the faults must
/// carry.
///
/// Nothing here names a script. The runner reads whatever case files the
/// submodule holds, takes the script name from the Script key of each case
/// file, and loads the script of that name, so a script added or renamed in
/// the shared repository needs no change to this file.
///
/// A missing or empty submodule folder passes with a notice rather than
/// failing, because the tests of one language repository must never depend
/// on the content of another repository.
/// </summary>
[TestClass]
public class ConformanceTests
{
    /// <summary>
    /// Set by MSTest so a passing test can still write to the test output.
    /// </summary>
    public TestContext TestContext { get; set; }

    /// <summary>
    /// The folders to walk up from the build output looking for the
    /// submodule, written as parts so no absolute path and no number of
    /// parent steps is fixed here.
    /// </summary>
    private static readonly string[] _submoduleParts =
    {
        "FiftyOne.Pipeline.Elements",
        "FiftyOne.Pipeline.DerivedProperty",
        "Scripts"
    };

    private static readonly ILoggerFactory _loggerFactory =
        NullLoggerFactory.Instance;

    /// <summary>
    /// Every value case must produce the value the shared case file gives.
    /// Every failure is collected and reported together, because one change
    /// to the shared format usually breaks several cases at once and seeing
    /// all of them is far more useful than seeing the first.
    /// </summary>
    [TestMethod]
    public void ValueCasesGiveTheSharedAnswer()
    {
        var report = Run(FindSharedFolder());
        Report(report);
        if (report.ValueFailures.Count > 0)
        {
            Assert.Fail(Join(
                report.ValueFailures.Count,
                "value case",
                report.ValueFailures));
        }
    }

    /// <summary>
    /// Every rejection case must be refused by the validator, with a fault
    /// at each place the case names and a fault mentioning each fragment.
    /// </summary>
    [TestMethod]
    public void RejectionCasesAreRefused()
    {
        var report = Run(FindSharedFolder());
        Report(report);
        if (report.RejectionFailures.Count > 0)
        {
            Assert.Fail(Join(
                report.RejectionFailures.Count,
                "rejection case",
                report.RejectionFailures));
        }
    }

    /// <summary>
    /// The same enumeration pointed at an empty folder reports no cases and
    /// throws nothing, which is what happens on a machine where the
    /// submodule has not been checked out.
    /// </summary>
    [TestMethod]
    public void AnEmptyFolderReportsNoCases()
    {
        var folder = Path.Combine(
            Path.GetTempPath(),
            "derived-conformance-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var report = Run(folder);
            Assert.AreEqual(0, report.CaseFiles);
            Assert.AreEqual(0, report.ValueCases);
            Assert.AreEqual(0, report.RejectionCases);
            Assert.IsEmpty(report.ValueFailures);
            Assert.IsEmpty(report.RejectionFailures);
            Assert.IsNotEmpty(
                report.Notices,
                "an empty folder must say why no cases were run");
            Report(report);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    // -------------------------------------------------------------------
    // Finding the shared folder and writing the counts out.
    // -------------------------------------------------------------------

    /// <summary>
    /// Walks up from the build output looking for the submodule folder.
    /// Null where no folder along the way holds one.
    /// </summary>
    private static string FindSharedFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = directory.FullName;
            foreach (var part in _submoduleParts)
            {
                candidate = Path.Combine(candidate, part);
            }
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            directory = directory.Parent;
        }
        return null;
    }

    private void Report(RunReport report)
    {
        foreach (var notice in report.Notices)
        {
            Write("notice: " + notice);
        }
        Write(string.Format(
            CultureInfo.InvariantCulture,
            "Ran {0} conformance cases from {1} case files, and {2} " +
            "rejection cases",
            report.ValueCases,
            report.CaseFiles,
            report.RejectionCases));
    }

    private void Write(string line)
    {
        Console.WriteLine(line);
        TestContext?.WriteLine(line);
    }

    private static string Join(
        int count,
        string kind,
        IReadOnlyList<string> failures)
    {
        var message = new StringBuilder();
        message.AppendFormat(
            CultureInfo.InvariantCulture,
            "{0} {1}{2} did not match the shared cases.",
            count,
            kind,
            count == 1 ? string.Empty : "s");
        foreach (var failure in failures)
        {
            message.AppendLine();
            message.Append(failure);
        }
        return message.ToString();
    }

    // -------------------------------------------------------------------
    // What one run produced.
    // -------------------------------------------------------------------

    private sealed class RunReport
    {
        public int CaseFiles { get; set; }
        public int ValueCases { get; set; }
        public int RejectionCases { get; set; }
        public List<string> ValueFailures { get; } = new List<string>();
        public List<string> RejectionFailures { get; } = new List<string>();
        public List<string> Notices { get; } = new List<string>();
    }

    /// <summary>
    /// Runs every case the given folder holds. The folder is a parameter so
    /// a test can point the same code at a folder with nothing in it.
    /// </summary>
    private static RunReport Run(string folder)
    {
        var report = new RunReport();
        if (string.IsNullOrEmpty(folder) || Directory.Exists(folder) == false)
        {
            report.Notices.Add(
                "the shared cases folder was not found, so no cases were " +
                "run. Check the submodule out with 'git submodule update " +
                "--init' to run them");
            return report;
        }
        RunValueCases(folder, report);
        RunRejectionCases(folder, report);
        return report;
    }

    private static void RunValueCases(string folder, RunReport report)
    {
        var casesFolder = Path.Combine(folder, "tests");
        var scriptsFolder = Path.Combine(folder, "scripts");
        if (Directory.Exists(casesFolder) == false)
        {
            report.Notices.Add(
                "the shared folder holds no tests folder, so no value " +
                "cases were run");
            return;
        }
        // Ordered so the run is the same on every machine, whatever order
        // the file system lists in.
        var files = Directory
            .GetFiles(casesFolder, "*.cases.yaml")
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
        if (files.Count == 0)
        {
            report.Notices.Add(
                "the shared tests folder holds no case files, so no value " +
                "cases were run");
            return;
        }

        foreach (var file in files)
        {
            report.CaseFiles++;
            RunOneCaseFile(file, scriptsFolder, report);
        }
    }

    private static void RunOneCaseFile(
        string file,
        string scriptsFolder,
        RunReport report)
    {
        var name = Path.GetFileName(file);
        DerivedMapping document;
        try
        {
            document = DerivedScriptParser.Parse(File.ReadAllText(file));
        }
        catch (DerivedScriptParseException exception)
        {
            report.ValueFailures.Add(string.Format(
                CultureInfo.InvariantCulture,
                "{0}: the case file cannot be read: {1}",
                name,
                exception.Message));
            return;
        }

        var scriptName = Text(document.Get("Script"));
        if (scriptName == null)
        {
            report.ValueFailures.Add(string.Format(
                CultureInfo.InvariantCulture,
                "{0}: Script must name the script the cases are for",
                name));
            return;
        }
        var scriptPath = Path.Combine(scriptsFolder, scriptName + ".yaml");
        if (File.Exists(scriptPath) == false)
        {
            report.ValueFailures.Add(string.Format(
                CultureInfo.InvariantCulture,
                "{0}: the case file is for '{1}' and there is no script of " +
                "that name in the shared scripts folder",
                name,
                scriptName));
            return;
        }

        if (!(document.Get("Cases") is DerivedSequence cases) ||
            cases.Items.Count == 0)
        {
            report.ValueFailures.Add(string.Format(
                CultureInfo.InvariantCulture,
                "{0}: Cases must list at least one case",
                name));
            return;
        }

        foreach (var item in cases.Items)
        {
            report.ValueCases++;
            RunOneCase(name, scriptPath, item, report);
        }
    }

    private static void RunOneCase(
        string file,
        string scriptPath,
        DerivedNode node,
        RunReport report)
    {
        if (!(node is DerivedMapping entry))
        {
            report.ValueFailures.Add(string.Format(
                CultureInfo.InvariantCulture,
                "{0}: a case must be a mapping", file));
            return;
        }
        var caseName = Text(entry.Get("Name")) ?? "(unnamed)";
        var prefix = string.Format(
            CultureInfo.InvariantCulture, "{0}: '{1}' ", file, caseName);

        if (!(entry.Get("Expect") is DerivedMapping expect))
        {
            report.ValueFailures.Add(prefix + "has no Expect");
            return;
        }

        Dictionary<string, object> properties;
        try
        {
            properties = ReadProperties(entry.Get("Properties"));
        }
        catch (FormatException exception)
        {
            report.ValueFailures.Add(prefix + exception.Message);
            return;
        }

        IAspectPropertyValue produced;
        try
        {
            produced = Produce(scriptPath, properties);
        }
        catch (Exception exception)
        {
            report.ValueFailures.Add(string.Format(
                CultureInfo.InvariantCulture,
                "{0}raised {1}: {2}",
                prefix,
                exception.GetType().Name,
                exception.Message));
            return;
        }

        Compare(prefix, expect, produced, report);
    }

    private static void Compare(
        string prefix,
        DerivedMapping expect,
        IAspectPropertyValue produced,
        RunReport report)
    {
        if (expect.Has("Value"))
        {
            var wanted = Text(expect.Get("Value"));
            if (produced.HasValue == false)
            {
                report.ValueFailures.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}expected the value '{1}' but there was no value. {2}",
                    prefix,
                    wanted,
                    produced.NoValueMessage));
                return;
            }
            var got = Convert.ToString(
                produced.Value, CultureInfo.InvariantCulture);
            if (string.Equals(got, wanted, StringComparison.Ordinal) == false)
            {
                report.ValueFailures.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}expected '{1}' but got '{2}'",
                    prefix, wanted, got));
            }
            return;
        }

        if (expect.Has("Missing"))
        {
            // The element hands back one value that has no value, and the
            // message names every source property that was not available,
            // so the names are looked for in the message.
            if (produced.HasValue)
            {
                report.ValueFailures.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}expected no value because a source property was " +
                    "not available, but got '{1}'",
                    prefix,
                    Convert.ToString(
                        produced.Value, CultureInfo.InvariantCulture)));
                return;
            }
            var missing = expect.Get("Missing") as DerivedSequence;
            if (missing == null)
            {
                report.ValueFailures.Add(
                    prefix + "Missing must list the properties expected to " +
                    "be absent");
                return;
            }
            foreach (var item in missing.Items)
            {
                var wanted = Text(item);
                if (wanted == null ||
                    produced.NoValueMessage.IndexOf(
                        "'" + wanted + "'",
                        StringComparison.OrdinalIgnoreCase) < 0)
                {
                    report.ValueFailures.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}expected the message to name '{1}' but it said: " +
                        "{2}",
                        prefix, wanted, produced.NoValueMessage));
                }
            }
            return;
        }

        report.ValueFailures.Add(
            prefix + "has an Expect that is not Value or Missing");
    }

    // -------------------------------------------------------------------
    // Building a real pipeline for one case.
    // -------------------------------------------------------------------

    /// <summary>
    /// Builds a pipeline holding one source element per element data key the
    /// case names, followed by the element under test, processes one request
    /// and gives back what the script wrote.
    /// </summary>
    private static IAspectPropertyValue Produce(
        string scriptPath,
        IReadOnlyDictionary<string, object> properties)
    {
        var element = new DerivedPropertyElementBuilder(_loggerFactory)
            .AddScriptFile(scriptPath)
            .Build();
        var script = element.Scripts[0];

        var builder = new PipelineBuilder(_loggerFactory);
        foreach (var source in BuildSources(script, properties))
        {
            builder.AddFlowElement(source);
        }
        builder.AddFlowElement(element);

        // The pipeline owns the elements, so disposing it disposes them.
        using (var pipeline = builder.Build())
        using (var flowData = pipeline.CreateFlowData())
        {
            flowData.Process();
            if (flowData.TryGet(
                DerivedPropertyElement.DerivedElementDataKey,
                out var derived) == false)
            {
                throw new InvalidOperationException(
                    "the element wrote no element data");
            }
            if (derived.TryGet(script.Output.Name, out var written) == false)
            {
                throw new InvalidOperationException(
                    "the element wrote no value for the output property");
            }
            if (!(written is IAspectPropertyValue value))
            {
                throw new InvalidOperationException(
                    "the element wrote something that is not a value with a " +
                    "no value message");
            }
            return value;
        }
    }

    /// <summary>
    /// One source element per element data key the script names, whether or
    /// not the case gives any value for that key. Each element declares
    /// every property of the script that comes from its key and publishes
    /// only the values the case gives, so the pipeline sees every property
    /// as supplied and a case that leaves one out exercises the value being
    /// absent on the request.
    ///
    /// A property with no supplier anywhere in the pipeline is a pipeline
    /// build failure rather than an absent value, which is a rule about
    /// assembling a pipeline rather than about evaluating a script, so the
    /// shared cases do not cover it and DerivedPropertyElementTests does.
    /// </summary>
    private static List<IFlowElement> BuildSources(
        DerivedScript script,
        IReadOnlyDictionary<string, object> properties)
    {
        var byKey = new Dictionary<string, Dictionary<string, object>>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var property in script.Properties)
        {
            if (byKey.ContainsKey(property.ElementDataKey) == false)
            {
                byKey.Add(
                    property.ElementDataKey,
                    new Dictionary<string, object>(
                        StringComparer.OrdinalIgnoreCase));
            }
        }
        foreach (var property in properties)
        {
            var dot = property.Key.IndexOf('.');
            var key = property.Key.Substring(0, dot);
            var name = property.Key.Substring(dot + 1);
            if (byKey.TryGetValue(key, out var values) == false)
            {
                values = new Dictionary<string, object>(
                    StringComparer.OrdinalIgnoreCase);
                byKey.Add(key, values);
            }
            values[name] = property.Value;
        }

        var sources = new List<IFlowElement>();
        foreach (var key in byKey.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var values = byKey[key];
            var declared = script.Properties
                .Where(p => string.Equals(
                    p.ElementDataKey, key, StringComparison.OrdinalIgnoreCase))
                .Select(p => p.PropertyName)
                .Where(n => values.ContainsKey(n) == false)
                .ToList();
            sources.Add(new StubSourceElement(
                _loggerFactory.CreateLogger<
                    FlowElementBase<StubSourceData, ElementPropertyMetaData>>(),
                key,
                values,
                declared));
        }
        return sources;
    }

    // -------------------------------------------------------------------
    // Reading the four forms a case can write a value in.
    // -------------------------------------------------------------------

    /// <summary>
    /// The properties of one case, keyed as the case wrote them in
    /// elementKey.PropertyName form.
    /// </summary>
    private static Dictionary<string, object> ReadProperties(DerivedNode node)
    {
        var result = new Dictionary<string, object>(
            StringComparer.OrdinalIgnoreCase);
        if (node == null)
        {
            return result;
        }
        if (!(node is DerivedMapping mapping))
        {
            throw new FormatException(
                "Properties must be a mapping of source properties to values");
        }
        foreach (var name in mapping.Names)
        {
            if (name.IndexOf('.') < 0)
            {
                throw new FormatException(string.Format(
                    CultureInfo.InvariantCulture,
                    "'{0}' is not written as elementKey.PropertyName", name));
            }
            result.Add(name, ReadValue(name, mapping.Get(name)));
        }
        return result;
    }

    /// <summary>
    /// A case writes a value one of four ways, and each way stands for how a
    /// value reaches the element in a real pipeline. A plain value arrives
    /// as its own type. { String: "..." } arrives as text, which is how a
    /// value read from a data file or from a cloud response usually arrives.
    /// { NoValue: "..." } arrives as a value that carries a message saying
    /// why it has none. A list of Value and Weight pairs arrives as weighted
    /// values, of which the heaviest is taken.
    /// </summary>
    private static object ReadValue(string property, DerivedNode node)
    {
        if (node is DerivedScalar scalar)
        {
            return scalar.Value;
        }
        if (node is DerivedMapping mapping)
        {
            if (mapping.Has("String"))
            {
                return Convert.ToString(
                    Value(mapping.Get("String")), CultureInfo.InvariantCulture)
                    ?? string.Empty;
            }
            if (mapping.Has("NoValue"))
            {
                return new AspectPropertyValue<string>
                {
                    NoValueMessage = Text(mapping.Get("NoValue"))
                };
            }
            throw new FormatException(string.Format(
                CultureInfo.InvariantCulture,
                "'{0}' is written as a mapping that is neither String nor " +
                "NoValue", property));
        }
        if (node is DerivedSequence sequence)
        {
            var weighted = new List<WeightedValue<string>>();
            foreach (var item in sequence.Items)
            {
                if (!(item is DerivedMapping member) ||
                    member.Has("Value") == false ||
                    member.Has("Weight") == false)
                {
                    throw new FormatException(string.Format(
                        CultureInfo.InvariantCulture,
                        "'{0}' is written as a list, so every member needs a " +
                        "Value and a Weight", property));
                }
                var weight = Value(member.Get("Weight"));
                weighted.Add(new WeightedValue<string>(
                    Convert.ToUInt16(weight, CultureInfo.InvariantCulture),
                    Text(member.Get("Value"))));
            }
            return weighted;
        }
        throw new FormatException(string.Format(
            CultureInfo.InvariantCulture,
            "'{0}' is written in a way the runner does not know", property));
    }

    private static object Value(DerivedNode node)
    {
        return node is DerivedScalar scalar ? scalar.Value : null;
    }

    /// <summary>
    /// The string form of a single value, or null where the place holds
    /// something other than a single value.
    /// </summary>
    private static string Text(DerivedNode node)
    {
        if (!(node is DerivedScalar scalar) || scalar.Value == null)
        {
            return null;
        }
        return Convert.ToString(scalar.Value, CultureInfo.InvariantCulture);
    }

    // -------------------------------------------------------------------
    // Rejection cases.
    // -------------------------------------------------------------------

    private static void RunRejectionCases(string folder, RunReport report)
    {
        var invalidFolder = Path.Combine(folder, "tests", "invalid");
        if (Directory.Exists(invalidFolder) == false)
        {
            report.Notices.Add(
                "the shared folder holds no tests/invalid folder, so no " +
                "rejection cases were run");
            return;
        }
        var files = Directory
            .GetFiles(invalidFolder, "*.yaml")
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
        if (files.Count == 0)
        {
            report.Notices.Add(
                "the shared tests/invalid folder is empty, so no rejection " +
                "cases were run");
            return;
        }
        foreach (var file in files)
        {
            report.RejectionCases++;
            RunOneRejectionCase(file, report);
        }
    }

    private static void RunOneRejectionCase(string file, RunReport report)
    {
        var name = Path.GetFileName(file);
        DerivedMapping document;
        try
        {
            document = DerivedScriptParser.Parse(File.ReadAllText(file));
        }
        catch (DerivedScriptParseException exception)
        {
            report.RejectionFailures.Add(string.Format(
                CultureInfo.InvariantCulture,
                "{0}: the case file cannot be read: {1}",
                name, exception.Message));
            return;
        }

        var text = Text(document.Get("Script"));
        if (text == null)
        {
            report.RejectionFailures.Add(string.Format(
                CultureInfo.InvariantCulture,
                "{0}: Script must hold the text of the script to refuse",
                name));
            return;
        }

        // Name is the file name the script is judged under, which the
        // script's own Name must equal, and most cases leave it out.
        var judgedAs = Text(document.Get("Name"));
        var result = DerivedScriptValidator.Validate(text, judgedAs, name);
        if (result.IsValid)
        {
            report.RejectionFailures.Add(string.Format(
                CultureInfo.InvariantCulture,
                "{0}: the script was expected to be refused but it validated",
                name));
            return;
        }

        var expect = document.Get("Expect") as DerivedMapping;
        if (expect == null)
        {
            return;
        }

        if (expect.Get("Paths") is DerivedSequence paths)
        {
            foreach (var item in paths.Items)
            {
                var wanted = item is DerivedScalar scalar
                    ? scalar.Text ?? string.Empty
                    : null;
                if (wanted == null)
                {
                    continue;
                }
                if (result.Faults.Any(f => string.Equals(
                    f.Path, wanted, StringComparison.Ordinal)) == false)
                {
                    report.RejectionFailures.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}: expected a fault at '{1}'. The faults were:{2}",
                        name,
                        wanted,
                        Describe(result.Faults)));
                }
            }
        }

        if (expect.Get("Mentions") is DerivedSequence mentions)
        {
            foreach (var item in mentions.Items)
            {
                var wanted = Text(item);
                if (wanted == null)
                {
                    continue;
                }
                if (result.Faults.Any(f => f.Message.IndexOf(
                    wanted, StringComparison.Ordinal) >= 0) == false)
                {
                    report.RejectionFailures.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}: expected a fault mentioning '{1}'. The faults " +
                        "were:{2}",
                        name,
                        wanted,
                        Describe(result.Faults)));
                }
            }
        }
    }

    private static string Describe(IReadOnlyList<DerivedScriptFault> faults)
    {
        var message = new StringBuilder();
        foreach (var fault in faults)
        {
            message.AppendLine();
            message.AppendFormat(
                CultureInfo.InvariantCulture,
                "    at '{0}': {1}",
                fault.Path,
                fault.Message);
        }
        return message.ToString();
    }
}
