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

    /// <summary>
    /// Present holds both ways round, being true where the property is
    /// there and readable and false where it is not.
    /// </summary>
    [TestMethod]
    public void Evaluation_PresentBothWays()
    {
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, Present: true }",
            Values("a.P", "anything")));
        Assert.AreEqual("no", RunCondition(
            "{ Property: a.P, Present: true }", Values()));
        Assert.AreEqual("yes", RunCondition(
            "{ Property: a.P, Present: false }", Values()));
        Assert.AreEqual("no", RunCondition(
            "{ Property: a.P, Present: false }",
            Values("a.P", "anything")));
    }

    /// <summary>
    /// A value that is there but cannot be read as the type the script
    /// inferred is not present, because a property is only present when it
    /// can actually be used.
    /// </summary>
    [TestMethod]
    public void Evaluation_PresentIsFalseWhereTheValueCannotBeRead()
    {
        // The check compares the property as a bool, so "N/A" is not a
        // value the property can hold.
        var checks = "Checks:\n  Bool: { Property: a.P, Eq: true }\n";
        Assert.AreEqual("no", RunCondition(
            "{ Property: a.P, Present: true }",
            Values("a.P", "N/A"),
            new[] { "a.P" },
            checks));
    }

    // -----------------------------------------------------------------
    // The three valued table of DESIGN.md 2.6. Each row is read through
    // the aggregate probe below, which tells true, false and unknown
    // apart, rather than through a rule that cannot see the difference
    // between false and unknown.
    // -----------------------------------------------------------------

    /// <summary>
    /// A comparison on a property that is there gives true or false.
    /// </summary>
    [TestMethod]
    public void Evaluation_ComparisonOnAvailablePropertyIsTrueOrFalse()
    {
        Assert.AreEqual("true", StateOf(
            "{ Property: a.P, Eq: true }", Values("a.P", true)));
        Assert.AreEqual("false", StateOf(
            "{ Property: a.P, Eq: true }", Values("a.P", false)));
    }

    /// <summary>
    /// A comparison on an absent property is unknown, so a rule holding
    /// only that comparison never matches and the Else is reached.
    /// </summary>
    [TestMethod]
    public void Evaluation_ComparisonOnAbsentPropertyIsUnknown()
    {
        Assert.AreEqual("unknown", StateOf(
            "{ Property: a.P, Eq: true }", Values()));
        Assert.AreEqual("no", RunCondition(
            "{ Property: a.P, Eq: true }", Values()));
    }

    /// <summary>
    /// Present is answerable whatever happened, so it is never unknown.
    /// </summary>
    [TestMethod]
    public void Evaluation_PresentIsNeverUnknown()
    {
        Assert.AreEqual("true", StateOf(
            "{ Property: a.P, Present: false }", Values()));
        Assert.AreEqual("false", StateOf(
            "{ Property: a.P, Present: true }", Values()));
    }

    /// <summary>
    /// All is false as soon as one member is false, even where another
    /// member could not be answered.
    /// </summary>
    [TestMethod]
    public void Evaluation_AllIsFalseAsSoonAsOneMemberIsFalse()
    {
        var condition = "{ All: [ { Property: a.P, Eq: true }, " +
            "{ Property: a.Q, Eq: true } ] }";
        Assert.AreEqual("false", StateOf(
            condition,
            Values("a.P", true, "a.Q", false),
            new[] { "a.P", "a.Q" }));
        Assert.AreEqual("false", StateOf(
            condition,
            Values("a.P", false),
            new[] { "a.P", "a.Q" }));
    }

    /// <summary>
    /// All is unknown where one member is unknown and no member is false.
    /// </summary>
    [TestMethod]
    public void Evaluation_AllIsUnknownWhereOneIsUnknownAndNoneFalse()
    {
        var condition = "{ All: [ { Property: a.P, Eq: true }, " +
            "{ Property: a.Q, Eq: true } ] }";
        Assert.AreEqual("unknown", StateOf(
            condition,
            Values("a.P", true),
            new[] { "a.P", "a.Q" }));
        Assert.AreEqual("true", StateOf(
            condition,
            Values("a.P", true, "a.Q", true),
            new[] { "a.P", "a.Q" }));
    }

    /// <summary>
    /// Any is true as soon as one member is true, even where another
    /// member could not be answered.
    /// </summary>
    [TestMethod]
    public void Evaluation_AnyIsTrueAsSoonAsOneIsTrue()
    {
        var condition = "{ Any: [ { Property: a.P, Eq: true }, " +
            "{ Property: a.Q, Eq: true } ] }";
        Assert.AreEqual("true", StateOf(
            condition,
            Values("a.P", true),
            new[] { "a.P", "a.Q" }));
        Assert.AreEqual("yes", RunCondition(
            condition,
            Values("a.P", true),
            new[] { "a.P", "a.Q" }));
    }

    /// <summary>
    /// Any is unknown where one member is unknown and no member is true,
    /// and false where every member is false.
    /// </summary>
    [TestMethod]
    public void Evaluation_AnyIsUnknownWhereOneIsUnknownAndNoneTrue()
    {
        var condition = "{ Any: [ { Property: a.P, Eq: true }, " +
            "{ Property: a.Q, Eq: true } ] }";
        Assert.AreEqual("unknown", StateOf(
            condition,
            Values("a.P", false),
            new[] { "a.P", "a.Q" }));
        Assert.AreEqual("false", StateOf(
            condition,
            Values("a.P", false, "a.Q", false),
            new[] { "a.P", "a.Q" }));
    }

    /// <summary>
    /// Not turns true into false, false into true, and leaves a condition
    /// that could not be answered unanswered.
    /// </summary>
    [TestMethod]
    public void Evaluation_NotTurnsTrueIntoFalseAndLeavesUnknownAlone()
    {
        Assert.AreEqual("false", StateOf(
            "{ Not: { Property: a.P, Eq: true } }", Values("a.P", true)));
        Assert.AreEqual("true", StateOf(
            "{ Not: { Property: a.P, Eq: true } }", Values("a.P", false)));
        Assert.AreEqual("unknown", StateOf(
            "{ Not: { Property: a.P, Eq: true } }", Values()));
        Assert.AreEqual("yes", RunCondition(
            "{ Not: { Property: a.P, Eq: true } }", Values("a.P", false)));
        Assert.AreEqual("no", RunCondition(
            "{ Not: { Property: a.P, Eq: true } }", Values("a.P", true)));
    }

    /// <summary>
    /// A comparison on a count is always answerable, because a count of
    /// checks is known even when every check in it is unknown.
    /// </summary>
    [TestMethod]
    public void Evaluation_AggregateComparisonIsNeverUnknown()
    {
        var checks = "Checks:\n" +
            "  One: { Property: a.P, Eq: true }\n" +
            "  Two: { Property: a.Q, Eq: true }\n";
        // Neither property is there, so both checks are unknown and every
        // count is zero, which the comparison still answers.
        Assert.AreEqual("yes", RunCondition(
            "{ Evaluated: Checks, Eq: 0 }",
            Values(),
            new[] { "a.P", "a.Q" },
            checks));
        // Negating the comparison also gives an answer, which it could
        // not do if the comparison were unknown, because Not leaves an
        // unanswered condition unanswered.
        Assert.AreEqual("yes", RunCondition(
            "{ Not: { Evaluated: Checks, Eq: 1 } }",
            Values(),
            new[] { "a.P", "a.Q" },
            checks));
    }

    /// <summary>
    /// A Check reference gives back the answer of the check it names,
    /// including where that answer is unknown.
    /// </summary>
    [TestMethod]
    public void Evaluation_CheckReferenceGivesTheReferencedAnswer()
    {
        var checks = "Checks:\n  One: { Property: a.P, Eq: true }\n";
        Assert.AreEqual("yes", RunCondition(
            "{ Check: One }", Values("a.P", true), new[] { "a.P" }, checks));
        Assert.AreEqual("no", RunCondition(
            "{ Check: One }", Values("a.P", false), new[] { "a.P" }, checks));
        Assert.AreEqual("unknown", StateOf(
            "{ Check: One }",
            Values(),
            new[] { "a.P" },
            "  One: { Property: a.P, Eq: true }\n"));
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
    /// property can hold, so the property is absent.
    /// </summary>
    [TestMethod]
    public void Conversion_PlainListWhereOneValueIsNeededIsInvalid()
    {
        var list = new List<string> { "one", "two" };
        Assert.AreEqual("no", RunCondition(
            "{ Property: a.P, Present: true }", Values("a.P", list)));
        Assert.AreEqual("unknown", StateOf(
            "{ Property: a.P, Eq: \"one\" }", Values("a.P", list)));
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
                "{ Property: a.P, Eq: \"None\" }", new[] { "a.P" }, string.Empty),
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

    private static readonly string[] AggregateProperties =
        new[] { "a.P", "a.Q", "a.R" };

    /// <summary>
    /// Passed, Failed and Evaluated count only the checks that could be
    /// answered, so a check that is unknown counts towards nothing.
    /// </summary>
    [TestMethod]
    public void Aggregate_PassedFailedAndEvaluatedCountOnlyKnownChecks()
    {
        var values = Values("a.P", true, "a.Q", false);
        Assert.AreEqual("yes", RunCondition(
            "{ Passed: Checks, Eq: 1 }",
            values, AggregateProperties, AggregateChecks));
        Assert.AreEqual("yes", RunCondition(
            "{ Failed: Checks, Eq: 1 }",
            values, AggregateProperties, AggregateChecks));
        Assert.AreEqual("yes", RunCondition(
            "{ Evaluated: Checks, Eq: 2 }",
            values, AggregateProperties, AggregateChecks));
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
            "{ Passed: [One, Two], Eq: 2 }",
            values, AggregateProperties, AggregateChecks));
        Assert.AreEqual("yes", RunCondition(
            "{ Failed: [One, Two], Eq: 0 }",
            values, AggregateProperties, AggregateChecks));
        Assert.AreEqual("yes", RunCondition(
            "{ Failed: Checks, Eq: 1 }",
            values, AggregateProperties, AggregateChecks));
    }

    /// <summary>
    /// One aggregate may be compared with another, which is how a script
    /// asks whether every check that could be answered passed.
    /// </summary>
    [TestMethod]
    public void Aggregate_MayBeComparedWithAnotherAggregate()
    {
        Assert.AreEqual("yes", RunCondition(
            "{ Passed: Checks, Eq: { Evaluated: Checks } }",
            Values("a.P", true, "a.Q", true),
            AggregateProperties, AggregateChecks));
        Assert.AreEqual("no", RunCondition(
            "{ Passed: Checks, Eq: { Evaluated: Checks } }",
            Values("a.P", true, "a.Q", false),
            AggregateProperties, AggregateChecks));
    }

    /// <summary>
    /// An int output returns a count through an Else naming an aggregate,
    /// which is how a script exposes how much evidence it had.
    /// </summary>
    [TestMethod]
    public void Aggregate_IntOutputReturnsACount()
    {
        var text = @"
Format: 1
Name: Counter
Version: 1.0.0
Output:
  Name: Counter
  Description: The number of checks that could be evaluated.
  ValueType: int
  IsList: false
Optional:
  - a.P
  - a.Q
Checks:
  One: { Property: a.P, Eq: true }
  Two: { Property: a.Q, Eq: true }
Rules:
  - Else: { Evaluated: Checks }
";
        Assert.AreEqual(1, IntOf(Run(
            text, "Counter", Values("a.P", true))));
        Assert.AreEqual(2, IntOf(Run(
            text, "Counter", Values("a.P", true, "a.Q", false))));
        Assert.AreEqual(0, IntOf(Run(text, "Counter", Values())));
    }

    // -----------------------------------------------------------------
    // Required and optional properties.
    // -----------------------------------------------------------------

    private const string RequiredScript = @"
