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

using FiftyOne.Pipeline.DerivedProperty.Data;
using System;
using System.IO;
using System.Linq;

namespace FiftyOne.Pipeline.DerivedProperty.Tests;

/// <summary>
/// Tests for <see cref="DerivedScriptWriter"/>, the canonical JSON the
/// element writes to its debug log at build.
///
/// The text has to match the JavaScript reference at tools/canonical.mjs of
/// the derived-properties repository character for character, so the last
/// test here compares against output produced by that reference and saved
/// beside this file as HumanConfidence.canonical.json.
/// </summary>
[TestClass]
public class DerivedScriptWriterTests
{
    /// <summary>
    /// Set by MSTest, and used to say why the cross language test could not
    /// run on a machine without the derived-properties submodule.
    /// </summary>
    public TestContext TestContext { get; set; }

    /// <summary>
    /// A script written to cover the parts of the form that are easy to get
    /// wrong, being the order of the Output fields, a source property whose
    /// type is unknown because only Present asks about it, named checks, All,
    /// Not, an aggregate compared with another aggregate, an aggregate over
    /// a named group of checks, and an Else.
    /// </summary>
    private const string ExampleYaml =
        """
        Format: 1
        Name: Example
        Version: 1.0.0
        Output:
          Name: Example
          Description: An example property.
          ValueType: string
          StoredValueType: string
          DefaultValue: Unknown
          IsList: false
          IsMandatory: true
          IsObsolete: false
          Category: General
          IsPopular: false
          ExportValues: true
          Url: "https://51degrees.com/documentation"
          DisplayOrder: 3
          PropertyId: 42
          VendorIds:
            - device
          Values:
            - { Name: High, Description: High confidence. }
            - { Name: Unknown }
        Optional:
          - device.IsCrawler
        Checks:
          NotCrawler: { Property: device.IsCrawler, Eq: false }
          Seen: { Property: device.IsVisible, Present: true }
          Fresh:
            All:
              - { Property: device.Year, Gt: 0 }
              - { Not: { Property: device.Year, Gt: 3000 } }
        Rules:
          - When: { Passed: Checks, Ge: { Evaluated: Checks } }
            Then: High
          - When: { Passed: [NotCrawler, Fresh], Ge: 1 }
            Then: High
          - Else: Unknown
        """;

    /// <summary>
    /// The same script written as JSON. The two build one model, which is
    /// how the two script formats are shown to mean the same thing.
    /// </summary>
    private const string ExampleJson =
        """
        {
          "Format": 1,
          "Name": "Example",
          "Version": "1.0.0",
          "Output": {
            "Name": "Example",
            "Description": "An example property.",
            "ValueType": "string",
            "StoredValueType": "string",
            "DefaultValue": "Unknown",
            "IsList": false,
            "IsMandatory": true,
            "IsObsolete": false,
            "Category": "General",
            "IsPopular": false,
            "ExportValues": true,
            "Url": "https://51degrees.com/documentation",
            "DisplayOrder": 3,
            "PropertyId": 42,
            "VendorIds": ["device"],
            "Values": [
              { "Name": "High", "Description": "High confidence." },
              { "Name": "Unknown" }
            ]
          },
          "Optional": ["device.IsCrawler"],
          "Checks": {
            "NotCrawler": { "Property": "device.IsCrawler", "Eq": false },
            "Seen": { "Property": "device.IsVisible", "Present": true },
            "Fresh": {
              "All": [
                { "Property": "device.Year", "Gt": 0 },
                { "Not": { "Property": "device.Year", "Gt": 3000 } }
              ]
            }
          },
          "Rules": [
            { "When": { "Passed": "Checks", "Ge": { "Evaluated": "Checks" } },
              "Then": "High" },
            { "When": { "Passed": ["NotCrawler", "Fresh"], "Ge": 1 },
              "Then": "High" },
            { "Else": "Unknown" }
          ]
        }
        """;

