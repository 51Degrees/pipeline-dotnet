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
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FiftyOne.Pipeline.DerivedProperty.Tests;

/// <summary>
/// Tests for <see cref="DerivedScriptValidator"/>.
///
/// The cases mirror validate.test.mjs in the JavaScript reference at
/// D:\Workspace\derived-properties\tools, one case per fault class in
/// DESIGN.md 4.2, and each names the path of the fault and a fragment of
/// the message. Cases the JavaScript file cannot express are added at the
/// end and are marked as such.
/// </summary>
[TestClass]
public class DerivedScriptValidatorTests
{
    /// <summary>
    /// A script that validates, used as the base for the fault cases. The
    /// text is joined with a single line feed so that the line a fault
    /// points at is the same whatever line endings the file itself uses.
    /// </summary>
    private static readonly string Good = string.Join("\n", new[]
    {
        "",
        "Format: 1",
        "Name: Example",
        "Version: 1.0.0",
        "Output:",
        "  Name: Example",
        "  Description: An example property.",
        "  ValueType: string",
        "  IsList: false",
        "  DefaultValue: Unknown",
        "  Values:",
        "    - { Name: High, Description: High. }",
        "    - { Name: Low, Description: Low. }",
        "    - { Name: Unknown, Description: Unknown. }",
        "Checks:",
        "  NotCrawler: { Property: device.IsCrawler, Eq: false }",
        "Rules:",
        "  - When: { Check: NotCrawler }",
        "    Then: High",
        "  - Else: Low",
        ""
    });

    // ------------------------------------------------------------------
    // The positive cases.
    // ------------------------------------------------------------------

    /// <summary>
    /// The good script validates and builds a model.
    /// </summary>
    [TestMethod]
    public void Validate_AGoodScript_BuildsAModel()
    {
        var result = DerivedScriptValidator.Validate(Good, "Example", "test");

        AssertNoFaults(result);
        Assert.AreEqual("Example", result.Script.Name);
        Assert.AreEqual("1.0.0", result.Script.Version);
        Assert.AreEqual(1, result.Script.Format);
        Assert.AreEqual(
            DerivedValueType.String, result.Script.Output.ValueType);
        Assert.HasCount(2, result.Script.Rules);
        Assert.HasCount(1, result.Script.Checks);
        Assert.AreEqual("NotCrawler", result.Script.Checks[0].Name);
        Assert.IsTrue(result.IsValid);
    }

    /// <summary>
    /// Dependencies are worked out from the properties the checks and the
    /// rules name, where the script does not list them.
    /// </summary>
    [TestMethod]
    public void Validate_DependenciesAreComputed_WhereTheScriptOmitsThem()
    {
        var result = DerivedScriptValidator.Validate(Good, "Example", "test");

        AssertNoFaults(result);
        CollectionAssert.AreEqual(
            new[] { "device.IsCrawler" },
            result.Script.Output.Dependencies.ToList());
    }

    /// <summary>
    /// The type of each source property is worked out from the literal the
    /// property is compared against, and is recorded on the model.
    /// </summary>
    [TestMethod]
    public void Validate_TheInferredTypeOfEachSourceProperty_IsRecorded()
    {
        var result = DerivedScriptValidator.Validate(Good, "Example", "test");

        AssertNoFaults(result);
        Assert.HasCount(1, result.Script.Properties);
        var property = result.Script.Properties[0];
        Assert.AreEqual("device.IsCrawler", property.Name);
        Assert.AreEqual("device", property.ElementDataKey);
        Assert.AreEqual("IsCrawler", property.PropertyName);
        Assert.AreEqual(DerivedValueType.Bool, property.ValueType);
    }

