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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace FiftyOne.Pipeline.DerivedProperty.Tests;

/// <summary>
/// A Boolean source property read as a Boolean cannot say that the data
/// holds no answer yet, because the conversion turns any stored value that
/// is not the literal True into False and reports it as a value. These
/// tests cover reading the stored text instead, so that a placeholder such
/// as Unknown leaves the script with no value rather than counting as a
/// definite No.
/// </summary>
[TestClass]
public class BooleanPlaceholderTests
{
    /// <summary>
    /// A stored placeholder leaves the script with no value, and the
    /// message names the property and the text the data holds.
    /// </summary>
    [TestMethod]
    public void BooleanPlaceholder_UnknownGivesNoValue()
    {
        var value = Run("Unknown");
        Assert.IsFalse(value.HasValue,
            "A property whose stored value is not a Boolean should leave " +
            "the script with no value.");
        StringAssert.Contains(value.NoValueMessage, "IsVisible");
        StringAssert.Contains(value.NoValueMessage, "Unknown");
    }

    /// <summary>
    /// A stored False is a real answer and still reads as one, so the
    /// change costs no request a value it used to have.
    /// </summary>
    [TestMethod]
    public void BooleanPlaceholder_FalseStillReadsAsFalse()
    {
        var value = Run("False");
        Assert.IsTrue(value.HasValue, value.NoValueMessage);
        Assert.AreEqual("No it is not", TextOf(value));
    }

    /// <summary>
    /// A stored True reads as True, which is the case a request reaches
    /// once the 51Degrees JavaScript has run.
    /// </summary>
    [TestMethod]
    public void BooleanPlaceholder_TrueReadsAsTrue()
    {
        var value = Run("True");
        Assert.IsTrue(value.HasValue, value.NoValueMessage);
        Assert.AreEqual("Yes it is", TextOf(value));
    }

    /// <summary>
    /// Only a property the element declares as a Boolean is read this way.
    /// Unknown is a legitimate value of a text property, so a script
    /// naming one still reads it and is not left without a value.
    /// </summary>
    [TestMethod]
    public void BooleanPlaceholder_TextPropertyKeepsUnknown()
    {
        var value = Run(
            "Unknown",
            "{ Property: device.IsVisible, Eq: \"Unknown\" }",
            typeof(string));
        Assert.IsTrue(value.HasValue, value.NoValueMessage);
        Assert.AreEqual("Yes it is", TextOf(value));
    }

    /// <summary>
    /// An element that keeps its values in the base dictionary, which is
    /// every element other than an engine converting them as they are
    /// read, is unaffected.
    /// </summary>
    [TestMethod]
    public void BooleanPlaceholder_PlainElementUnaffected()
    {
        var script = Script("{ Property: device.IsVisible, Eq: true }");
        var result = DerivedScriptValidator.Validate(script, "Probe", "code");
        Assert.IsTrue(result.IsValid,
            DerivedScriptValidationException.Describe(result.Faults));
        var compiled = new CompiledScript(result.Script);
        var loggerFactory = new TestLoggerFactory();
        using (var pipeline = new PipelineBuilder(loggerFactory)
            .AddFlowElement(new StubSourceElement(
                loggerFactory.CreateLogger<
                    FlowElementBase<StubSourceData, ElementPropertyMetaData>>(),
                "device",
                new Dictionary<string, object> { { "IsVisible", true } }))
            .Build())
        using (var data = pipeline.CreateFlowData())
        {
            data.Process();
            var value = compiled.Evaluate(data, null);
            Assert.IsTrue(value.HasValue, value.NoValueMessage);
            Assert.AreEqual("Yes it is", TextOf(value));
        }
    }

    private static IAspectPropertyValue Run(
        string stored,
        string condition = "{ Property: device.IsVisible, Eq: true }",
        Type declaredType = null)
    {
        var script = Script(condition);
        var result = DerivedScriptValidator.Validate(script, "Probe", "code");
        Assert.IsTrue(result.IsValid,
            DerivedScriptValidationException.Describe(result.Faults));
        var compiled = new CompiledScript(result.Script);
        var loggerFactory = new TestLoggerFactory();
        var declaredTypes = declaredType == null
            ? null
            : new Dictionary<string, Type> { { "IsVisible", declaredType } };
        using (var pipeline = new PipelineBuilder(loggerFactory)
            .AddFlowElement(new CoercingSourceElement(
                loggerFactory.CreateLogger<
                    FlowElementBase<
                        CoercingSourceData, ElementPropertyMetaData>>(),
                "device",
                new Dictionary<string, string> { { "IsVisible", stored } },
                declaredTypes))
            .Build())
        using (var data = pipeline.CreateFlowData())
        {
            data.Process();
            return compiled.Evaluate(data, null);
        }
    }

    private static string Script(string condition)
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
            "Rules:\n" +
            "  - When: " + condition + "\n" +
            "    Then: Yes it is\n" +
            "  - Else: No it is not\n";
    }

    private static string TextOf(IAspectPropertyValue value)
    {
        return Convert.ToString(value.Value, CultureInfo.InvariantCulture);
    }
}
