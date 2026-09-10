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

namespace FiftyOne.Pipeline.DerivedProperty.Tests;

/// <summary>
/// Tests for <see cref="DerivedScriptParser"/>.
///
/// The cases mirror parse.test.mjs in the JavaScript reference at
/// D:\Workspace\derived-properties\tools, because the two languages have
/// to read one script the same way. Cases the JavaScript file cannot
/// express, such as the runtime type a value is read as, are added at the
/// end and are marked as such.
/// </summary>
[TestClass]
public class DerivedScriptParserTests
{
    // ------------------------------------------------------------------
    // The cases parse.test.mjs runs.
    // ------------------------------------------------------------------

    /// <summary>
    /// YAML is read into a mapping of names to values.
    /// </summary>
    [TestMethod]
    public void Parse_Yaml_ReadsIntoAMapping()
    {
        var document = DerivedScriptParser.Parse(
            "Format: 1\nName: Example\n");

        CollectionAssert.AreEqual(
            new[] { "Format", "Name" },
            new List<string>(document.Names));
        Assert.AreEqual(1, ValueOf(document, "Format"));
        Assert.AreEqual("Example", ValueOf(document, "Name"));
    }

    /// <summary>
    /// JSON is spotted from the first character that is not white space
    /// and is read into the same shape of mapping as YAML.
    /// </summary>
    [TestMethod]
    public void Parse_Json_IsDetectedAndReadIntoAMapping()
    {
        var text = "\n\n  {\"Format\": 1, \"Name\": \"Example\"}";

        Assert.IsTrue(DerivedScriptParser.LooksLikeJson(text));

        var document = DerivedScriptParser.Parse(text);
        CollectionAssert.AreEqual(
            new[] { "Format", "Name" },
            new List<string>(document.Names));
        Assert.AreEqual(1, ValueOf(document, "Format"));
        Assert.AreEqual("Example", ValueOf(document, "Name"));
    }

    /// <summary>
    /// The same script written as YAML and as JSON gives an equal tree,
    /// which is what makes the two formats interchangeable.
    /// </summary>
    [TestMethod]
    public void Parse_YamlAndJsonOfOneScript_GiveAnEqualTree()
    {
        var yaml = DerivedScriptParser.Parse(
            "Format: 1\nRules:\n  - Else: High\n");
        var json = DerivedScriptParser.Parse(
            "{\"Format\":1,\"Rules\":[{\"Else\":\"High\"}]}");

        AssertSameTree(yaml, json, "document");
    }

    /// <summary>
    /// Every mapping and every list carries the line it started on, so a
    /// validation fault can point at a line.
    ///
    /// .NET reports the line the first key of a mapping sits on, and the
    /// line the first entry of a list sits on, which is where YamlDotNet
    /// places the start of the node. The JavaScript reference reports the
    /// line of the key that introduced the mapping or the list instead, so
    /// the two disagree by a line for a block mapping or list written under
    /// its key. Both point at the same script, so a reader is led to the
    /// right place either way.
    /// </summary>
    [TestMethod]
    public void Parse_Yaml_RecordsTheLineOfEachMappingAndList()
    {
        var document = DerivedScriptParser.Parse(string.Join("\n", new[]
        {
            "Format: 1",           // line 1
            "Rules:",              // line 2
            "  - When:",           // line 3
            "      Property: a.B", // line 4
            "      Eq: true",      // line 5
            "    Then: High"       // line 6
        }));

        Assert.AreEqual(1, document.Line);
        var rules = (DerivedSequence)document.Get("Rules");
        Assert.AreEqual(3, rules.Line);
        var rule = (DerivedMapping)rules.Items[0];
        Assert.AreEqual(3, rule.Line);
        var when = (DerivedMapping)rule.Get("When");
        Assert.AreEqual(4, when.Line);
        Assert.AreEqual(4, ((DerivedScalar)when.Get("Property")).Line);
        Assert.AreEqual(5, ((DerivedScalar)when.Get("Eq")).Line);
        Assert.AreEqual(6, ((DerivedScalar)rule.Get("Then")).Line);
    }

    /// <summary>
    /// YAML that cannot be read raises a parse failure carrying a line.
    /// </summary>
    [TestMethod]
    public void Parse_UnreadableYaml_RaisesAParseFailureWithALine()
    {
        var exception = Assert.ThrowsExactly<DerivedScriptParseException>(
            () => DerivedScriptParser.Parse("Format: 1\nName: [unclosed\n"));

        Assert.IsGreaterThan(
            0,
            exception.Line,
            "the failure carries no line, the message was " +
            exception.Message);
        StringAssert.Contains(exception.Message, "not valid YAML");
    }