    /// <summary>
    /// Each operator infers the type of the literal it is given.
    /// </summary>
    [TestMethod]
    public void Validate_TypesAreInferred_FromTheLiteralComparedAgainst()
    {
        var text = Replace(
            "  NotCrawler: { Property: device.IsCrawler, Eq: false }",
            "  NotCrawler: { Property: device.IsCrawler, Eq: false }\n" +
            "  Year:  { Property: device.Year, Gt: 0 }\n" +
            "  Age:   { Property: device.Age, Lt: 2.5 }\n" +
            "  Name:  { Property: device.Name, StartsWith: \"Chr\" }");

        var result = DerivedScriptValidator.Validate(text, "Example", "test");

        AssertNoFaults(result);
        var types = result.Script.Properties.ToDictionary(
            p => p.Name, p => p.ValueType);
        Assert.HasCount(4, types);
        Assert.AreEqual(DerivedValueType.Bool, types["device.IsCrawler"]);
        Assert.AreEqual(DerivedValueType.Int, types["device.Year"]);
        Assert.AreEqual(DerivedValueType.Double, types["device.Age"]);
        Assert.AreEqual(DerivedValueType.String, types["device.Name"]);
    }

    /// <summary>
    /// Property names are matched without regard to case, as the Pipeline
    /// matches them elsewhere, so two conditions naming one property in
    /// two letter cases name one source property.
    /// </summary>
    [TestMethod]
    public void Validate_PropertyNames_AreMatchedWithoutRegardToCase()
    {
        var text = Replace(
            "  - When: { Check: NotCrawler }",
            "  - When: { Property: DEVICE.iscrawler, Eq: false }");

        var result = DerivedScriptValidator.Validate(text, "Example", "test");

        AssertNoFaults(result);
        Assert.HasCount(1, result.Script.Properties);
        // The name kept is the one written first, being the check's.
        Assert.AreEqual("device.IsCrawler", result.Script.Properties[0].Name);
    }

    /// <summary>
    /// The format's own keys are matched without regard to case, as
    /// pipeline configuration files are.
    /// </summary>
    [TestMethod]
    public void Validate_TopLevelKeys_AreMatchedWithoutRegardToCase()
    {
        var text = Good
            .Replace("Format: 1", "format: 1", StringComparison.Ordinal)
            .Replace("Rules:", "rules:", StringComparison.Ordinal);

        var result = DerivedScriptValidator.Validate(text, "Example", "test");

        AssertNoFaults(result);
        Assert.AreEqual(1, result.Script.Format);
        Assert.HasCount(2, result.Script.Rules);
    }

    /// <summary>
    /// A JSON script and the YAML mirroring it build an equal model.
    /// </summary>
    [TestMethod]
    public void Validate_AJsonScriptAndTheYamlItMirrors_GiveAnEqualModel()
    {
        var json =
            "{\"Format\":1,\"Name\":\"Example\",\"Version\":\"1.0.0\"," +
            "\"Output\":{\"Name\":\"Example\"," +
            "\"Description\":\"An example property.\"," +
            "\"ValueType\":\"string\",\"IsList\":false," +
            "\"DefaultValue\":\"Unknown\",\"Values\":[" +
            "{\"Name\":\"High\",\"Description\":\"High.\"}," +
            "{\"Name\":\"Low\",\"Description\":\"Low.\"}," +
            "{\"Name\":\"Unknown\",\"Description\":\"Unknown.\"}]}," +
            "\"Checks\":{\"NotCrawler\":" +
            "{\"Property\":\"device.IsCrawler\",\"Eq\":false}}," +
            "\"Rules\":[{\"When\":{\"Check\":\"NotCrawler\"}," +
            "\"Then\":\"High\"},{\"Else\":\"Low\"}]}";

        var fromJson = DerivedScriptValidator.Validate(json, "Example", "test");
        var fromYaml = DerivedScriptValidator.Validate(Good, "Example", "test");

        AssertNoFaults(fromJson);
        AssertNoFaults(fromYaml);
        AssertSameScript(fromJson.Script, fromYaml.Script);
    }

    // ------------------------------------------------------------------
    // One case per fault class in DESIGN.md 4.2.
    // ------------------------------------------------------------------

    /// <summary>Text that does not parse at all.</summary>
    [TestMethod]
    public void Fault_TextThatDoesNotParse()
    {
        var faults = FaultsOf("Name: [unclosed\n");

        AssertFault(faults, string.Empty, "not valid YAML");
    }

