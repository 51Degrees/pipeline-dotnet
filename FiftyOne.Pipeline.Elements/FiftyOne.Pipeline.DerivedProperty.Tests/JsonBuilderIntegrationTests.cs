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
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.JsonBuilder.Data;
using FiftyOne.Pipeline.JsonBuilder.FlowElement;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.DerivedProperty.Tests;

/// <summary>
/// Checks that a derived property reaches the JSON the JSON builder
/// writes, both when it has a value and when it does not.
///
/// Why this is tested here rather than left to the JSON builder's own
/// tests. The cloud serves a derived property by putting the JSON builder
/// after this element and returning what the builder writes, so the shape
/// of that JSON is the contract between this element and every cloud
/// customer. Nothing else holds it, and the JSON builder reaches this
/// element only because it walks every element data in the flow data
/// rather than only the aspect engines, which is a detail of the JSON
/// builder that could change without anyone thinking about this element.
/// </summary>
[TestClass]
public class JsonBuilderIntegrationTests
{
    /// <summary>
    /// A script with one source property, so that the same script covers
    /// both the value and the no value case depending on whether the
    /// source element publishes the property.
    /// </summary>
    private const string ScriptText =
        "Format: 1\n" +
        "Name: Confidence\n" +
        "Version: 1.0.0\n" +
        "Output:\n" +
        "  Name: Confidence\n" +
        "  Description: Whether a human is viewing the page.\n" +
        "  ValueType: string\n" +
        "  IsList: false\n" +
        "  Category: General\n" +
        "  Values:\n" +
        "    - Name: High\n" +
        "      Description: A human is viewing the page.\n" +
        "    - Name: Low\n" +
        "      Description: No human is viewing the page.\n" +
        "Rules:\n" +
        "  - When: { Property: device.IsCrawler, Eq: true }\n" +
        "    Then: Low\n" +
        "  - Else: High\n";

    /// <summary>
    /// The derived property is written under the element data key of this
    /// element, lower cased as the JSON builder lower cases every key, and
    /// carries the value the rules chose.
    /// </summary>
    [TestMethod]
    public void JsonBuilder_CarriesTheDerivedValue()
    {
        var json = ProcessAndGetJson(publishTheSourceProperty: true);

        var derived = json[DerivedPropertyElement.DerivedElementDataKey];
        Assert.IsNotNull(
            derived,
            "The JSON carried no '" +
            DerivedPropertyElement.DerivedElementDataKey +
            "' section at all, so a cloud response would not carry the " +
            "derived property. The JSON was " + json.ToString());
        Assert.AreEqual("High", (string)derived["confidence"]);

        // A property that has a value carries no reason, because a reason
        // beside a value would tell a customer the value is suspect.
        Assert.IsNull(derived["confidencenullreason"]);
    }

    /// <summary>
    /// Where a source property is missing the derived property has no
    /// value, and the JSON carries a null value with the message beside
    /// it, which is how a cloud customer is told why rather than being
    /// left with a silently absent property.
    /// </summary>
    [TestMethod]
    public void JsonBuilder_CarriesTheNoValueReason()
    {
        var json = ProcessAndGetJson(publishTheSourceProperty: false);

        var derived = json[DerivedPropertyElement.DerivedElementDataKey];
        Assert.IsNotNull(derived);
        Assert.AreEqual(
            JTokenType.Null,
            derived["confidence"].Type,
            "A property with no value is written as null.");

        var reason = (string)derived["confidencenullreason"];
        Assert.IsNotNull(
            reason,
            "The JSON carried no reason, so a customer would see an " +
            "absent property with nothing saying why.");
        StringAssert.Contains(reason, "device.IsCrawler");
    }

    /// <summary>
    /// Builds a pipeline of a source element, the derived property element
    /// and the JSON builder, processes one request and parses what the
    /// JSON builder wrote.
    /// </summary>
    /// <param name="publishTheSourceProperty">
    /// Whether the source element publishes a value for IsCrawler. The
    /// property is declared either way, so that the pipeline check passes
    /// and the difference is only whether a value is present on the
    /// request.
    /// </param>
    private static JObject ProcessAndGetJson(bool publishTheSourceProperty)
    {
        var loggerFactory = NullLoggerFactory.Instance;
        var values = new Dictionary<string, object>();
        var declaredWithoutValues = new List<string>();
        if (publishTheSourceProperty)
        {
            values.Add("IsCrawler", false);
        }
        else
        {
            declaredWithoutValues.Add("IsCrawler");
        }

        var source = new StubSourceElement(
            loggerFactory.CreateLogger<
                FlowElementBase<StubSourceData, ElementPropertyMetaData>>(),
            "device",
            values,
            declaredWithoutValues);

        // The JSON builder reads a sequence number and throws where none
        // was set, so the sequence element goes first, which is the order
        // any pipeline serving JSON already uses.
        using (var sequence = new SequenceElementBuilder(loggerFactory)
            .Build())
        using (var derived = new DerivedPropertyElementBuilder(loggerFactory)
            .AddScript("Confidence", ScriptText)
            .Build())
        using (var jsonBuilder =
            new JsonBuilderElementBuilder(loggerFactory).Build())
        using (var pipeline = new PipelineBuilder(loggerFactory)
            .AddFlowElement(sequence)
            .AddFlowElement(source)
            .AddFlowElement(derived)
            .AddFlowElement(jsonBuilder)
            .Build())
        using (var data = pipeline.CreateFlowData())
        {
            data.Process();
            var built = data.Get<IJsonBuilderElementData>();
            Assert.IsNotNull(built, "The JSON builder wrote no element data.");
            return JObject.Parse(built.Json);
        }
    }
}