    /// <summary>
    /// The canonical JSON the example prints. Produced by the JavaScript
    /// reference as well as by the writer, and the two agree.
    /// </summary>
    private const string ExampleCanonical =
        """
        {
          "Format": 1,
          "Name": "Example",
          "Version": "1.0.0",
          "Output": {
            "Name": "Example",
            "Description": "An example property.",
            "ValueType": "string",
            "StoredValueType": "string",
            "DefaultValue": "Unknown",
            "IsList": false,
            "IsMandatory": true,
            "IsObsolete": false,
            "Category": "General",
            "IsPopular": false,
            "ExportValues": true,
            "Url": "https://51degrees.com/documentation",
            "DisplayOrder": 3,
            "PropertyId": 42,
            "VendorIds": [
              "device"
            ],
            "Dependencies": [
              "device.IsCrawler",
              "device.IsVisible",
              "device.Year"
            ],
            "Values": [
              {
                "Name": "High",
                "Description": "High confidence."
              },
              {
                "Name": "Unknown"
              }
            ]
          },
          "Optional": [
            "device.IsCrawler"
          ],
          "Properties": {
            "device.IsCrawler": {
              "Type": "bool",
              "Required": false
            },
            "device.IsVisible": {
              "Type": null,
              "Required": true
            },
            "device.Year": {
              "Type": "int",
              "Required": true
            }
          },
          "Checks": {
            "NotCrawler": {
              "Property": "device.IsCrawler",
              "Eq": false
            },
            "Seen": {
              "Property": "device.IsVisible",
              "Present": true
            },
            "Fresh": {
              "All": [
                {
                  "Property": "device.Year",
                  "Gt": 0
                },
                {
                  "Not": {
                    "Property": "device.Year",
                    "Gt": 3000
                  }
                }
              ]
            }
          },
          "Rules": [
            {
              "When": {
                "Passed": "Checks",
                "Ge": {
                  "Evaluated": "Checks"
                }
              },
              "Then": "High"
            },
            {
              "When": {
                "Passed": [
                  "NotCrawler",
                  "Fresh"
                ],
                "Ge": 1
              },
              "Then": "High"
            },
            {
              "Else": "Unknown"
            }
          ]
        }
        """;

    /// <summary>
    /// An int output, so that a rule can supply the whole number zero and a
    /// count of checks. Both are places a hand written serialiser can turn a
    /// value into text without anyone noticing.
    /// </summary>
    private const string CountYaml =
        """
        Format: 1
        Name: Count
        Version: 1.0.0
        Output:
          Name: Count
          Description: How many checks passed.
          ValueType: int
          IsList: false
        Checks:
          NotCrawler: { Property: device.IsCrawler, Eq: false }
        Rules:
          - When: { Property: device.IsCrawler, Eq: true }
            Then: 0
          - Else: { Passed: Checks }
        """;

    /// <summary>
    /// A double output, where the whole number 2 written for the value
    /// stands for 2.0 and has to print as 2, the way JavaScript prints it.
    /// </summary>
    private const string RatioYaml =
        """
        Format: 1
        Name: Ratio
        Version: 1.0.0
        Output:
          Name: Ratio
          Description: A ratio.
          ValueType: double
          IsList: false
        Rules:
          - When: { Property: device.Score, Gt: 0.5 }
            Then: 2
          - Else: 0.25
        """;

    [TestMethod]
    public void YamlAndTheJsonThatMirrorsItPrintTheSameCanonicalJson()
    {
        Assert.AreEqual(
            Canonical(ExampleYaml, "Example"),
            Canonical(ExampleJson, "Example"));
    }

    [TestMethod]
    public void TheCanonicalFormMatchesTheExpectedText()
    {
        Assert.AreEqual(
            Normalise(ExampleCanonical),
            Canonical(ExampleYaml, "Example"));
    }

    [TestMethod]
    public void LiteralTypesSurvive()
    {
        var example = Canonical(ExampleYaml, "Example");
        StringAssert.Contains(example, "\"Eq\": false");
        Assert.IsFalse(
            example.Contains("\"Eq\": \"false\"", StringComparison.Ordinal),
            "false was printed as text rather than as a boolean");
        StringAssert.Contains(example, "\"Gt\": 0");
        Assert.IsFalse(
            example.Contains("\"Gt\": \"0\"", StringComparison.Ordinal),
            "the whole number zero was printed as text");

        var count = Canonical(CountYaml, "Count");
        StringAssert.Contains(count, "\"Then\": 0");
        Assert.IsFalse(
            count.Contains("\"Then\": \"0\"", StringComparison.Ordinal),
            "a rule value of zero was printed as text");

        // A double that happens to be whole prints the way JavaScript prints
        // one, so 2.0 prints as 2 and not as 2.0.
        var ratio = Canonical(RatioYaml, "Ratio");
        StringAssert.Contains(ratio, "\"Then\": 2");
        Assert.IsFalse(
            ratio.Contains("\"Then\": 2.0", StringComparison.Ordinal),
            "a whole valued double kept a decimal point");
        StringAssert.Contains(ratio, "\"Else\": 0.25");
    }