    /// <summary>Format missing.</summary>
    [TestMethod]
    public void Fault_FormatMissing()
    {
        var faults = FaultsOf(Good.Replace(
            "Format: 1\n", string.Empty, StringComparison.Ordinal));

        AssertFault(faults, "Format", "required key 'Format' is missing");
    }

    /// <summary>Format present but not 1.</summary>
    [TestMethod]
    public void Fault_FormatIsNotOne()
    {
        var faults = FaultsOf(Replace("Format: 1", "Format: 2"));

        AssertFault(faults, "Format", "Format must be 1, found 2");
    }

    /// <summary>An unknown key at the top level.</summary>
    [TestMethod]
    public void Fault_AnUnknownKeyAtTheTopLevel()
    {
        var faults = FaultsOf(Good + "Inputs:\n  - device.IsCrawler\n");

        AssertFault(faults, "Inputs", "unknown key 'Inputs'");
    }

    /// <summary>An unknown key under Output, such as a typo.</summary>
    [TestMethod]
    public void Fault_AnUnknownKeyUnderOutput()
    {
        var faults = FaultsOf(Replace(
            "  Description: An example property.",
            "  Description: An example property.\n  Descriptoin: typed twice"));

        AssertFault(
            faults, "Output.Descriptoin", "unknown key 'Descriptoin'");
    }

    /// <summary>A key holding a value of the wrong type.</summary>
    [TestMethod]
    public void Fault_AKeyOfTheWrongType()
    {
        var faults = FaultsOf(Replace("  IsList: false", "  IsList: nope"));

        AssertFault(faults, "Output.IsList", "expected a boolean");
    }

    /// <summary>Name not matching the identifier pattern.</summary>
    [TestMethod]
    public void Fault_NameDoesNotMatchThePattern()
    {
        var faults = FaultsOf(
            Replace("Name: Example\nVersion", "Name: 1Example\nVersion"),
            "1Example");

        AssertFault(faults, "Name", "does not match the pattern");
    }

    /// <summary>Name not equal to the file name.</summary>
    [TestMethod]
    public void Fault_NameIsNotEqualToTheFileName()
    {
        var faults = FaultsOf(Good, "SomethingElse");

        AssertFault(
            faults,
            "Name",
            "script name 'Example' must equal the file name 'SomethingElse'");
    }

    /// <summary>A value type format 1 does not define.</summary>
    [TestMethod]
    public void Fault_OutputValueTypeOutsideTheFormatOneSet()
    {
        var faults = FaultsOf(Replace(
            "  ValueType: string", "  ValueType: weightedstring"));

        AssertFault(faults, "Output.ValueType", "'weightedstring'");
    }

    /// <summary>A list output, which format 1 defers.</summary>
    [TestMethod]
    public void Fault_OutputIsListIsTrue()
    {
        var faults = FaultsOf(Replace("  IsList: false", "  IsList: true"));

        AssertFault(faults, "Output.IsList", "must be false in format 1");
    }

    /// <summary>An operator the format does not define.</summary>
    [TestMethod]
    public void Fault_AnOperatorThatDoesNotExist()
    {
        var faults = FaultsOf(Replace(
            "{ Property: device.IsCrawler, Eq: false }",
            "{ Property: device.IsCrawler, Equals: false }"));

        AssertFault(faults, "Checks.NotCrawler", "unknown operator 'Equals'");
    }

    /// <summary>More than one operator in one condition.</summary>
    [TestMethod]
    public void Fault_MoreThanOneOperatorInACondition()
    {
        var faults = FaultsOf(Replace(
            "{ Property: device.IsCrawler, Eq: false }",
            "{ Property: device.IsCrawler, Eq: false, Ne: true }"));

        AssertFault(faults, "Checks.NotCrawler", "exactly one operator");
    }

    /// <summary>
    /// An operator that does not work on the type the literal infers.
    /// Ordering is not allowed on text because collation differs between
    /// languages.
    /// </summary>
    [TestMethod]
    public void Fault_AnOperatorNotAllowedOnTheInferredType()
    {
        var faults = FaultsOf(Replace(
            "{ Property: device.IsCrawler, Eq: false }",
            "{ Property: device.BrowserName, Gt: \"A\" }"));

        AssertFault(
            faults,
            "Checks.NotCrawler",
            "operator 'Gt' is not allowed on type string");
    }

