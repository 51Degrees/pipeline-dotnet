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
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.DerivedProperty.Data;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FiftyOne.Pipeline.DerivedProperty.Tests;

/// <summary>
/// Runs scripts against stub source elements and checks the answers.
///
/// The cases here mirror test/evaluate.test.mjs of the derived-properties
/// repository one for one, because that file is the specification every
/// language implementation has to agree with. Where a case is stronger
/// here than in the JavaScript, the reason is written above the test.
/// </summary>
[TestClass]
public class EvaluationTests
{
    // -----------------------------------------------------------------
    // Every operator on every type it is allowed on.
    // -----------------------------------------------------------------

    /// <summary>
    /// Eq and Ne read a boolean property.
    /// </summary>
    [TestMethod]
    public void Evaluation_EqAndNeOnBool()
    {
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, Eq: true }", Values("a.P", true)));
        Assert.AreEqual("no", RunCondition(
            "{ Property: a.P, Eq: true }", Values("a.P", false)));
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, Ne: true }", Values("a.P", false)));
        Assert.AreEqual("no", RunCondition(
            "{ Property: a.P, Ne: true }", Values("a.P", true)));
    }

    /// <summary>
    /// Eq and Ne read a whole number property.
    /// </summary>
    [TestMethod]
    public void Evaluation_EqAndNeOnInt()
    {
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, Eq: 8 }", Values("a.P", 8)));
        Assert.AreEqual("no", RunCondition(
            "{ Property: a.P, Eq: 8 }", Values("a.P", 9)));
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, Ne: 8 }", Values("a.P", 9)));
    }

    /// <summary>
    /// Eq and Ne read a property holding a number with a fractional part.
    /// </summary>
    [TestMethod]
    public void Evaluation_EqAndNeOnDouble()
    {
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, Eq: 1.5 }", Values("a.P", 1.5)));
        Assert.AreEqual("no", RunCondition(
            "{ Property: a.P, Eq: 1.5 }", Values("a.P", 1.75)));
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, Ne: 1.5 }", Values("a.P", 1.75)));
    }

    /// <summary>
    /// Eq and Ne read a text property. Text is compared ordinally and with
    /// regard to case, so "None" and "none" are different values.
    /// </summary>
    [TestMethod]
    public void Evaluation_EqAndNeOnString()
    {
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, Eq: \"None\" }", Values("a.P", "None")));
        Assert.AreEqual("no", RunCondition(
            "{ Property: a.P, Eq: \"None\" }", Values("a.P", "none")));
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, Ne: \"None\" }", Values("a.P", "none")));
    }

    /// <summary>
    /// Gt, Ge, Lt and Le read a whole number property.
    /// </summary>
    [TestMethod]
    public void Evaluation_GtGeLtLeOnInt()
    {
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, Gt: 0 }", Values("a.P", 1)));
        Assert.AreEqual("no", RunCondition(
            "{ Property: a.P, Gt: 0 }", Values("a.P", 0)));
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, Ge: 8 }", Values("a.P", 8)));
        Assert.AreEqual("no", RunCondition(
            "{ Property: a.P, Ge: 8 }", Values("a.P", 7)));
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, Lt: 2 }", Values("a.P", 1)));
        Assert.AreEqual("no", RunCondition(
            "{ Property: a.P, Lt: 2 }", Values("a.P", 2)));
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, Le: 2 }", Values("a.P", 2)));
        Assert.AreEqual("no", RunCondition(
            "{ Property: a.P, Le: 2 }", Values("a.P", 3)));
    }

    /// <summary>
    /// Gt, Ge, Lt and Le read a property holding a number with a
    /// fractional part.
    /// </summary>
    [TestMethod]
    public void Evaluation_GtGeLtLeOnDouble()
    {
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, Gt: 0.5 }", Values("a.P", 0.6)));
        Assert.AreEqual("no", RunCondition(
            "{ Property: a.P, Gt: 0.5 }", Values("a.P", 0.5)));
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, Ge: 0.5 }", Values("a.P", 0.5)));
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, Lt: 0.5 }", Values("a.P", 0.25)));
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, Le: 0.5 }", Values("a.P", 0.5)));
        Assert.AreEqual("no", RunCondition(
            "{ Property: a.P, Le: 0.5 }", Values("a.P", 0.75)));
    }

    /// <summary>
    /// In and NotIn read a list of text values.
    /// </summary>
    [TestMethod]
    public void Evaluation_InAndNotInOnString()
    {
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, In: [\"A\", \"B\"] }", Values("a.P", "B")));
        Assert.AreEqual("no", RunCondition(
            "{ Property: a.P, In: [\"A\", \"B\"] }", Values("a.P", "C")));
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, NotIn: [\"A\", \"B\"] }", Values("a.P", "C")));
        Assert.AreEqual("no", RunCondition(
            "{ Property: a.P, NotIn: [\"A\", \"B\"] }", Values("a.P", "A")));
    }

    /// <summary>
    /// In and NotIn read a list of whole numbers.
    /// </summary>
    [TestMethod]
    public void Evaluation_InAndNotInOnInt()
    {
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, In: [1, 2, 3] }", Values("a.P", 2)));
        Assert.AreEqual("no", RunCondition(
            "{ Property: a.P, In: [1, 2, 3] }", Values("a.P", 4)));
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, NotIn: [1, 2, 3] }", Values("a.P", 4)));
    }

    /// <summary>
    /// In and NotIn read a list of booleans.
    /// </summary>
    [TestMethod]
    public void Evaluation_InAndNotInOnBool()
    {
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, In: [true] }", Values("a.P", true)));
        Assert.AreEqual("no", RunCondition(
            "{ Property: a.P, In: [true] }", Values("a.P", false)));
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, NotIn: [true] }", Values("a.P", false)));
    }

    /// <summary>
    /// StartsWith, EndsWith and Contains read text ordinally and with
    /// regard to case.
    /// </summary>
    [TestMethod]
    public void Evaluation_StartsWithEndsWithAndContainsOnString()
    {
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, StartsWith: \"Chr\" }",
            Values("a.P", "Chrome")));
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, EndsWith: \"ome\" }",
            Values("a.P", "Chrome")));
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, Contains: \"hro\" }",
            Values("a.P", "Chrome")));
        Assert.AreEqual("no", RunCondition(
            "{ Property: a.P, StartsWith: \"chr\" }",
            Values("a.P", "Chrome")));
        Assert.AreEqual("no", RunCondition(
            "{ Property: a.P, EndsWith: \"OME\" }",
            Values("a.P", "Chrome")));
        Assert.AreEqual("no", RunCondition(
            "{ Property: a.P, Contains: \"HRO\" }",
            Values("a.P", "Chrome")));
    }

    // -----------------------------------------------------------------
    // The two valued table of DESIGN.md 2.6. Every condition is true or
    // false, because the rules only run once every source property the
    // script names has been read.
    // -----------------------------------------------------------------

    /// <summary>
    /// All is true only where every member is true.
    /// </summary>
    [TestMethod]
    public void Evaluation_AllIsTrueOnlyWhereEveryMemberIsTrue()
    {
        var condition = "{ All: [ { Property: a.P, Eq: true }, " +
            "{ Property: a.Q, Eq: true } ] }";
        Assert.AreEqual("yes", RunCondition(
            condition, Values("a.P", true, "a.Q", true)));
        Assert.AreEqual("no", RunCondition(
            condition, Values("a.P", true, "a.Q", false)));
        Assert.AreEqual("no", RunCondition(
            condition, Values("a.P", false, "a.Q", true)));
        Assert.AreEqual("no", RunCondition(
            condition, Values("a.P", false, "a.Q", false)));
    }

    /// <summary>
    /// Any is true where at least one member is true.
    /// </summary>
    [TestMethod]
    public void Evaluation_AnyIsTrueWhereAtLeastOneMemberIsTrue()
    {
        var condition = "{ Any: [ { Property: a.P, Eq: true }, " +
            "{ Property: a.Q, Eq: true } ] }";
        Assert.AreEqual("yes", RunCondition(
            condition, Values("a.P", true, "a.Q", false)));
        Assert.AreEqual("yes", RunCondition(
            condition, Values("a.P", false, "a.Q", true)));
        Assert.AreEqual("no", RunCondition(
            condition, Values("a.P", false, "a.Q", false)));
    }

    /// <summary>
    /// Not turns true into false and false into true.
    /// </summary>
    [TestMethod]
    public void Evaluation_NotInvertsItsCondition()
    {
        Assert.AreEqual("no", RunCondition(
            "{ Not: { Property: a.P, Eq: true } }", Values("a.P", true)));
        Assert.AreEqual("yes", RunCondition(
            "{ Not: { Property: a.P, Eq: true } }", Values("a.P", false)));
    }

    /// <summary>
    /// A Check reference gives back the answer of the check it names.
    /// </summary>
    [TestMethod]
    public void Evaluation_CheckReferenceGivesTheReferencedAnswer()
    {
        var checks = "Checks:\n  One: { Property: a.P, Eq: true }\n";
        Assert.AreEqual("yes", RunCondition(
            "{ Check: One }", Values("a.P", true), checks));
        Assert.AreEqual("no", RunCondition(
            "{ Check: One }", Values("a.P", false), checks));
    }

    // -----------------------------------------------------------------
    // The conversion table of DESIGN.md section 3, read straight from the
    // converter and then again through a script.
    // -----------------------------------------------------------------

    /// <summary>
    /// A bool is read from a native boolean and from the words true and
    /// false in any letter case with surrounding white space.
    /// </summary>
    [TestMethod]
    public void Conversion_BoolFromNativeAndFromText()
    {
        Assert.IsTrue(DerivedValueConverter.TryConvert(
            true, DerivedValueType.Bool, out var native));
        Assert.IsTrue((bool)native);
        Assert.IsTrue(DerivedValueConverter.TryConvert(
            false, DerivedValueType.Bool, out var nativeFalse));
        Assert.IsFalse((bool)nativeFalse);

        foreach (var text in new[] { "true", "TRUE", "True", " true " })
        {
            Assert.IsTrue(DerivedValueConverter.TryConvertString(
                text, DerivedValueType.Bool, out var value), text);
            Assert.IsTrue((bool)value, text);
        }
        foreach (var text in new[] { "false", "FALSE", "  false  " })
        {
            Assert.IsTrue(DerivedValueConverter.TryConvertString(
                text, DerivedValueType.Bool, out var value), text);
            Assert.IsFalse((bool)value, text);
        }
    }

    /// <summary>
    /// A bool refuses everything else, so N/A and Unknown never quietly
    /// become false.
    /// </summary>
    [TestMethod]
    public void Conversion_BoolRefusesAnythingElse()
    {
        foreach (var text in new[] { "N/A", "Unknown", "1", "yes", "" })
        {
            Assert.IsFalse(DerivedValueConverter.TryConvertString(
                text, DerivedValueType.Bool, out _), text);
        }
    }

    /// <summary>
    /// An int is read from every native whole number type and from a
    /// string of digits with an optional sign.
    /// </summary>
    [TestMethod]
    public void Conversion_IntFromNativeAndFromText()
    {
        var natives = new object[]
        {
            (sbyte)8, (byte)8, (short)8, (ushort)8,
            8, (uint)8, (long)8, (ulong)8
        };
        foreach (var native in natives)
        {
            Assert.IsTrue(DerivedValueConverter.TryConvert(
                native, DerivedValueType.Int, out var value),
                native.GetType().Name);
            Assert.AreEqual(8, value, native.GetType().Name);
        }
        Assert.IsTrue(DerivedValueConverter.TryConvertString(
            "-8", DerivedValueType.Int, out var negative));
        Assert.AreEqual(-8, negative);
        Assert.IsTrue(DerivedValueConverter.TryConvertString(
            "+8", DerivedValueType.Int, out var positive));
        Assert.AreEqual(8, positive);
    }

    /// <summary>
    /// An int refuses a number written with a decimal point, and refuses
    /// text that is not a number at all.
    /// </summary>
    [TestMethod]
    public void Conversion_IntRefusesAnythingElse()
    {
        foreach (var text in new[] { "1.0", "Unknown", "" })
        {
            Assert.IsFalse(DerivedValueConverter.TryConvertString(
                text, DerivedValueType.Int, out _), text);
        }
        Assert.IsFalse(DerivedValueConverter.TryConvert(
            1.5, DerivedValueType.Int, out _));
    }

    /// <summary>
    /// A double is read from whole and fractional native numbers and from
    /// a decimal string carrying a sign and an exponent.
    /// </summary>
    [TestMethod]
    public void Conversion_DoubleFromNativeAndFromText()
    {
        Assert.IsTrue(DerivedValueConverter.TryConvert(
            1, DerivedValueType.Double, out var whole));
        Assert.AreEqual(1.0, whole);
        Assert.IsTrue(DerivedValueConverter.TryConvert(
            (long)1, DerivedValueType.Double, out var wholeLong));
        Assert.AreEqual(1.0, wholeLong);
        Assert.IsTrue(DerivedValueConverter.TryConvert(
            1.5, DerivedValueType.Double, out var fraction));
        Assert.AreEqual(1.5, fraction);
        Assert.IsTrue(DerivedValueConverter.TryConvert(
            1.5f, DerivedValueType.Double, out var single));
        Assert.AreEqual(1.5, single);
        Assert.IsTrue(DerivedValueConverter.TryConvertString(
            "-1.5e2", DerivedValueType.Double, out var exponent));
        Assert.AreEqual(-150.0, exponent);
    }

    /// <summary>
    /// A double refuses text that is not a number.
    /// </summary>
    [TestMethod]
    public void Conversion_DoubleRefusesAnythingElse()
    {
        foreach (var text in new[] { "Unknown", "" })
        {
            Assert.IsFalse(DerivedValueConverter.TryConvertString(
                text, DerivedValueType.Double, out _), text);
        }
    }

    /// <summary>
    /// Text is taken exactly as it arrived, and a native boolean or number
    /// takes its canonical written form.
    /// </summary>
    [TestMethod]
    public void Conversion_StringTakesValuesAsTheyAre()
    {
        Assert.IsTrue(DerivedValueConverter.TryConvert(
            "N/A", DerivedValueType.String, out var text));
        Assert.AreEqual("N/A", text);
        Assert.IsTrue(DerivedValueConverter.TryConvert(
            true, DerivedValueType.String, out var yes));
        Assert.AreEqual("True", yes);
        Assert.IsTrue(DerivedValueConverter.TryConvert(
            false, DerivedValueType.String, out var no));
        Assert.AreEqual("False", no);
        Assert.IsTrue(DerivedValueConverter.TryConvert(
            8, DerivedValueType.String, out var number));
        Assert.AreEqual("8", number);
    }

    /// <summary>
    /// The written form of a value gives the same answer as the native
    /// form when a script reads it.
    /// </summary>
    [TestMethod]
    public void Conversion_WrittenFormReadsTheSameAsTheNativeForm()
    {
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, Eq: false }", Values("a.P", "False")));
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, Ge: 8 }", Values("a.P", "9")));
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, Le: 0.5 }", Values("a.P", "0.25")));
    }

    /// <summary>
    /// A list of weighted values takes the value carrying the highest
    /// weight, which is how the 51Degrees data files hand back a property
    /// that has more than one candidate answer.
    /// </summary>
    [TestMethod]
    public void Conversion_WeightedValuesTakeTheHighestWeight()
    {
        var weighted = new List<WeightedValue<string>>
        {
            new WeightedValue<string>(1, "Low"),
            new WeightedValue<string>(5, "High")
        };
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, Eq: \"High\" }", Values("a.P", weighted)));
        Assert.AreEqual("no", RunCondition(
            "{ Property: a.P, Eq: \"Low\" }", Values("a.P", weighted)));
    }

    /// <summary>
    /// A plain list where the script needs one value is not a value the
    /// property can hold, so the property is absent and the script
    /// produces no value.
    /// </summary>
    [TestMethod]
    public void Conversion_PlainListWhereOneValueIsNeededIsInvalid()
    {
        var list = new List<string> { "one", "two" };
        var value = Run(
            ConditionScript("{ Property: a.P, Eq: \"one\" }", string.Empty),
            "Probe",
            Values("a.P", list));
        Assert.IsFalse(value.HasValue);
        Assert.Contains(
            "'a.P' (held a list where a single value is needed).",
            value.NoValueMessage);
    }

    /// <summary>
    /// A source value that carries its own reason for having no value
    /// hands that reason on, so the message reaches back to the element
    /// that actually knows why.
    /// </summary>
    [TestMethod]
    public void Conversion_SourceNoValueMessageIsHandedOn()
    {
        var absent = new AspectPropertyValue<string>
        {
            NoValueMessage = "the JavaScript has not run yet"
        };
        var trace = new DerivedTrace();
        Run(
            ConditionScript(
                "{ Property: a.P, Eq: \"None\" }", string.Empty),
            "Probe",
            Values("a.P", absent),
            trace);
        Assert.HasCount(1, trace.Properties);
        Assert.IsFalse(trace.Properties[0].Available);
        Assert.AreEqual(
            "element 'a' has no value for 'P': " +
            "the JavaScript has not run yet",
            trace.Properties[0].Reason);
    }

    // -----------------------------------------------------------------
    // Aggregates.
    // -----------------------------------------------------------------

    private const string AggregateChecks =
        "Checks:\n" +
        "  One:   { Property: a.P, Eq: true }\n" +
        "  Two:   { Property: a.Q, Eq: true }\n" +
        "  Three: { Property: a.R, Eq: true }\n";

    /// <summary>
    /// Passed and Failed count the checks that were true and the checks
    /// that were false, and always add up to the size of the group.
    /// </summary>
    [TestMethod]
    public void Aggregate_PassedAndFailedAddUpToTheGroup()
    {
        var values = Values("a.P", true, "a.Q", false, "a.R", false);
        Assert.AreEqual("yes", RunCondition(
            "{ Passed: Checks, Eq: 1 }", values, AggregateChecks));
        Assert.AreEqual("yes", RunCondition(
            "{ Failed: Checks, Eq: 2 }", values, AggregateChecks));
        Assert.AreEqual("no", RunCondition(
            "{ Passed: Checks, Eq: 2 }", values, AggregateChecks));
    }

    /// <summary>
    /// An aggregate over a named list of checks counts only the checks in
    /// that list.
    /// </summary>
    [TestMethod]
    public void Aggregate_OverANamedListOfChecksCountsOnlyThose()
    {
        var values = Values("a.P", true, "a.Q", true, "a.R", false);
        Assert.AreEqual("yes", RunCondition(
            "{ Passed: [One, Two], Eq: 2 }", values, AggregateChecks));
        Assert.AreEqual("yes", RunCondition(
            "{ Failed: [One, Two], Eq: 0 }", values, AggregateChecks));
        Assert.AreEqual("yes", RunCondition(
            "{ Failed: Checks, Eq: 1 }", values, AggregateChecks));
    }

    /// <summary>
    /// One aggregate may be compared with another, which is how a script
    /// asks whether more checks passed than failed.
    /// </summary>
    [TestMethod]
    public void Aggregate_MayBeComparedWithAnotherAggregate()
    {
        Assert.AreEqual("yes", RunCondition(
            "{ Passed: Checks, Gt: { Failed: Checks } }",
            Values("a.P", true, "a.Q", true, "a.R", false),
            AggregateChecks));
        Assert.AreEqual("no", RunCondition(
            "{ Passed: Checks, Gt: { Failed: Checks } }",
            Values("a.P", true, "a.Q", false, "a.R", false),
            AggregateChecks));
    }

    // -----------------------------------------------------------------
    // Absent source properties.
    // -----------------------------------------------------------------

    private const string StrictScript = @"
