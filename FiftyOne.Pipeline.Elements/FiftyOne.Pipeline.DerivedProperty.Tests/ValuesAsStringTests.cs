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

namespace FiftyOne.Pipeline.DerivedProperty.Tests;

/// <summary>
/// Reads a source property that the element can hand back both ways, being
/// the typed value through the indexer and the string it stored through
/// <see cref="IProvidesValuesAsString"/>.
///
/// Device detection stores "Unknown" for the properties the 51Degrees
/// JavaScript fills in, meaning the JavaScript has not run on the request,
/// and the boolean accessor turns that into False, which no caller can
/// tell from a measured False. A script comparing the property with text
/// now reads the stored string, so it can answer differently for a request
/// nobody has measured. A script comparing with anything else, and an
/// element that does not offer its stored strings, read through the
/// indexer exactly as they did before.
/// </summary>
[TestClass]
public class ValuesAsStringTests
{
    private TestLoggerFactory _loggerFactory;

    /// <summary>
    /// A logger factory of its own for each test, so the lines one test
    /// writes are never read by another.
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        _loggerFactory = new TestLoggerFactory();
    }

    /// <summary>
    /// A script comparing the property with text reads the string the
    /// element stored, being "Unknown", and never the "False" the typed
    /// value would be written as.
    /// </summary>
    [TestMethod]
    public void ValuesAsString_TextComparisonReadsTheStoredString()
    {
        Assert.AreEqual("yes", RunCondition(
            "{ Property: device.IsVisible, Eq: \"Unknown\" }",
            Device(Typed(false), Stored("Unknown"))));
        Assert.AreEqual("no", RunCondition(
            "{ Property: device.IsVisible, Eq: \"False\" }",
            Device(Typed(false), Stored("Unknown"))));
    }

    /// <summary>
    /// A script comparing the same property with a boolean reads the typed
    /// value through the indexer, as every script always has. The stored
    /// string is "Unknown", which is not a boolean, so a script that read
    /// the string here would report the property as unavailable rather
    /// than answering at all.
    /// </summary>
    [TestMethod]
    public void ValuesAsString_BoolComparisonStillReadsTheTypedValue()
    {
        Assert.AreEqual("yes", RunCondition(
            "{ Property: device.IsVisible, Eq: false }",
            Device(Typed(false), Stored("Unknown"))));
        Assert.AreEqual("no", RunCondition(
            "{ Property: device.IsVisible, Eq: true }",
            Device(Typed(false), Stored("Unknown"))));
    }

    /// <summary>
    /// An element that does not offer its stored strings is read through
    /// the indexer, so a script comparing with text sees the written form
    /// of the typed value and nothing about that has changed.
    /// </summary>
    [TestMethod]
    public void ValuesAsString_ElementWithoutTheInterfaceIsUnchanged()
    {
        Assert.AreEqual("yes", RunCondition(
            "{ Property: device.IsVisible, Eq: \"False\" }",
            PlainDevice(Typed(false))));
        Assert.AreEqual("no", RunCondition(
            "{ Property: device.IsVisible, Eq: \"Unknown\" }",
            PlainDevice(Typed(false))));
    }

    /// <summary>
    /// Where the element offers the string and has none to give, the
    /// script says the property was not available and hands on the reason
    /// the element gave, rather than throwing. The indexer would have
    /// answered with a value on this request, so the reason reported can
    /// only have come from the string.
    /// </summary>
    [TestMethod]
    public void ValuesAsString_NoValueIsReportedWithTheReasonGiven()
    {
        var trace = new DerivedTrace();
        var value = Run(
            Script("{ Property: device.IsVisible, Eq: \"Unknown\" }"),
            Device(
                Typed(false),
                Absent("the 51Degrees JavaScript has not run yet")),
            trace);

        Assert.IsFalse(value.HasValue);
        Assert.Contains(
            "'device.IsVisible' (element 'device' has no value for " +
            "'IsVisible': the 51Degrees JavaScript has not run yet).",
            value.NoValueMessage);
        Assert.HasCount(1, trace.Properties);
        Assert.IsFalse(trace.Properties[0].Available);
    }

    // -----------------------------------------------------------------
    // Helpers.
    // -----------------------------------------------------------------

    /// <summary>
    /// A script whose single rule gives one answer when the condition
    /// holds and another when it does not, so one condition can be read on
    /// its own.
    /// </summary>
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

    /// <summary>
    /// Runs one condition against one source element and gives back "yes"
    /// where the rule matched and "no" where the Else was reached.
    /// </summary>
    private string RunCondition(string condition, IFlowElement source)
    {
        var text = TextOf(Run(Script(condition), source));
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
    /// pipeline holding the source element given.
    /// </summary>
    private IAspectPropertyValue Run(
        string text,
        IFlowElement source,
        DerivedTrace trace = null)
    {
        var result = DerivedScriptValidator.Validate(text, "Probe", "code");
        Assert.IsTrue(result.IsValid,
            DerivedScriptValidationException.Describe(result.Faults));
        var compiled = new CompiledScript(result.Script);
        using (var pipeline = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(source)
            .Build())
        using (var data = pipeline.CreateFlowData())
        {
            data.Process();
            return compiled.Evaluate(data, trace);
        }
    }

    /// <summary>
    /// An element publishing IsVisible both ways, being the typed value
    /// the indexer answers and the string the element stored.
    /// </summary>
    private StubStringSourceElement Device(
        object typed,
        IAspectPropertyValue<string> stored)
    {
        return new StubStringSourceElement(
            _loggerFactory.CreateLogger<FlowElementBase<
                StubStringSourceData, ElementPropertyMetaData>>(),
            "device",
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "IsVisible", typed }
            },
            new Dictionary<string, IAspectPropertyValue<string>>(
                StringComparer.OrdinalIgnoreCase)
            {
                { "IsVisible", stored }
            });
    }

    /// <summary>
    /// An element publishing IsVisible as the typed value alone, which is
    /// every element that has not been given the string accessor.
    /// </summary>
    private StubSourceElement PlainDevice(object typed)
    {
        return new StubSourceElement(
            _loggerFactory.CreateLogger<FlowElementBase<
                StubSourceData, ElementPropertyMetaData>>(),
            "device",
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "IsVisible", typed }
            });
    }

    /// <summary>
    /// The value a typed accessor answers, which is what device detection
    /// gives for a stored "Unknown" on a boolean property.
    /// </summary>
    private static IAspectPropertyValue<bool> Typed(bool value)
    {
        return new AspectPropertyValue<bool>(value);
    }

    /// <summary>
    /// The string the element stored.
    /// </summary>
    private static IAspectPropertyValue<string> Stored(string value)
    {
        return new AspectPropertyValue<string>(value);
    }

    /// <summary>
    /// A stored string that has no value, carrying the reason.
    /// </summary>
    private static IAspectPropertyValue<string> Absent(string reason)
    {
        return new AspectPropertyValue<string> { NoValueMessage = reason };
    }

    private static string TextOf(IAspectPropertyValue value)
    {
        Assert.IsTrue(value.HasValue, value.NoValueMessage);
        return Convert.ToString(value.Value, CultureInfo.InvariantCulture);
    }
}