    /// <summary>A null literal to compare against.</summary>
    [TestMethod]
    public void Fault_ANullLiteral()
    {
        var faults = FaultsOf(Replace(
            "{ Property: device.IsCrawler, Eq: false }",
            "{ Property: device.IsCrawler, Eq: null }"));

        AssertFault(faults, "Checks.NotCrawler.Eq", "a null literal");
    }

    /// <summary>
    /// The same property compared as two types, with both places named so
    /// the author can see which use to change.
    /// </summary>
    [TestMethod]
    public void Fault_ThePropertyInferringTwoTypes_NamesBothPlaces()
    {
        var faults = FaultsOf(Replace(
            "Rules:\n  - When: { Check: NotCrawler }",
            "Rules:\n  - When: { Property: device.IsCrawler, Eq: 1 }"));

        AssertFault(
            faults,
            "Rules[0].When",
            "already inferred as bool at Checks.NotCrawler");
    }

    /// <summary>A Check reference naming a check that is not there.</summary>
    [TestMethod]
    public void Fault_ACheckReferenceThatIsNotDefined()
    {
        var faults = FaultsOf(Replace(
            "{ Check: NotCrawler }", "{ Check: NoSuch }"));

        AssertFault(
            faults, "Rules[0].When.Check", "check 'NoSuch' is not defined");
    }

    /// <summary>A group naming a check that is not there.</summary>
    [TestMethod]
    public void Fault_ACheckGroupNamingAnUnknownCheck()
    {
        var faults = FaultsOf(Replace(
            "  - When: { Check: NotCrawler }",
            "  - When: { Passed: [NotCrawler, NoSuch], Ge: 1 }"));

        AssertFault(
            faults, "Rules[0].When.Passed", "check 'NoSuch' is not defined");
    }

    /// <summary>A Then that is not one of the listed values.</summary>
    [TestMethod]
    public void Fault_ThenOutsideOutputValues()
    {
        var faults = FaultsOf(Replace("    Then: High", "    Then: Enormous"));

        AssertFault(
            faults, "Rules[0].Then", "'Enormous' is not one of the values");
    }

    /// <summary>A Then of a type the output does not return.</summary>
    [TestMethod]
    public void Fault_ThenOfTheWrongType()
    {
        var faults = FaultsOf(Replace("    Then: High", "    Then: 7"));

        AssertFault(faults, "Rules[0].Then", "expected a string");
    }

    /// <summary>A DefaultValue that is not one of the listed values.</summary>
    [TestMethod]
    public void Fault_DefaultValueOutsideOutputValues()
    {
        var faults = FaultsOf(Replace(
            "  DefaultValue: Unknown", "  DefaultValue: Nothing"));

        AssertFault(
            faults,
            "Output.DefaultValue",
            "'Nothing' is not one of the values");
    }

    /// <summary>An Else that is not the last rule.</summary>
    [TestMethod]
    public void Fault_ElseAnywhereButLast()
    {
        var faults = FaultsOf(Replace(
            "  - When: { Check: NotCrawler }\n    Then: High\n  - Else: Low",
            "  - Else: Low\n  - When: { Check: NotCrawler }\n    Then: High"));

        AssertFault(
            faults, "Rules[0]", "Else is only allowed on the last rule");
    }

    /// <summary>A rule carrying both When and Else.</summary>
    [TestMethod]
    public void Fault_ARuleWithBothWhenAndElse()
    {
        var faults = FaultsOf(Replace(
            "  - Else: Low",
            "  - When: { Check: NotCrawler }\n    Else: Low"));

        AssertFault(faults, "Rules[1]", "has both When and Else");
    }

    /// <summary>
    /// A Then that is a mapping rather than a literal. Then and Else are
    /// literals of Output.ValueType and nothing else.
    /// </summary>
    [TestMethod]
    public void Fault_AThenThatIsNotALiteral()
    {
        var faults = FaultsOf(Replace(
            "    Then: High", "    Then: { Passed: Checks }"));

        AssertFault(
            faults,
            "Rules[0].Then",
            "a rule value is a literal of the output value type");
    }