    /// <summary>
    /// JSON that cannot be read raises a parse failure, and the message
    /// says JSON rather than YAML because the text opens with a brace.
    /// </summary>
    [TestMethod]
    public void Parse_UnreadableJson_RaisesAParseFailure()
    {
        var exception = Assert.ThrowsExactly<DerivedScriptParseException>(
            () => DerivedScriptParser.Parse("{\"Format\": 1"));

        StringAssert.Contains(exception.Message, "not valid JSON");
    }

    /// <summary>
    /// A script has to be a mapping at the top level, so a list is a parse
    /// failure rather than something validation later reports.
    /// </summary>
    [TestMethod]
    public void Parse_ADocumentThatIsNotAMapping_RaisesAParseFailure()
    {
        var exception = Assert.ThrowsExactly<DerivedScriptParseException>(
            () => DerivedScriptParser.Parse("- one\n- two\n"));

        StringAssert.Contains(
            exception.Message, "must be a mapping of keys to values");
    }

    /// <summary>
    /// A key written twice in one mapping is a parse failure, because the
    /// author cannot have meant both and quietly keeping the last one hides
    /// the mistake.
    /// </summary>
    [TestMethod]
    public void Parse_ADuplicateKey_RaisesAParseFailure()
    {
        var exception = Assert.ThrowsExactly<DerivedScriptParseException>(
            () => DerivedScriptParser.Parse("Name: One\nName: Two\n"));

        StringAssert.Contains(exception.Message, "Duplicate key");
    }

    // ------------------------------------------------------------------
    // Cases the JavaScript file cannot express.
    // ------------------------------------------------------------------

    /// <summary>
    /// Keys are matched without regard to case, so two keys that differ
    /// only in case are the same key written twice.
    /// </summary>
    [TestMethod]
    public void Parse_ADuplicateKeyDifferingOnlyInCase_RaisesAParseFailure()
    {
        var exception = Assert.ThrowsExactly<DerivedScriptParseException>(
            () => DerivedScriptParser.Parse("Name: One\nname: Two\n"));

        StringAssert.Contains(
            exception.Message, "written more than once in the same mapping");
        Assert.AreEqual(2, exception.Line);
    }

    /// <summary>
    /// White space before the opening brace does not stop the text being
    /// spotted as JSON.
    /// </summary>
    [TestMethod]
    public void LooksLikeJson_LeadingWhiteSpace_IsIgnored()
    {
        Assert.IsTrue(DerivedScriptParser.LooksLikeJson("  {\"A\": 1}"));
        Assert.IsTrue(DerivedScriptParser.LooksLikeJson("\n\n\t {\"A\": 1}"));
        Assert.IsTrue(DerivedScriptParser.LooksLikeJson("{\"A\": 1}"));
    }

    /// <summary>
    /// A script is a mapping, so text opening with a list bracket is not
    /// JSON as far as the format is concerned. The value is only used to
    /// name the format in a failure message.
    /// </summary>
    [TestMethod]
    public void LooksLikeJson_ALeadingBracket_IsNotJson()
    {
        Assert.IsFalse(DerivedScriptParser.LooksLikeJson("[1, 2]"));
        Assert.IsFalse(DerivedScriptParser.LooksLikeJson("  [1, 2]"));
        Assert.IsFalse(DerivedScriptParser.LooksLikeJson("Format: 1"));
        Assert.IsFalse(DerivedScriptParser.LooksLikeJson("   "));
        Assert.IsFalse(DerivedScriptParser.LooksLikeJson(null));
    }

    /// <summary>
    /// Quoting is how an author keeps the word true as text. Written plain
    /// the same word is a boolean.
    /// </summary>
    [TestMethod]
    public void Parse_AQuotedTrue_StaysAString()
    {
        var document = DerivedScriptParser.Parse(
            "Plain: true\nQuoted: \"true\"\nSingle: 'true'\n");

        AssertValue(document, "Plain", typeof(bool), true);
        AssertValue(document, "Quoted", typeof(string), "true");
        AssertValue(document, "Single", typeof(string), "true");
    }

    /// <summary>
    /// Quoting is also how an author keeps a number as text.
    /// </summary>
    [TestMethod]
    public void Parse_AQuotedNumber_StaysAString()
    {
        var document = DerivedScriptParser.Parse(
            "Plain: 8\nQuoted: \"8\"\nSingle: '8'\n");

        AssertValue(document, "Plain", typeof(int), 8);
        AssertValue(document, "Quoted", typeof(string), "8");
        AssertValue(document, "Single", typeof(string), "8");
    }