Format: 1
Name: Strict
Version: 1.0.0
Output:
  Name: Strict
  Description: A property computed from two source properties.
  ValueType: string
  IsList: false
Rules:
  - When:
      All:
        - { Property: device.IsVisible, Eq: true }
        - { Property: device.WebDriver, Eq: ""None"" }
    Then: High
  - Else: Low
";

    /// <summary>
    /// A source property that is not there makes the output a value with
    /// no value rather than a guess.
    /// </summary>
    [TestMethod]
    public void Missing_AbsentPropertyGivesAValueWithNoValue()
    {
        var value = Run(
            StrictScript,
            "Strict",
            Values("device.WebDriver", "None"));
        Assert.IsFalse(value.HasValue);
        Assert.Contains("'device.IsVisible'", value.NoValueMessage);
    }

    /// <summary>
    /// The message names every absent property and not only the first. The
    /// wording is the contract between the languages, so the whole
    /// sentence is asserted rather than a fragment of it, and the closing
    /// sentence is taken from the constant every language shares so that
    /// changing the wording fails here first.
    /// </summary>
    [TestMethod]
    public void Missing_MessageNamesEveryAbsentProperty()
    {
        var value = Run(StrictScript, "Strict", Values());
        Assert.IsFalse(value.HasValue);
        var expected =
            "Derived property 'Strict' has no value because 2 source " +
            "properties were not available. 'device.IsVisible' (element " +
            "'device' has no value for 'IsVisible': property not present " +
            "on this request). 'device.WebDriver' (element 'device' has " +
            "no value for 'WebDriver': property not present on this " +
            "request). " + CompiledScript.UsualCauses;
        Assert.AreEqual(expected, value.NoValueMessage);
    }

    /// <summary>
    /// The message counts in the singular where one property is absent.
    /// </summary>
    [TestMethod]
    public void Missing_MessageIsSingularForOneProperty()
    {
        var value = Run(
            StrictScript,
            "Strict",
            Values("device.IsVisible", true));
        Assert.IsFalse(value.HasValue);
        Assert.Contains(
            "because 1 source property was not available.",
            value.NoValueMessage);
    }

    /// <summary>
    /// Where the source value carries its own reason, that reason is what
    /// the message gives.
    /// </summary>
    [TestMethod]
    public void Missing_MessageCarriesTheSourceNoValueMessage()
    {
        var value = Run(
            StrictScript,
            "Strict",
            Values(
                "device.IsVisible", true,
                "device.WebDriver", new AspectPropertyValue<string>
                {
                    NoValueMessage = "the JavaScript has not run yet"
                }));
        Assert.IsFalse(value.HasValue);
        Assert.Contains(
            "'device.WebDriver' (element 'device' has no value for " +
            "'WebDriver': the JavaScript has not run yet).",
            value.NoValueMessage);
    }

    /// <summary>
    /// A value that cannot be read says what it held and what it could not
    /// be read as.
    /// </summary>
    [TestMethod]
    public void Missing_MessageSaysWhatAnInvalidValueHeld()
    {
        var value = Run(
            StrictScript,
            "Strict",
            Values(
                "device.IsVisible", "N/A",
                "device.WebDriver", "None"));
        Assert.IsFalse(value.HasValue);
        Assert.Contains(
            "'device.IsVisible' (held 'N/A' which cannot be read as bool).",
            value.NoValueMessage);
    }

    /// <summary>
    /// Nothing about the rules is read where a source property is absent,
    /// so the trace holds no checks and names no rule.
    /// </summary>
    [TestMethod]
    public void Missing_NoCheckIsEvaluatedAndNoRuleIsReached()
    {
        var text = @"
Format: 1
Name: Halted
Version: 1.0.0
Output:
  Name: Halted
  Description: A property whose checks are never reached.
  ValueType: string
  IsList: false
Checks:
  One: { Property: a.P, Eq: true }
Rules:
  - When: { Check: One }
    Then: High
  - Else: Low
";
        var trace = new DerivedTrace();
        var value = Run(text, "Halted", Values(), trace);

        Assert.IsFalse(value.HasValue);
        Assert.IsEmpty(trace.Checks);
        Assert.IsNull(trace.MatchedRule);
        Assert.AreEqual(value.NoValueMessage, trace.NoValueMessage);
    }

    // -----------------------------------------------------------------
    // Rule order and Else.
    // -----------------------------------------------------------------

    /// <summary>
    /// Rules are read in order and the first one that holds supplies the
    /// answer.
    /// </summary>
    [TestMethod]
    public void Rules_FirstMatchWins()
    {
        var text = @"
Format: 1
Name: Ordered
Version: 1.0.0
Output:
  Name: Ordered
  Description: Which rule matched.
  ValueType: string
  IsList: false
Rules:
  - When: { Property: a.P, Ge: 1 }
    Then: First
  - When: { Property: a.P, Ge: 2 }
    Then: Second
  - Else: None
";
        Assert.AreEqual("First", TextOf(Run(
            text, "Ordered", Values("a.P", 5))));
        Assert.AreEqual("None", TextOf(Run(
            text, "Ordered", Values("a.P", 0))));
    }

    /// <summary>
    /// Output.DefaultValue is metadata carried through from the script and
    /// nothing reads it while a request is being processed, because every
    /// script ends in an Else and so always chooses a value.
    /// </summary>
    [TestMethod]
    public void Rules_DefaultValueIsNotReadWhileProcessing()
    {
        var text = @"
Format: 1
Name: Defaulted
Version: 1.0.0
Output:
  Name: Defaulted
  Description: A property carrying a default that nothing reads.
  ValueType: string
  IsList: false
  DefaultValue: Unknown
Rules:
  - When: { Property: a.P, Eq: true }
    Then: High
  - Else: Low
";
        var result = DerivedScriptValidator.Validate(text, "Defaulted", "code");
        Assert.IsTrue(result.IsValid,
            DerivedScriptValidationException.Describe(result.Faults));
        Assert.AreEqual("Unknown", result.Script.Output.DefaultValue);

        // The Else answers where no earlier rule matched, so the default
        // never reaches the value the element writes.
        Assert.AreEqual("Low", TextOf(Run(
            text, "Defaulted", Values("a.P", false))));
    }

    /// <summary>
    /// A script whose rules do not end in an Else cannot be built by the
    /// validator, so reaching the evaluator with one says a script was
    /// built by hand. The evaluator says so rather than answering.
    /// </summary>
    [TestMethod]
    public void Rules_AModelWithNoElseRaisesRatherThanAnswering()
    {
        var result = DerivedScriptValidator.Validate(
            StrictScript, "Strict", "code");
        Assert.IsTrue(result.IsValid,
            DerivedScriptValidationException.Describe(result.Faults));
        var script = result.Script;
        var withoutElse = new DerivedScript(
            script.Format,
            script.Name,
            script.Version,
            script.Deprecated,
            script.DeprecationNote,
            script.Source,
            script.Output,
            script.Properties,
            script.Checks,
            script.Rules.Where(r => r.IsElse == false).ToList());
        var compiled = new CompiledScript(withoutElse);
        var values = Values(
            "device.IsVisible", false, "device.WebDriver", "None");

        using (var pipeline = BuildSources(script, values))
        using (var data = pipeline.CreateFlowData())
        {
            data.Process();
            var exception = Assert.ThrowsExactly<InvalidOperationException>(
                () => compiled.Evaluate(data, null));
            Assert.Contains("do not end in an Else", exception.Message);
        }
    }

    // -----------------------------------------------------------------
    // The trace.
    // -----------------------------------------------------------------

    /// <summary>
    /// The trace names what every source property did, what every check
    /// answered, and which rule supplied the answer.
    /// </summary>
    [TestMethod]
    public void Trace_NamesEachCheckStateAndTheRuleThatMatched()
    {
        var text = @"
Format: 1
Name: Traced
Version: 1.0.0
Output:
  Name: Traced
  Description: A property whose evaluation is traced.
  ValueType: string
  IsList: false
Checks:
  One: { Property: a.P, Eq: true }
  Two: { Property: a.Q, Eq: true }
Rules:
  - When: { Passed: Checks, Ge: 1 }
    Then: High
  - Else: Low
";
        var trace = new DerivedTrace();
        var value = Run(
            text, "Traced", Values("a.P", true, "a.Q", false), trace);

        Assert.AreEqual("High", TextOf(value));

        Assert.HasCount(2, trace.Checks);
        Assert.AreEqual("One", trace.Checks[0].Name);
        Assert.IsTrue(trace.Checks[0].State);
        Assert.AreEqual("Two", trace.Checks[1].Name);
        Assert.IsFalse(trace.Checks[1].State);

        Assert.AreEqual(0, trace.MatchedRule);
        Assert.IsFalse(trace.MatchedElse);

        Assert.HasCount(2, trace.Properties);
        Assert.AreEqual("a.P", trace.Properties[0].Name);
        Assert.IsTrue(trace.Properties[0].Available);
        Assert.IsTrue((bool)trace.Properties[0].Value);
        Assert.IsNull(trace.Properties[0].Reason);
        Assert.AreEqual("a.Q", trace.Properties[1].Name);
        Assert.IsTrue(trace.Properties[1].Available);
        Assert.IsFalse((bool)trace.Properties[1].Value);
    }

    /// <summary>
    /// An Else that matches is recorded as an Else in the trace.
    /// </summary>
    [TestMethod]
    public void Trace_RecordsWhenTheElseMatched()
    {
        var trace = new DerivedTrace();
        Run(
            ConditionScript("{ Property: a.P, Eq: true }", string.Empty),
            "Probe",
            Values("a.P", false),
            trace);
        Assert.AreEqual(1, trace.MatchedRule);
        Assert.IsTrue(trace.MatchedElse);
    }

    // -----------------------------------------------------------------
    // Reading properties, and repeatability.
    // -----------------------------------------------------------------

    /// <summary>
    /// The element data key and the property name are both matched without
    /// regard to case, as the Pipeline does everywhere else.
    /// </summary>
    [TestMethod]
    public void Evaluation_PropertyLookupIgnoresCase()
    {
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, Eq: true }", Values("A.p", true)));
    }

    /// <summary>
    /// One compiled script gives the same answer however many times it is
    /// run, because nothing in it changes as it runs.
    /// </summary>
    [TestMethod]
    public void Evaluation_TheSameScriptGivesTheSameAnswerEveryTime()
    {
        var result = DerivedScriptValidator.Validate(
            StrictScript, "Strict", "code");
        Assert.IsTrue(result.IsValid,
            DerivedScriptValidationException.Describe(result.Faults));
        var compiled = new CompiledScript(result.Script);
        var values = Values(
            "device.IsVisible", true, "device.WebDriver", "None");

        using (var pipeline = BuildSources(result.Script, values))
        using (var data = pipeline.CreateFlowData())
        {
            data.Process();
            var first = TextOf(compiled.Evaluate(data, null));
            Assert.AreEqual("High", first);
            for (var i = 0; i < 100; i++)
            {
                Assert.AreEqual(first, TextOf(compiled.Evaluate(data, null)));
            }
        }
    }

    // -----------------------------------------------------------------
    // Helpers.
    // -----------------------------------------------------------------

    /// <summary>
    /// A script whose single rule gives one answer when the condition
    /// holds and another when it does not, so one condition can be read on
    /// its own.
    /// </summary>
    private static string ConditionScript(string condition, string checks)
    {
        return
            "Format: 1\n" +
            "Name: Probe\n" +
            "Version: 1.0.0\n" +
            "Output:\n" +
            "  Name: Probe\n" +
            "  Description: Whether the condition was true.\n" +
            "  ValueType: string\n" +
            "  IsList: false\n" +
            (checks ?? string.Empty) +
            "Rules:\n" +
            "  - When: " + condition + "\n" +
            "    Then: Yes it is\n" +
            "  - Else: No it is not\n";
    }

    /// <summary>
    /// Runs one condition and gives back "yes" where the rule matched and
    /// "no" where the Else was reached.
    /// </summary>
    private static string RunCondition(
        string condition,
        IDictionary<string, object> values,
        string checks = null)
    {
        var value = Run(
            ConditionScript(condition, checks), "Probe", values);
        var text = TextOf(value);
        if (string.Equals(text, "Yes it is", StringComparison.Ordinal))
        {
            return "yes";
        }
        if (string.Equals(text, "No it is not", StringComparison.Ordinal))
        {
            return "no";
        }
        return text;
    }

    /// <summary>
    /// Validates and compiles a script, then runs it once against a
    /// pipeline of stub source elements holding the values given.
    /// </summary>
    private static IAspectPropertyValue Run(
        string text,
        string name,
        IDictionary<string, object> values,
        DerivedTrace trace = null)
    {
        var result = DerivedScriptValidator.Validate(text, name, "code");
        Assert.IsTrue(result.IsValid,
            DerivedScriptValidationException.Describe(result.Faults));
        var compiled = new CompiledScript(result.Script);
        using (var pipeline = BuildSources(result.Script, values))
        using (var data = pipeline.CreateFlowData())
        {
            data.Process();
            return compiled.Evaluate(data, trace);
        }
    }

    /// <summary>
    /// A pipeline holding one stub element for each element data key the
    /// values or the script name, publishing the values given.
    /// </summary>
    private static IPipeline BuildSources(
        DerivedScript script,
        IDictionary<string, object> values)
    {
        var byElement =
            new Dictionary<string, Dictionary<string, object>>(
                StringComparer.OrdinalIgnoreCase);

        // The values are read first so that a test naming an element key
        // in a different letter case really publishes under that case.
        if (values != null)
        {
            foreach (var entry in values)
            {
                var dot = entry.Key.IndexOf('.');
                var elementKey = entry.Key.Substring(0, dot);
                var propertyName = entry.Key.Substring(dot + 1);
                if (byElement.TryGetValue(elementKey, out var holding) == false)
                {
                    holding = new Dictionary<string, object>(
                        StringComparer.OrdinalIgnoreCase);
                    byElement.Add(elementKey, holding);
                }
                holding[propertyName] = entry.Value;
            }
        }
        foreach (var property in script.Properties)
        {
            if (byElement.ContainsKey(property.ElementDataKey) == false)
            {
                byElement.Add(
                    property.ElementDataKey,
                    new Dictionary<string, object>(
                        StringComparer.OrdinalIgnoreCase));
            }
        }

        var loggerFactory = new TestLoggerFactory();
        var builder = new PipelineBuilder(loggerFactory);
        foreach (var element in byElement)
        {
            builder.AddFlowElement(new StubSourceElement(
                loggerFactory.CreateLogger<
                    FlowElementBase<StubSourceData, ElementPropertyMetaData>>(),
                element.Key,
                element.Value));
        }
        return builder.Build();
    }

    /// <summary>
    /// The values for one request, written as pairs of name and value.
    /// </summary>
    private static IDictionary<string, object> Values(params object[] pairs)
    {
        var values = new Dictionary<string, object>(
            StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i + 1 < pairs.Length; i += 2)
        {
            values[(string)pairs[i]] = pairs[i + 1];
        }
        return values;
    }

    private static string TextOf(IAspectPropertyValue value)
    {
        Assert.IsTrue(value.HasValue, value.NoValueMessage);
        return Convert.ToString(value.Value, CultureInfo.InvariantCulture);
    }
}