    /// <summary>
    /// A Rules list whose last entry is not an Else. Every script ends in
    /// an Else, so that a script always chooses a value once its source
    /// properties have been read.
    /// </summary>
    [TestMethod]
    public void Fault_TheLastRuleIsNotAnElse()
    {
        var faults = FaultsOf(Replace("  - Else: Low\n", string.Empty));

        AssertFault(faults, "Rules", "the last rule must be an Else");
    }

    /// <summary>Rules missing.</summary>
    [TestMethod]
    public void Fault_RulesMissing()
    {
        var faults = FaultsOf(Good.Substring(
            0, Good.IndexOf("Rules:", StringComparison.Ordinal)));

        AssertFault(faults, "Rules", "required key 'Rules' is missing");
    }

    /// <summary>Rules present but holding nothing.</summary>
    [TestMethod]
    public void Fault_RulesIsEmpty()
    {
        var faults = FaultsOf(
            Good.Substring(
                0, Good.IndexOf("Rules:", StringComparison.Ordinal)) +
            "Rules: []\n");

        AssertFault(faults, "Rules", "at least one rule");
    }

    /// <summary>
    /// A source property written without the element it comes from.
    /// </summary>
    [TestMethod]
    public void Fault_ASourcePropertyThatIsNotElementKeyDotPropertyName()
    {
        var faults = FaultsOf(Replace(
            "{ Property: device.IsCrawler, Eq: false }",
            "{ Property: IsCrawler, Eq: false }"));

        AssertFault(
            faults, "Checks.NotCrawler.Property", "elementKey.PropertyName");
    }

    /// <summary>A deprecated script that does not say what to use.</summary>
    [TestMethod]
    public void Fault_DeprecationNoteMissingWhenDeprecatedIsTrue()
    {
        var faults = FaultsOf(Replace(
            "Version: 1.0.0", "Version: 1.0.0\nDeprecated: true"));

        AssertFault(
            faults, "DeprecationNote", "a deprecated script must say");
    }

    /// <summary>A list literal whose members are not all one type.</summary>
    [TestMethod]
    public void Fault_AMixedTypeListLiteral()
    {
        var faults = FaultsOf(Replace(
            "{ Property: device.IsCrawler, Eq: false }",
            "{ Property: device.BrowserName, In: [\"Chrome\", 7] }"));

        AssertFault(faults, "Checks.NotCrawler.In", "every member of a list");
    }

    /// <summary>A Version that is not a semantic version.</summary>
    [TestMethod]
    public void Fault_VersionIsNotASemanticVersion()
    {
        var faults = FaultsOf(Replace("Version: 1.0.0", "Version: one"));

        AssertFault(faults, "Version", "semantic version");
    }

    // ------------------------------------------------------------------
    // What every fault carries.
    // ------------------------------------------------------------------

    /// <summary>
    /// A fault in a YAML script carries the line, so an editor can jump to
    /// the place.
    /// </summary>
    [TestMethod]
    public void Faults_CarryALineNumber_ForAYamlScript()
    {
        var faults = FaultsOf(Replace("  IsList: false", "  IsList: true"));

        var fault = faults.Single(
            f => string.Equals(
                f.Path, "Output.IsList", StringComparison.Ordinal));
        // Line 1 is the empty line the script opens with, so IsList sits on
        // line 9.
        Assert.AreEqual(9, fault.Line);
    }

    /// <summary>
    /// Validation collects everything wrong rather than stopping at the
    /// first fault, so an author fixes a file in one pass.
    /// </summary>
    [TestMethod]
    public void Faults_AreCollected_RatherThanStoppingAtTheFirst()
    {
        var faults = FaultsOf(Replace("Format: 1", "Format: 3")
            .Replace(
                "  IsList: false", "  IsList: true", StringComparison.Ordinal));

        Assert.IsGreaterThanOrEqualTo(
            2,
            faults.Count,
            "expected several faults, found " + Describe(faults));
        AssertFault(faults, "Format", "Format must be 1, found 3");
        AssertFault(faults, "Output.IsList", "must be false in format 1");
    }