    /// <summary>
    /// The four ways YAML writes nothing all read as nothing, and the
    /// validator turns each into the same fault about a null literal.
    /// </summary>
    [TestMethod]
    public void Parse_TheWaysOfWritingNothing_AllReadAsNothing()
    {
        var document = DerivedScriptParser.Parse(string.Join("\n", new[]
        {
            "A: null",
            "B: Null",
            "C: NULL",
            "D: ~",
            "E:"
        }));

        foreach (var name in document.Names)
        {
            var scalar = (DerivedScalar)document.Get(name);
            Assert.IsNull(
                scalar.Value,
                "the key " + name + " was read as " + scalar.Value);
        }
    }

    /// <summary>
    /// The numeric rule, which is the one most likely to drift between
    /// languages. A number with nothing after the decimal point stands for
    /// a whole number, so 8, 8.0 and 8.00 all read as the int 8 and infer
    /// the type int, whilst 8.5 reads as the double 8.5 and infers double.
    /// DESIGN.md 2.3 and docs/format-1.md both say so, and the rule is
    /// written this way because several YAML libraries do not hand back the
    /// text a value was written as.
    /// </summary>
    [TestMethod]
    public void Parse_AWholeNumberWrittenWithADecimalPoint_ReadsAsAnInt()
    {
        var document = DerivedScriptParser.Parse(string.Join("\n", new[]
        {
            "A: 8",
            "B: 8.0",
            "C: 8.00",
            "D: 8.5",
            "E: -8.0",
            "F: 0.5"
        }));

        AssertValue(document, "A", typeof(int), 8);
        AssertValue(document, "B", typeof(int), 8);
        AssertValue(document, "C", typeof(int), 8);
        AssertValue(document, "D", typeof(double), 8.5d);
        AssertValue(document, "E", typeof(int), -8);
        AssertValue(document, "F", typeof(double), 0.5d);
    }

    /// <summary>
    /// The text a value was written as is kept beside the value, so a
    /// fault message can quote the script rather than the reading of it.
    /// </summary>
    [TestMethod]
    public void Parse_TheTextOfAValue_IsKeptBesideTheValue()
    {
        var document = DerivedScriptParser.Parse("A: 8.00\nB: \"8\"\n");

        Assert.AreEqual("8.00", ((DerivedScalar)document.Get("A")).Text);
        Assert.AreEqual("8", ((DerivedScalar)document.Get("B")).Text);
    }

    // ------------------------------------------------------------------
    // Helpers.
    // ------------------------------------------------------------------

    private static object ValueOf(DerivedMapping mapping, string name)
    {
        return ((DerivedScalar)mapping.Get(name)).Value;
    }

    private static void AssertValue(
        DerivedMapping mapping,
        string name,
        Type expectedType,
        object expected)
    {
        var value = ValueOf(mapping, name);
        Assert.IsNotNull(value, "the key " + name + " was read as nothing");
        Assert.AreEqual(
            expectedType,
            value.GetType(),
            "the key " + name + " was read as a " + value.GetType().Name);
        Assert.AreEqual(expected, value, "the key " + name);
    }

    /// <summary>
    /// Whether two trees hold the same names, the same order and the same
    /// values read as the same types. Lines are not compared, because the
    /// two texts are written differently.
    /// </summary>
    private static void AssertSameTree(
        DerivedNode left,
        DerivedNode right,
        string path)
    {
        if (left is DerivedMapping leftMapping)
        {
            var rightMapping = right as DerivedMapping;
            Assert.IsNotNull(rightMapping, path + " is not a mapping on both");
            CollectionAssert.AreEqual(
                new List<string>(leftMapping.Names),
                new List<string>(rightMapping.Names),
                path + " has different names");
            foreach (var name in leftMapping.Names)
            {
                AssertSameTree(
                    leftMapping.Get(name),
                    rightMapping.Get(name),
                    path + "." + name);
            }
            return;
        }
        if (left is DerivedSequence leftSequence)
        {
            var rightSequence = right as DerivedSequence;
            Assert.IsNotNull(rightSequence, path + " is not a list on both");
            Assert.HasCount(
                leftSequence.Items.Count,
                rightSequence.Items,
                path + " has a different length");
            for (var i = 0; i < leftSequence.Items.Count; i++)
            {
                AssertSameTree(
                    leftSequence.Items[i],
                    rightSequence.Items[i],
                    path + "[" + i + "]");
            }
            return;
        }
        var leftScalar = (DerivedScalar)left;
        var rightScalar = right as DerivedScalar;
        Assert.IsNotNull(rightScalar, path + " is not a value on both");
        Assert.AreEqual(
            leftScalar.Value,
            rightScalar.Value,
            path + " holds a different value");
        Assert.AreEqual(
            leftScalar.Value == null ? null : leftScalar.Value.GetType(),
            rightScalar.Value == null ? null : rightScalar.Value.GetType(),
            path + " holds a value of a different type");
    }
}