Format: 1
Name: Strict
Version: 1.0.0
Output:
  Name: Strict
  Description: A property whose sources are all required.
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
    /// A required property that is not there makes the output a value with
    /// no value rather than a guess.
    /// </summary>
    [TestMethod]
    public void Required_AbsentPropertyGivesAValueWithNoValue()
    {
        var value = Run(
            RequiredScript,
            "Strict",
            Values("device.WebDriver", "None"));
        Assert.IsFalse(value.HasValue);
        Assert.Contains("'device.IsVisible'", value.NoValueMessage);
    }

    /// <summary>
    /// The message names every absent required property and not only the
    /// first. The wording is the contract between the languages, so the
    /// whole sentence is asserted rather than a fragment of it, and the
    /// closing sentence is taken from the constant every language shares
    /// so that changing the wording fails here first.
    /// </summary>
    [TestMethod]
    public void Required_MessageNamesEveryAbsentProperty()
    {
        var value = Run(RequiredScript, "Strict", Values());
        Assert.IsFalse(value.HasValue);
        var expected =
            "Derived property 'Strict' has no value because 2 required " +
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
    public void Required_MessageIsSingularForOneProperty()
    {
        var value = Run(
            RequiredScript,
            "Strict",
            Values("device.IsVisible", true));
        Assert.IsFalse(value.HasValue);
        Assert.Contains(
            "because 1 required property was not available.",
            value.NoValueMessage);
    }

    /// <summary>
    /// Where the source value carries its own reason, that reason is what
    /// the message gives.
    /// </summary>
    [TestMethod]
    public void Required_MessageCarriesTheSourceNoValueMessage()
    {
        var value = Run(
            RequiredScript,
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
    public void Required_MessageSaysWhatAnInvalidValueHeld()
    {
        var value = Run(
            RequiredScript,
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
    /// An optional property that is not there leaves the conditions on it
    /// unanswered and the script still produces a value.
    /// </summary>
    [TestMethod]
    public void Optional_AbsentPropertyLeavesConditionsUnanswered()
    {
        Assert.AreEqual("unknown", StateOf(
            "{ Property: a.P, Eq: true }", Values()));
        Assert.AreEqual("no", RunCondition(
            "{ Property: a.P, Eq: true }", Values()));
    }

    // -----------------------------------------------------------------
    // Rule order, Else, DefaultValue and no match.
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
Optional:
  - a.P
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
        Assert.AreEqual("None", TextOf(Run(text, "Ordered", Values())));
    }

    /// <summary>
    /// Where no rule matches and the script gives a DefaultValue, the
    /// default is the answer.
    /// </summary>
    [TestMethod]
    public void Rules_DefaultValueIsUsedWhereNoRuleMatches()
    {
        var text = @"
Format: 1
Name: Defaulted
Version: 1.0.0
Output:
  Name: Defaulted
  Description: A property with a default and no Else.
  ValueType: string
  IsList: false
  DefaultValue: Unknown
Optional:
  - a.P
Rules:
  - When: { Property: a.P, Eq: true }
    Then: High
";
        var trace = new DerivedTrace();
        var value = Run(text, "Defaulted", Values(), trace);
        Assert.AreEqual("Unknown", TextOf(value));
        Assert.IsTrue(trace.UsedDefault);
        Assert.IsNull(trace.MatchedRule);
    }

    /// <summary>
    /// Where no rule matches and there is neither an Else nor a
    /// DefaultValue there is no value, and the message says why. The
    /// wording is the same in every language.
    /// </summary>
    [TestMethod]
    public void Rules_NoMatchAndNoDefaultGivesNoValue()
    {
        var text = @"
Format: 1
Name: Bare
Version: 1.0.0
Output:
  Name: Bare
  Description: A property with neither an Else nor a default.
  ValueType: string
  IsList: false
Optional:
  - a.P
Rules:
  - When: { Property: a.P, Eq: true }
    Then: High
";
        var value = Run(text, "Bare", Values());
        Assert.IsFalse(value.HasValue);
        Assert.AreEqual(
            "Derived property 'Bare' has no value because no rule matched " +
            "and the script has no Else or DefaultValue.",
            value.NoValueMessage);
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
Optional:
  - a.P
  - a.Q
Checks:
  One: { Property: a.P, Eq: true }
  Two: { Property: a.Q, Eq: true }
Rules:
  - When: { Passed: Checks, Ge: 1 }
    Then: High
  - Else: Low
";
        var trace = new DerivedTrace();
        var value = Run(text, "Traced", Values("a.P", true), trace);

        Assert.AreEqual("High", TextOf(value));

        Assert.HasCount(2, trace.Checks);
        Assert.AreEqual("One", trace.Checks[0].Name);
        Assert.AreEqual(DerivedState.True, trace.Checks[0].State);
        Assert.AreEqual("Two", trace.Checks[1].Name);
        Assert.AreEqual(DerivedState.Unknown, trace.Checks[1].State);

        Assert.AreEqual(0, trace.MatchedRule);
        Assert.IsFalse(trace.MatchedElse);

        Assert.HasCount(2, trace.Properties);
        Assert.AreEqual("a.P", trace.Properties[0].Name);
        Assert.IsFalse(trace.Properties[0].Required);
        Assert.IsTrue(trace.Properties[0].Available);
        Assert.IsTrue((bool)trace.Properties[0].Value);
        Assert.IsNull(trace.Properties[0].Reason);
        Assert.AreEqual("a.Q", trace.Properties[1].Name);
        Assert.IsFalse(trace.Properties[1].Available);
        Assert.IsNull(trace.Properties[1].Value);
        Assert.AreEqual(
            "element 'a' has no value for 'Q': property not present on " +
            "this request",
            trace.Properties[1].Reason);
    }

    /// <summary>
    /// An Else that matches is recorded as an Else in the trace.
    /// </summary>
    [TestMethod]
    public void Trace_RecordsWhenTheElseMatched()
    {
        var trace = new DerivedTrace();
        Run(
            ConditionScript(
                "{ Property: a.P, Eq: true }",
                new[] { "a.P" },
                string.Empty),
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
            RequiredScript, "Strict", "code");
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
    private static string ConditionScript(
        string condition,
        IReadOnlyList<string> optional,
        string checks)
    {
        var names = optional ?? new[] { "a.P" };
        return
            "Format: 1\n" +
            "Name: Probe\n" +
            "Version: 1.0.0\n" +
            "Output:\n" +
            "  Name: Probe\n" +
            "  Description: Whether the condition was true.\n" +
            "  ValueType: string\n" +
            "  IsList: false\n" +
            "Optional:\n" +
            string.Join("", names.Select(n => "  - " + n + "\n")) +
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
        IReadOnlyList<string> optional = null,
        string checks = null)
    {
        var value = Run(
            ConditionScript(condition, optional, checks), "Probe", values);
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
    /// Runs one condition as a named check and gives back "true", "false"
    /// or "unknown". Counting the check apart from reading it is the only
    /// way to tell a condition that is false from one that could not be
    /// answered, because neither matches a rule.
    ///
    /// Any text passed as otherChecks holds further check entries already
    /// indented by two spaces, without the Checks heading.
    /// </summary>
    private static string StateOf(
        string condition,
        IDictionary<string, object> values,
        IReadOnlyList<string> optional = null,
        string otherChecks = null)
    {
        var names = optional ?? new[] { "a.P" };
        var text =
            "Format: 1\n" +
            "Name: Probe\n" +
            "Version: 1.0.0\n" +
            "Output:\n" +
            "  Name: Probe\n" +
            "  Description: The state of the condition under test.\n" +
            "  ValueType: string\n" +
            "  IsList: false\n" +
            "Optional:\n" +
            string.Join("", names.Select(n => "  - " + n + "\n")) +
            "Checks:\n" +
            // Any other check comes first, so a check the condition under
            // test names has already been answered by the time the
            // condition is read.
            (otherChecks ?? string.Empty) +
            "  Subject: " + condition + "\n" +
            "Rules:\n" +
            "  - When: { Passed: [Subject], Eq: 1 }\n" +
            "    Then: IsTrue\n" +
            "  - When: { Failed: [Subject], Eq: 1 }\n" +
            "    Then: IsFalse\n" +
            "  - Else: IsUnknown\n";

        switch (TextOf(Run(text, "Probe", values)))
        {
            case "IsTrue": return "true";
            case "IsFalse": return "false";
            default: return "unknown";
        }
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

    private static int IntOf(IAspectPropertyValue value)
    {
        Assert.IsTrue(value.HasValue, value.NoValueMessage);
        return (int)value.Value;
    }
}