    /// <summary>
    /// A fault names the script and where the script came from, so a build
    /// log says which file to open.
    /// </summary>
    [TestMethod]
    public void Faults_CarryTheScriptNameAndTheSource()
    {
        var faults = FaultsOf(Replace("Format: 1", "Format: 9"));

        Assert.AreEqual("Example", faults[0].Script);
        Assert.AreEqual("test", faults[0].Source);
    }

    // ------------------------------------------------------------------
    // Cases the JavaScript file cannot express.
    // ------------------------------------------------------------------

    /// <summary>
    /// The exception raised for a list of faults names how many there were
    /// and lists one fault per line.
    /// </summary>
    [TestMethod]
    public void ValidationException_ListsOneFaultPerLine()
    {
        var faults = FaultsOf(Replace("Format: 1", "Format: 3")
            .Replace(
                "  IsList: false", "  IsList: true", StringComparison.Ordinal));

        var exception = new DerivedScriptValidationException(faults);

        var lines = exception.Message.Split(
            new[] { Environment.NewLine }, StringSplitOptions.None);
        Assert.HasCount(
            faults.Count + 1,
            lines,
            "the message was:\n" + exception.Message);
        Assert.AreEqual(
            faults.Count + " faults were found while reading derived " +
            "property scripts.",
            lines[0]);
        for (var i = 0; i < faults.Count; i++)
        {
            Assert.AreEqual(faults[i].ToString(), lines[i + 1]);
        }
        CollectionAssert.AreEqual(
            faults.ToList(), exception.Faults.ToList());
    }

    /// <summary>
    /// One fault names how many faults there were in the singular, so the
    /// message reads properly either way.
    /// </summary>
    [TestMethod]
    public void ValidationException_OneFault_ReadsInTheSingular()
    {
        var faults = FaultsOf(Replace("Format: 1", "Format: 9"));

        var exception = new DerivedScriptValidationException(faults);

        StringAssert.StartsWith(
            exception.Message,
            "1 fault was found while reading derived property scripts.");
    }

    /// <summary>
    /// A fault printed as one line names the script, where the script came
    /// from, the line and the place in the document.
    /// </summary>
    [TestMethod]
    public void Fault_ToString_NamesTheScriptSourceLineAndPath()
    {
        var fault = new DerivedScriptFault(
            "Example",
            "scripts/Example.yml",
            "Rules[3].When.All[1]",
            42,
            "check 'NoSuch' is not defined");

        Assert.AreEqual(
            "Example (scripts/Example.yml) line 42 at " +
            "Rules[3].When.All[1]: check 'NoSuch' is not defined",
            fault.ToString());
    }

    /// <summary>
    /// A fault with no name, no line and no place still prints a line a
    /// person can read.
    /// </summary>
    [TestMethod]
    public void Fault_ToString_WithNothingKnown_NamesTheDocument()
    {
        var fault = new DerivedScriptFault(
            null, "code", string.Empty, 0, "the script is empty");

        Assert.AreEqual(
            "script (code) at (document): the script is empty",
            fault.ToString());
    }

    // ------------------------------------------------------------------
    // Helpers.
    // ------------------------------------------------------------------

    /// <summary>
    /// Replaces one part of the good script, to make one fault at a time.
    /// </summary>
    private static string Replace(string find, string replace)
    {
        Assert.IsTrue(
            Good.Contains(find, StringComparison.Ordinal),
            "the good script must hold \"" + find + "\"");
        return Good.Replace(find, replace, StringComparison.Ordinal);
    }

    /// <summary>
    /// Validates and hands back the faults, having checked the script was
    /// rejected.
    /// </summary>
    private static IReadOnlyList<DerivedScriptFault> FaultsOf(
        string text,
        string name = "Example")
    {
        var result = DerivedScriptValidator.Validate(text, name, "test");
        Assert.IsNull(
            result.Script,
            "the script was expected to be rejected but it validated");
        Assert.IsFalse(result.IsValid);
        Assert.IsNotEmpty(result.Faults);
        return result.Faults;
    }

