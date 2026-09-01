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

using FiftyOne.Common.TestHelpers;
using FiftyOne.Pipeline.Core.Configuration;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.DerivedProperty.Data;
using FiftyOne.Pipeline.DerivedProperty.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FiftyOne.Pipeline.DerivedProperty.Tests;

/// <summary>
/// Tests the builder, the element, the metadata it exposes, the check it
/// runs when a pipeline adds it, and what it writes to the build log.
/// </summary>
[TestClass]
public class DerivedPropertyElementTests
{
    private TestLoggerFactory _loggerFactory;

    private string _folder;

    /// <summary>
    /// A folder of its own for each test, so a wildcard in one test never
    /// picks up a file another test wrote.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        _loggerFactory = new TestLoggerFactory();
        _folder = Path.Combine(
            Path.GetTempPath(),
            "derived-property-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    /// <summary>
    /// Removes the folder the test wrote its script files to.
    /// </summary>
    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, true);
            }
        }
        catch (IOException)
        {
            // A file left open by the machine's virus scanner must not
            // fail a test that has already made its point.
        }
    }

    // -----------------------------------------------------------------
    // The three ways to add a script.
    // -----------------------------------------------------------------

    /// <summary>
    /// A script that ships inside the package is added by naming its
    /// member of the generated enumeration.
    /// </summary>
    [TestMethod]
    public void Builder_AddBuiltInScript()
    {
        var members = (BuiltInScript[])Enum.GetValues(typeof(BuiltInScript));
        if (members.Length == 0)
        {
            // The scripts are embedded from a submodule which is not
            // checked out on every machine, and the package is built
            // without them when it is not.
            Assert.Inconclusive(
                "No scripts are embedded in this build of the package.");
        }

        using (var element = new DerivedPropertyElementBuilder(_loggerFactory)
            .AddScript(members[0])
            .Build())
        {
            Assert.HasCount(1, element.Scripts);
            Assert.AreEqual(members[0].ToString(), element.Scripts[0].Name);
            Assert.AreEqual(
                "built in " + members[0], element.Scripts[0].Source);
        }
    }

    /// <summary>
    /// A script file in the customer's own environment is added by path.
    /// </summary>
    [TestMethod]
    public void Builder_AddScriptFile()
    {
        var path = WriteScript("FromFile", Script("FromFile", "FromFile"));

        using (var element = new DerivedPropertyElementBuilder(_loggerFactory)
            .AddScriptFile(path)
            .Build())
        {
            Assert.HasCount(1, element.Scripts);
            Assert.AreEqual("FromFile", element.Scripts[0].Name);
            Assert.AreEqual(path, element.Scripts[0].Source);
        }
    }

    /// <summary>
    /// A script held as a string in the caller's own code is added by name
    /// and content.
    /// </summary>
    [TestMethod]
    public void Builder_AddScriptFromCode()
    {
        using (var element = new DerivedPropertyElementBuilder(_loggerFactory)
            .AddScript("FromCode", Script("FromCode", "FromCode"))
            .Build())
        {
            Assert.HasCount(1, element.Scripts);
            Assert.AreEqual("FromCode", element.Scripts[0].Name);
            Assert.AreEqual("code", element.Scripts[0].Source);
        }
    }

    /// <summary>
    /// The three ways of adding a script may be mixed in one element.
    /// </summary>
    [TestMethod]
    public void Builder_AllThreeSourcesInOneElement()
    {
        var members = (BuiltInScript[])Enum.GetValues(typeof(BuiltInScript));
        if (members.Length == 0)
        {
            Assert.Inconclusive(
                "No scripts are embedded in this build of the package.");
        }
        var path = WriteScript("MixedFile", Script("MixedFile", "MixedFile"));

        using (var element = new DerivedPropertyElementBuilder(_loggerFactory)
            .AddScript(members[0])
            .AddScriptFile(path)
            .AddScript("MixedCode", Script("MixedCode", "MixedCode"))
            .Build())
        {
            Assert.HasCount(3, element.Scripts);
            CollectionAssert.AreEqual(
                new[] { members[0].ToString(), "MixedFile", "MixedCode" },
                element.Scripts.Select(s => s.Name).ToArray());
        }
    }

    /// <summary>
    /// A path may hold a wildcard, and the files it matches are added in
    /// the same order on every build so that one configuration gives one
    /// element whatever order the file system lists in.
    /// </summary>
    [TestMethod]
    public void Builder_AddScriptFileWildcard()
    {
        WriteScript("Gamma", Script("Gamma", "Gamma"));
        WriteScript("Alpha", Script("Alpha", "Alpha"));
        WriteScript("Beta", Script("Beta", "Beta"));

        var pattern = Path.Combine(_folder, "*.yaml");
        var expected = new[] { "Alpha", "Beta", "Gamma" };

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using (var element =
                new DerivedPropertyElementBuilder(_loggerFactory)
                    .AddScriptFile(pattern)
                    .Build())
            {
                Assert.HasCount(3, element.Scripts);
                CollectionAssert.AreEqual(
                    expected,
                    element.Scripts.Select(s => s.Name).ToArray(),
                    "The order must not change between builds.");
            }
        }
    }

    // -----------------------------------------------------------------
    // The configuration keys.
    // -----------------------------------------------------------------

    /// <summary>
    /// SetScripts is what the configuration key Scripts binds to.
    /// </summary>
    [TestMethod]
    public void Builder_SetScripts()
    {
        var members = (BuiltInScript[])Enum.GetValues(typeof(BuiltInScript));
        if (members.Length == 0)
        {
            Assert.Inconclusive(
                "No scripts are embedded in this build of the package.");
        }

        using (var element = new DerivedPropertyElementBuilder(_loggerFactory)
            .SetScripts(new List<string> { members[0].ToString() })
            .Build())
        {
            Assert.HasCount(1, element.Scripts);
            Assert.AreEqual(members[0].ToString(), element.Scripts[0].Name);
        }
    }

    /// <summary>
    /// SetScriptFiles is what the configuration key ScriptFiles binds to.
    /// </summary>
    [TestMethod]
    public void Builder_SetScriptFiles()
    {
        var first = WriteScript("ListedOne", Script("ListedOne", "ListedOne"));
        var second = WriteScript("ListedTwo", Script("ListedTwo", "ListedTwo"));

        using (var element = new DerivedPropertyElementBuilder(_loggerFactory)
            .SetScriptFiles(new List<string> { first, second })
            .Build())
        {
            Assert.HasCount(2, element.Scripts);
            CollectionAssert.AreEqual(
                new[] { "ListedOne", "ListedTwo" },
                element.Scripts.Select(s => s.Name).ToArray());
        }
    }

    /// <summary>
    /// The element can be built from a real set of pipeline options, which
    /// is the path a customer's configuration file takes.
    /// </summary>
    [TestMethod]
    public void Builder_BuildFromConfiguration()
    {
        var path = WriteScript(
            "Configured", Script("Configured", "Configured"));

        var elementOptions = new ElementOptions()
        {
            BuilderName = "DerivedPropertyElement"
        };
        elementOptions.BuildParameters.Add("ScriptFiles", path);

        var options = new PipelineOptions();
        options.Elements.Add(elementOptions);

        using (var pipeline =
            new PipelineBuilder(_loggerFactory).BuildFromConfiguration(options))
        {
            var element = pipeline.GetElement<DerivedPropertyElement>();
            Assert.IsNotNull(element);
            Assert.HasCount(1, element.Scripts);
            Assert.AreEqual("Configured", element.Scripts[0].Name);

            using (var data = pipeline.CreateFlowData())
            {
                data.Process();
                Assert.AreEqual("Low", TextOf(data, "Configured"));
            }
        }
    }

    // -----------------------------------------------------------------
    // Build failures.
    // -----------------------------------------------------------------

    /// <summary>
    /// An element with no scripts has nothing to do, so building one is a
    /// mistake rather than a pipeline that quietly writes nothing.
    /// </summary>
    [TestMethod]
    public void Builder_BuildWithNoScriptsFails()
    {
        var builder = new DerivedPropertyElementBuilder(_loggerFactory);
        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => builder.Build());
        Assert.Contains("At least one script", exception.Message);
    }

    /// <summary>
    /// A script that does not validate stops the build, and the message
    /// lists every fault one per line rather than only the first.
    /// </summary>
    [TestMethod]
    public void Builder_FaultyScriptListsEveryFault()
    {
        var text =
            "Format: 2\n" +
            "Name: Faulty\n" +
            "Version: not a version\n" +
            "Output:\n" +
            "  Name: Faulty\n" +
            "  Description: A script written with several faults.\n" +
            "  ValueType: string\n" +
            "  IsList: false\n" +
            "Optional:\n" +
            "  - a.P\n" +
            "Rules:\n" +
            "  - When: { Property: a.P, Nope: 1 }\n" +
            "    Then: High\n";

        var builder = new DerivedPropertyElementBuilder(_loggerFactory)
            .AddScript("Faulty", text);
        var exception =
            Assert.ThrowsExactly<DerivedScriptValidationException>(
                () => builder.Build());

        Assert.IsGreaterThanOrEqualTo(3, exception.Faults.Count);

        var lines = exception.Message.Split(
            new[] { Environment.NewLine }, StringSplitOptions.None);
        // One line of counting followed by one line for each fault.
        Assert.HasCount(exception.Faults.Count + 1, lines);
        foreach (var fault in exception.Faults)
        {
            CollectionAssert.Contains(lines, fault.ToString());
        }
    }

    /// <summary>
    /// Two scripts in one element cannot both write the same property,
    /// because the element writes each property once.
    /// </summary>
    [TestMethod]
    public void Builder_TwoScriptsWritingTheSamePropertyFail()
    {
        var builder = new DerivedPropertyElementBuilder(_loggerFactory)
            .AddScript("First", Script("First", "Same"))
            .AddScript("Second", Script("Second", "Same"));

        var exception =
            Assert.ThrowsExactly<DerivedScriptValidationException>(
                () => builder.Build());
        Assert.Contains("write the property 'Same'", exception.Message);
    }

    // -----------------------------------------------------------------
    // The element itself.
    // -----------------------------------------------------------------

    /// <summary>
    /// The element writes under the key derived, takes no evidence, and
    /// every derived property element in a pipeline writes to one shared
    /// element data instance.
    /// </summary>
    [TestMethod]
    public void Element_KeyFilterAndSharedElementData()
    {
        using (var first = new DerivedPropertyElementBuilder(_loggerFactory)
            .AddScript("First", Script("First", "FirstOutput"))
            .Build())
        using (var second = new DerivedPropertyElementBuilder(_loggerFactory)
            .AddScript("Second", Script("Second", "SecondOutput"))
            .Build())
        {
            Assert.AreEqual("derived", first.ElementDataKey);
            Assert.AreEqual(
                DerivedPropertyElement.DerivedElementDataKey,
                first.ElementDataKey);

            var filter = (EvidenceKeyFilterWhitelist)first.EvidenceKeyFilter;
            Assert.IsEmpty(filter.Whitelist);
            Assert.IsFalse(first.EvidenceKeyFilter.Include("header.user-agent"));
            Assert.IsFalse(first.EvidenceKeyFilter.Include("query.anything"));

            using (var pipeline = new PipelineBuilder(_loggerFactory)
                .AddFlowElement(first)
                .AddFlowElement(second)
                .Build())
            using (var data = pipeline.CreateFlowData())
            {
                data.Process();
                var fromFirst = data.GetFromElement(first);
                var fromSecond = data.GetFromElement(second);
                Assert.AreSame(fromFirst, fromSecond);
                Assert.AreEqual("Low", TextOf(data, "FirstOutput"));
                Assert.AreEqual("Low", TextOf(data, "SecondOutput"));
            }
        }
    }

    /// <summary>
    /// The element declares one property for each script, with the name
    /// the script writes, the type the value comes back as, and the
    /// category the Output block gives.
    /// </summary>
    [TestMethod]
    public void Element_MetaDataIsOnePropertyPerScript()
    {
        using (var element = new DerivedPropertyElementBuilder(_loggerFactory)
            .AddScript("AsText",
                Script("AsText", "AsText", "string", "High", "Low", "General"))
            .AddScript("AsFlag",
                Script("AsFlag", "AsFlag", "bool", "true", "false", "Device"))
            .AddScript("AsWhole",
                Script("AsWhole", "AsWhole", "int", "1", "0", "Metrics"))
            .AddScript("AsNumber",
                Script("AsNumber", "AsNumber", "double", "1.5", "0.5", "Metrics"))
            .Build())
        {
            Assert.HasCount(4, element.Properties);

            var expected = new Dictionary<string, Type>
            {
                { "AsText", typeof(string) },
                { "AsFlag", typeof(bool) },
                { "AsWhole", typeof(int) },
                { "AsNumber", typeof(double) }
            };
            var expectedCategory = new Dictionary<string, string>
            {
                { "AsText", "General" },
                { "AsFlag", "Device" },
                { "AsWhole", "Metrics" },
                { "AsNumber", "Metrics" }
            };

            foreach (var property in element.Properties)
            {
                Assert.AreEqual(
                    expected[property.Name], property.Type, property.Name);
                Assert.AreEqual(
                    expectedCategory[property.Name],
                    property.Category,
                    property.Name);
                Assert.IsTrue(property.Available, property.Name);
                Assert.AreSame(element, property.Element);
            }
        }
    }

    /// <summary>
    /// Every field of the Output block reaches the element unchanged, so a
    /// script is a complete property definition rather than a name and a
    /// type.
    /// </summary>
    [TestMethod]
    public void Element_EveryOutputFieldSurvives()
    {
        var text =
            "Format: 1\n" +
            "Name: Everything\n" +
            "Version: 2.3.4\n" +
            "Output:\n" +
            "  Name: Everything\n" +
            "  Description: Every field of the Output block, carried " +
            "through.\n" +
            "  ValueType: string\n" +
            "  StoredValueType: azimuth\n" +
            "  DefaultValue: Unknown\n" +
            "  IsList: false\n" +
            "  IsMandatory: true\n" +
            "  IsObsolete: false\n" +
            "  Category: General\n" +
            "  IsPopular: true\n" +
            "  ExportValues: true\n" +
            "  Url: \"https://51degrees.com/documentation\"\n" +
            "  DisplayOrder: 7\n" +
            "  PropertyId: 1234\n" +
            "  VendorIds:\n" +
            "    - ip\n" +
            "    - device\n" +
            "  Dependencies:\n" +
            "    - a.P\n" +
            "  Values:\n" +
            "    - Name: High\n" +
            "      Description: The high value.\n" +
            "    - Name: Unknown\n" +
            "      Description: The unknown value.\n" +
            "Optional:\n" +
            "  - a.P\n" +
            "Rules:\n" +
            "  - When: { Property: a.P, Eq: true }\n" +
            "    Then: High\n";

        using (var element = new DerivedPropertyElementBuilder(_loggerFactory)
            .AddScript("Everything", text)
            .Build())
        {
            var script = element.Scripts[0];
            Assert.AreEqual("Everything", script.Name);
            Assert.AreEqual("2.3.4", script.Version);
            Assert.AreEqual(1, script.Format);

            var output = script.Output;
            Assert.AreEqual("Everything", output.Name);
            Assert.AreEqual(
                "Every field of the Output block, carried through.",
                output.Description);
            Assert.AreEqual(DerivedValueType.String, output.ValueType);
            Assert.AreEqual("Unknown", output.DefaultValue);
            Assert.IsFalse(output.IsList);
            Assert.IsTrue(output.IsMandatory);
            Assert.IsFalse(output.IsObsolete);
            Assert.AreEqual("General", output.Category);
            Assert.IsTrue(output.IsPopular);
            Assert.IsTrue(output.ExportValues);
            Assert.AreEqual(
                "https://51degrees.com/documentation", output.Url);
            Assert.AreEqual(7, output.DisplayOrder);
            Assert.AreEqual(1234, output.PropertyId);
            Assert.AreEqual("azimuth", output.StoredValueType);
            CollectionAssert.AreEqual(
                new[] { "ip", "device" }, output.VendorIds.ToArray());
            CollectionAssert.AreEqual(
                new[] { "a.P" }, output.Dependencies.ToArray());

            Assert.HasCount(2, output.Values);
            Assert.AreEqual("High", output.Values[0].Name);
            Assert.AreEqual("The high value.", output.Values[0].Description);
            Assert.AreEqual("Unknown", output.Values[1].Name);
            Assert.AreEqual(
                "The unknown value.", output.Values[1].Description);
        }
    }

    // -----------------------------------------------------------------
    // The pipeline check of DESIGN.md 4.3.
    // -----------------------------------------------------------------

    /// <summary>
    /// A required source property that nothing in the pipeline supplies
    /// fails the pipeline build, naming the property.
    /// </summary>
    [TestMethod]
    public void PipelineCheck_RequiredPropertyWithNoSupplierFails()
    {
        var element = new DerivedPropertyElementBuilder(_loggerFactory)
            .AddScript("Strict", RequiredScript)
            .Build();
        var builder = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(element);

        var exception = Assert.ThrowsExactly<PipelineConfigurationException>(
            () => builder.Build());
        Assert.Contains("'device.IsVisible'", exception.Message);
        Assert.Contains(
            "no element in the pipeline supplies", exception.Message);
        element.Dispose();
    }

    /// <summary>
    /// A supplier placed after the derived property element is named, so
    /// an ordering mistake says what to move rather than reading as a
    /// missing element.
    /// </summary>
    [TestMethod]
    public void PipelineCheck_SupplierAfterTheElementIsNamed()
    {
        var element = new DerivedPropertyElementBuilder(_loggerFactory)
            .AddScript("Strict", RequiredScript)
            .Build();
        var builder = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(element)
            .AddFlowElement(Source("device", "IsVisible", true));

        var exception = Assert.ThrowsExactly<PipelineConfigurationException>(
            () => builder.Build());
        Assert.Contains("StubSourceElement", exception.Message);
        Assert.Contains(
            "placed after the derived property element",
            exception.Message);
        element.Dispose();
    }

    /// <summary>
    /// An optional source property that nothing supplies builds, says so
    /// at information level, and is absent on every request.
    /// </summary>
    [TestMethod]
    public void PipelineCheck_OptionalPropertyWithNoSupplierIsLogged()
    {
        using (var element = new DerivedPropertyElementBuilder(_loggerFactory)
            .AddScript("Loose", Script("Loose", "Loose"))
            .Build())
        using (var pipeline = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(element)
            .Build())
        {
            var lines = _loggerFactory.Loggers
                .SelectMany(l => l.InfoEntries)
                .Where(e => e.Contains("names the optional property"))
                .ToList();
            Assert.HasCount(1, lines);
            Assert.Contains("'a.P'", lines[0]);

            // Absent on every request, so the rule that needs it never
            // matches and the Else supplies the answer.
            for (var i = 0; i < 3; i++)
            {
                using (var data = pipeline.CreateFlowData())
                {
                    data.Process();
                    Assert.AreEqual("Low", TextOf(data, "Loose"));
                }
            }
        }
    }

    /// <summary>
    /// Two elements in one pipeline cannot both write the same derived
    /// property, because one pipeline writes each property once.
    /// </summary>
    [TestMethod]
    public void PipelineCheck_TwoElementsWritingTheSamePropertyFail()
    {
        var first = new DerivedPropertyElementBuilder(_loggerFactory)
            .AddScript("First", Script("First", "Same"))
            .Build();
        var second = new DerivedPropertyElementBuilder(_loggerFactory)
            .AddScript("Second", Script("Second", "Same"))
            .Build();
        var builder = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(first)
            .AddFlowElement(second);

        var exception = Assert.ThrowsExactly<PipelineConfigurationException>(
            () => builder.Build());
        Assert.Contains(
            "already writes a property of that name", exception.Message);
        first.Dispose();
        second.Dispose();
    }

    /// <summary>
    /// A script may read a derived property an earlier element wrote,
    /// which is how one script builds on another.
    /// </summary>
    [TestMethod]
    public void PipelineCheck_AScriptCanReadAnEarlierDerivedProperty()
    {
        var second =
            "Format: 1\n" +
            "Name: Second\n" +
            "Version: 1.0.0\n" +
            "Output:\n" +
            "  Name: Second\n" +
            "  Description: Read from the property the first script " +
            "wrote.\n" +
            "  ValueType: string\n" +
            "  IsList: false\n" +
            "Rules:\n" +
            "  - When: { Property: derived.First, Eq: \"High\" }\n" +
            "    Then: Agreed\n" +
            "  - Else: Disagreed\n";

        using (var firstElement =
            new DerivedPropertyElementBuilder(_loggerFactory)
                .AddScript("First", Script("First", "First"))
                .Build())
        using (var secondElement =
            new DerivedPropertyElementBuilder(_loggerFactory)
                .AddScript("Second", second)
                .Build())
        using (var pipeline = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(Source("a", "P", true))
            .AddFlowElement(firstElement)
            .AddFlowElement(secondElement)
            .Build())
        using (var data = pipeline.CreateFlowData())
        {
            data.Process();
            Assert.AreEqual("High", TextOf(data, "First"));
            Assert.AreEqual("Agreed", TextOf(data, "Second"));
        }
    }

    // -----------------------------------------------------------------
    // The build log.
    // -----------------------------------------------------------------

    /// <summary>
    /// One information line per script names the script, its version, the
    /// format, where it came from and the property it writes.
    /// </summary>
    [TestMethod]
    public void Log_InformationLineNamesTheScript()
    {
        using (new DerivedPropertyElementBuilder(_loggerFactory)
            .AddScript("Logged", Script("Logged", "LoggedOutput"))
            .Build())
        {
            var lines = _loggerFactory.Loggers
                .SelectMany(l => l.InfoEntries)
                .Where(e => e.Contains("Derived property script 'Logged'"))
                .ToList();
            Assert.HasCount(1, lines);
            var line = lines[0];
            Assert.Contains("version 1.0.0", line);
            Assert.Contains("format 1", line);
            Assert.Contains("from code", line);
            Assert.Contains("'LoggedOutput'", line);
            Assert.Contains("as string", line);
        }
    }

    /// <summary>
    /// A deprecated script still works and says at warning level what to
    /// use instead.
    /// </summary>
    [TestMethod]
    public void Log_DeprecatedScriptWarnsWithItsNote()
    {
        var text =
            "Format: 1\n" +
            "Name: Old\n" +
            "Version: 1.0.0\n" +
            "Deprecated: true\n" +
            "DeprecationNote: Use the New script instead.\n" +
            "Output:\n" +
            "  Name: Old\n" +
            "  Description: A script that should no longer be used.\n" +
            "  ValueType: string\n" +
            "  IsList: false\n" +
            "Optional:\n" +
            "  - a.P\n" +
            "Rules:\n" +
            "  - When: { Property: a.P, Eq: true }\n" +
            "    Then: High\n" +
            "  - Else: Low\n";

        using (var element = new DerivedPropertyElementBuilder(_loggerFactory)
            .AddScript("Old", text)
            .Build())
        {
            Assert.IsTrue(element.Scripts[0].Deprecated);

            var lines = _loggerFactory.Loggers
                .SelectMany(l => l.WarningEntries)
                .Where(e => e.Contains("is deprecated"))
                .ToList();
            Assert.HasCount(1, lines);
            Assert.Contains("'Old'", lines[0]);
            Assert.Contains("Use the New script instead.", lines[0]);
        }
    }

    /// <summary>
    /// At debug level the compiled model is written as canonical JSON, so
    /// anyone holding the log can see what was evaluated without the file.
    /// </summary>
    [TestMethod]
    public void Log_DebugWritesTheCanonicalJson()
    {
        using (new DerivedPropertyElementBuilder(_loggerFactory)
            .AddScript("Traced", Script("Traced", "Traced"))
            .Build())
        {
            var lines = _loggerFactory.Loggers
                .SelectMany(l => l.DebugEntries)
                .Where(e => e.Contains("Derived property script 'Traced'"))
                .ToList();
            Assert.HasCount(1, lines);
            var line = lines[0];
            Assert.Contains("compiled to", line);
            Assert.Contains("\"Format\"", line);
            Assert.Contains("\"Output\"", line);
            Assert.Contains("\"Rules\"", line);
        }
    }

    // -----------------------------------------------------------------
    // Helpers.
    // -----------------------------------------------------------------

    /// <summary>
    /// A script whose only source property is optional, so an element
    /// built from it can go into a pipeline on its own.
    /// </summary>
    private static string Script(
        string name,
        string outputName,
        string valueType = "string",
        string thenValue = "High",
        string elseValue = "Low",
        string category = null)
    {
        return
            "Format: 1\n" +
            "Name: " + name + "\n" +
            "Version: 1.0.0\n" +
            "Output:\n" +
            "  Name: " + outputName + "\n" +
            "  Description: A property computed for the tests.\n" +
            "  ValueType: " + valueType + "\n" +
            "  IsList: false\n" +
            (category == null
                ? string.Empty
                : "  Category: " + category + "\n") +
            "Optional:\n" +
            "  - a.P\n" +
            "Rules:\n" +
            "  - When: { Property: a.P, Eq: true }\n" +
            "    Then: " + thenValue + "\n" +
            "  - Else: " + elseValue + "\n";
    }

    /// <summary>
    /// A script whose source properties are all required, used by the
    /// pipeline check tests.
    /// </summary>
    private const string RequiredScript =
        "Format: 1\n" +
        "Name: Strict\n" +
        "Version: 1.0.0\n" +
        "Output:\n" +
        "  Name: Strict\n" +
        "  Description: A property whose sources are all required.\n" +
        "  ValueType: string\n" +
        "  IsList: false\n" +
        "Rules:\n" +
        "  - When: { Property: device.IsVisible, Eq: true }\n" +
        "    Then: High\n" +
        "  - Else: Low\n";

    private string WriteScript(string name, string text)
    {
        var path = Path.Combine(_folder, name + ".yaml");
        File.WriteAllText(path, text);
        return path;
    }

    private StubSourceElement Source(
        string elementDataKey,
        string propertyName,
        object value)
    {
        return new StubSourceElement(
            _loggerFactory.CreateLogger<
                FlowElementBase<StubSourceData, ElementPropertyMetaData>>(),
            elementDataKey,
            new Dictionary<string, object> { { propertyName, value } });
    }

    private static string TextOf(IFlowData data, string propertyName)
    {
        var derived = data.Get(DerivedPropertyElement.DerivedElementDataKey);
        var value = (IAspectPropertyValue)derived[propertyName];
        Assert.IsTrue(value.HasValue, value.NoValueMessage);
        return (string)value.Value;
    }
}