    [TestMethod]
    public void ADeprecatedScriptPrintsItsNote()
    {
        var text = ExampleYaml.Replace(
            "Version: 1.0.0",
            "Version: 1.0.0\nDeprecated: true\n" +
            "DeprecationNote: Use Example2 instead.",
            StringComparison.Ordinal);
        var printed = Canonical(text, "Example");
        StringAssert.Contains(printed, "\"Deprecated\": true");
        StringAssert.Contains(
            printed, "\"DeprecationNote\": \"Use Example2 instead.\"");

        // The two keys sit between Version and Output, so the note reads in
        // the place the format reference puts it.
        StringAssert.Contains(
            printed,
            "\"Version\": \"1.0.0\",\n  \"Deprecated\": true,\n" +
            "  \"DeprecationNote\": \"Use Example2 instead.\",\n" +
            "  \"Output\": {");
    }

    [TestMethod]
    public void AScriptThatIsNotDeprecatedPrintsNeitherKey()
    {
        var printed = Canonical(ExampleYaml, "Example");
        Assert.IsFalse(
            printed.Contains("Deprecated", StringComparison.Ordinal),
            "a script that is not deprecated printed a Deprecated key");
        Assert.IsFalse(
            printed.Contains("DeprecationNote", StringComparison.Ordinal),
            "a script that is not deprecated printed a DeprecationNote key");
    }

    /// <summary>
    /// The cross language test. HumanConfidence is read from the
    /// derived-properties submodule and the text printed here is compared
    /// with the text the JavaScript reference printed for the same script,
    /// which was produced by running
    ///
    ///   node -e "import('./tools/run-cases.mjs').then(async rc=>{
    ///     const c=await import('./tools/canonical.mjs');
    ///     const s=rc.loadScripts('.').find(x=>x.name==='HumanConfidence');
    ///     console.log(c.canonical(s.model));})"
    ///
    /// from the root of the derived-properties repository, and saved as
    /// HumanConfidence.canonical.json.
    ///
    /// The submodule is not checked out on every machine, and no test may
    /// depend on the content of another repository, so a missing script
    /// leaves a message in the test output rather than failing.
    /// </summary>
    [TestMethod]
    public void HumanConfidenceMatchesTheJavaScriptReference()
    {
        var path = FindScript("HumanConfidence.yaml");
        if (path == null)
        {
            TestContext.WriteLine(
                "The derived-properties submodule is not checked out, so " +
                "there is no HumanConfidence.yaml to print and the two " +
                "languages were not compared. Run 'git submodule update " +
                "--init' to include this test.");
            return;
        }

        var reference = Path.Combine(
            AppContext.BaseDirectory, "HumanConfidence.canonical.json");
        Assert.IsTrue(
            File.Exists(reference),
            $"The JavaScript reference output is missing from {reference}.");

        Assert.AreEqual(
            Normalise(File.ReadAllText(reference)),
            Canonical(File.ReadAllText(path), "HumanConfidence"));
    }

    /// <summary>
    /// Validates the text of a script and prints it, failing the test with
    /// every fault where the script does not validate.
    /// </summary>
    private static string Canonical(string text, string name)
    {
        var result = DerivedScriptValidator.Validate(text, name, "code");
        Assert.IsTrue(
            result.IsValid,
            string.Join(
                Environment.NewLine,
                result.Faults.Select(f => f.ToString())));
        return DerivedScriptWriter.ToCanonicalJson(result.Script);
    }

    /// <summary>
    /// The writer always ends a line with a single line feed, as
    /// JSON.stringify does, so text held in this file or read from a file
    /// is compared with its line endings put into the same form.
    /// </summary>
    private static string Normalise(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    /// <summary>
    /// Finds a script in the derived-properties submodule by walking up
    /// from the folder the tests run in until the checked out repository is
    /// found. Returns null where the submodule is not checked out.
    /// </summary>
    private static string FindScript(string name)
    {
        var relative = Path.Combine(
            "FiftyOne.Pipeline.Elements",
            "FiftyOne.Pipeline.DerivedProperty",
            "Scripts",
            "scripts",
            name);
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