    /// <summary>
    /// Asserts one fault sits at the path and that its message holds the
    /// fragment. A failure prints every fault found.
    /// </summary>
    private static void AssertFault(
        IReadOnlyList<DerivedScriptFault> faults,
        string path,
        string fragment)
    {
        var matching = faults
            .Where(f => string.Equals(f.Path, path, StringComparison.Ordinal))
            .ToList();
        Assert.IsNotEmpty(
            matching,
            "no fault at path \"" + path + "\". Faults were:\n" +
            Describe(faults));
        Assert.IsNotEmpty(
            matching.Where(
                f => f.Message.Contains(fragment, StringComparison.Ordinal)),
            "no fault at \"" + path + "\" mentioning \"" + fragment +
            "\". Found:\n" + Describe(matching));
    }

    private static void AssertNoFaults(DerivedScriptValidationResult result)
    {
        Assert.IsEmpty(
            result.Faults,
            "the script was expected to validate. Faults were:\n" +
            Describe(result.Faults));
        Assert.IsNotNull(result.Script);
    }

    private static string Describe(IReadOnlyList<DerivedScriptFault> faults)
    {
        var builder = new StringBuilder();
        foreach (var fault in faults)
        {
            builder
                .Append("  ")
                .Append(string.IsNullOrEmpty(fault.Path)
                    ? "(document)"
                    : fault.Path)
                .Append(": ")
                .Append(fault.Message)
                .Append('\n');
        }
        return builder.ToString();
    }

    /// <summary>
    /// Whether two models hold the same output, the same source
    /// properties, the same checks and the same rules. Only what the model
    /// exposes is compared, which is everything a caller can see.
    /// </summary>
    private static void AssertSameScript(
        DerivedScript left,
        DerivedScript right)
    {
        Assert.AreEqual(left.Format, right.Format);
        Assert.AreEqual(left.Name, right.Name);
        Assert.AreEqual(left.Version, right.Version);
        Assert.AreEqual(left.Deprecated, right.Deprecated);
        Assert.AreEqual(left.DeprecationNote, right.DeprecationNote);

        Assert.AreEqual(left.Output.Name, right.Output.Name);
        Assert.AreEqual(left.Output.Description, right.Output.Description);
        Assert.AreEqual(left.Output.ValueType, right.Output.ValueType);
        Assert.AreEqual(left.Output.IsList, right.Output.IsList);
        Assert.AreEqual(left.Output.DefaultValue, right.Output.DefaultValue);
        CollectionAssert.AreEqual(
            left.Output.Dependencies.ToList(),
            right.Output.Dependencies.ToList());
        Assert.HasCount(left.Output.Values.Count, right.Output.Values);
        for (var i = 0; i < left.Output.Values.Count; i++)
        {
            Assert.AreEqual(
                left.Output.Values[i].Name, right.Output.Values[i].Name);
            Assert.AreEqual(
                left.Output.Values[i].Description,
                right.Output.Values[i].Description);
        }

        Assert.HasCount(left.Properties.Count, right.Properties);
        for (var i = 0; i < left.Properties.Count; i++)
        {
            Assert.AreEqual(left.Properties[i].Name, right.Properties[i].Name);
            Assert.AreEqual(
                left.Properties[i].ValueType, right.Properties[i].ValueType);
        }

        Assert.HasCount(left.Checks.Count, right.Checks);
        for (var i = 0; i < left.Checks.Count; i++)
        {
            Assert.AreEqual(left.Checks[i].Name, right.Checks[i].Name);
            Assert.AreEqual(
                left.Checks[i].Condition.GetType(),
                right.Checks[i].Condition.GetType());
        }

        Assert.HasCount(left.Rules.Count, right.Rules);
        for (var i = 0; i < left.Rules.Count; i++)
        {
            Assert.AreEqual(left.Rules[i].IsElse, right.Rules[i].IsElse);
            Assert.AreEqual(left.Rules[i].Value, right.Rules[i].Value);
            Assert.AreEqual(
                left.Rules[i].Condition == null,
                right.Rules[i].Condition == null);
            if (left.Rules[i].Condition != null)
            {
                Assert.AreEqual(
                    left.Rules[i].Condition.GetType(),
                    right.Rules[i].Condition.GetType());
            }
        }
    }
}
