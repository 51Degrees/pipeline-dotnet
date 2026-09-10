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
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Core.TypedMap;
using FiftyOne.Pipeline.DerivedProperty.Data;
using FiftyOne.Pipeline.DerivedProperty.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.DerivedProperty.Tests;

/// <summary>
/// Tests a script that replaces the value of a property another element
/// already produced, rather than creating a property of its own.
///
/// The whole point of the capability is that a script has proof where the
/// owning element has inference, so where the proof is absent the
/// inference has to stand. An override therefore sets a value and never
/// clears one, and a request that cannot read every source property the
/// script names leaves the owning element's value exactly as it was, with
/// no message, because from the caller's point of view nothing is missing.
/// </summary>
[TestClass]
public class OverrideTests
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

    // -----------------------------------------------------------------
    // What an override writes on a request.
    // -----------------------------------------------------------------

    /// <summary>
    /// The value the script chose replaces the value the owning element
    /// produced, in that element's own data. Nothing of that name is
    /// written under the derived key, because the property belongs to the
    /// element that declared it and a second copy would be a second answer
    /// to the same question.
    /// </summary>
    [TestMethod]
    public void Override_ReplacesTheValueInTheTargetElementData()
    {
        var produced = new AspectPropertyValue<bool>(true);

        using (var element = new DerivedPropertyElementBuilder(_loggerFactory)
            .AddScript("CrawlerProof", OverrideScript)
            .Build())
        using (var pipeline = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(Source("signature", Values("Verified", true)))
            .AddFlowElement(Device(produced))
            .AddFlowElement(element)
            .Build())
        using (var data = pipeline.CreateFlowData())
        {
            data.Process();

            var device = (IAspectPropertyValue)data.Get("device")["IsCrawler"];
            Assert.IsTrue(device.HasValue, device.NoValueMessage);
            Assert.IsFalse((bool)device.Value);

            var derived = data.Get(
                DerivedPropertyElement.DerivedElementDataKey);
            Assert.IsFalse(
                derived.AsDictionary().ContainsKey("IsCrawler"),
                "the derived element data gained a copy of the property " +
                "the script replaces");
        }
    }

    /// <summary>
    /// A source property the script names but cannot read leaves the value
    /// the owning element produced in place, being the same instance it
    /// wrote, so nothing was written over it and no message was left
    /// saying the value is missing when it is not.
    /// </summary>
    [TestMethod]
    public void Override_MissingSourcePropertyLeavesTheValueAlone()
    {
        var produced = new AspectPropertyValue<bool>(true);
        var signature = new StubSourceElement(
            SourceLogger(),
            "signature",
            new Dictionary<string, object>(),
            new[] { "Verified" });

        using (var element = new DerivedPropertyElementBuilder(_loggerFactory)
            .AddScript("CrawlerProof", OverrideScript)
            .Build())
        using (var pipeline = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(signature)
            .AddFlowElement(Device(produced))
            .AddFlowElement(element)
            .Build())
        using (var data = pipeline.CreateFlowData())
        {
            data.Process();

            // The same instance the owning element wrote is still there, so
            // nothing was written over it, and it still carries the value
            // that element chose.
            var device = data.Get("device")["IsCrawler"];
            Assert.AreSame(produced, device);
            Assert.IsTrue(produced.HasValue);
            Assert.IsTrue(produced.Value);

            var derived = data.Get(
                DerivedPropertyElement.DerivedElementDataKey);
            Assert.IsFalse(
                derived.AsDictionary().ContainsKey("IsCrawler"));
        }
    }

    /// <summary>
    /// A request on which the owning element wrote no element data at all
    /// leaves the script with nothing to replace, which is not a fault. The
    /// script is run against flow data holding no element data under the
    /// key it targets, which a pipeline built through the element cannot
    /// produce because the build refuses a pipeline without the owning
    /// element in it.
    /// </summary>
    [TestMethod]
    public void Override_TargetElementDataAbsentWritesNothing()
    {
        var compiled = new CompiledScript(
            Validate(OverrideScript, "CrawlerProof"));

        using (var pipeline = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(Source("signature", Values("Verified", true)))
            .Build())
        using (var data = pipeline.CreateFlowData())
        {
            data.Process();
            Assert.IsFalse(data.TryGetValue(
                new TypedKey<IElementData>("device"), out _));

            var derived = new DerivedPropertyData(
                _loggerFactory.CreateLogger<DerivedPropertyData>(), pipeline);
            compiled.Process(data, derived);

            Assert.IsEmpty(derived.AsDictionary());
        }
    }

    // -----------------------------------------------------------------
    // What the pipeline build refuses.
    // -----------------------------------------------------------------

    /// <summary>
    /// An override with nothing to replace would write nothing on every
    /// request and would do so silently, since leaving the value alone is
    /// what an override does when it has nothing to say, so the build is
    /// the only place the mistake can be caught.
    /// </summary>
    [TestMethod]
    public void PipelineCheck_NoElementProducesTheTargetPropertyFails()
    {
        var element = new DerivedPropertyElementBuilder(_loggerFactory)
            .AddScript("CrawlerProof", OverrideScript)
            .Build();
        var builder = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(Source("signature", Values("Verified", true)))
            .AddFlowElement(element);

        var exception = Assert.ThrowsExactly<PipelineConfigurationException>(
            () => builder.Build());
        Assert.Contains("'IsCrawler'", exception.Message);
        Assert.Contains("'device'", exception.Message);
        Assert.Contains(
            "no element in the pipeline produces", exception.Message);
        element.Dispose();
    }

    /// <summary>
    /// The owning element placed after the derived property element builds,
    /// and the value the owning element produces stands, because the
    /// element data the script would have written into did not exist when
    /// the script ran.
    ///
    /// Where an element sits is deliberately not judged at build, for the
    /// reason given on PipelineCheck_SupplierAfterTheElementLeavesNoValue.
    /// An override that finds nothing to replace leaves the value alone,
    /// which is exactly what an override does when it has nothing to say,
    /// so the outcome here is the owning element's own answer.
    /// </summary>
    [TestMethod]
    public void PipelineCheck_TargetElementAfterThisElementLeavesItsValue()
    {
        var produced = new AspectPropertyValue<bool>(true);

        using (var element = new DerivedPropertyElementBuilder(_loggerFactory)
            .AddScript("CrawlerProof", OverrideScript)
            .Build())
        using (var pipeline = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(Source("signature", Values("Verified", true)))
            .AddFlowElement(element)
            .AddFlowElement(Device(produced))
            .Build())
        using (var data = pipeline.CreateFlowData())
        {
            data.Process();

            var device = (IAspectPropertyValue)data.Get("device")["IsCrawler"];
            Assert.IsTrue(device.HasValue, device.NoValueMessage);
            Assert.IsTrue(
                (bool)device.Value,
                "the owning element's own value should stand");

            // And nothing of that name is written under the derived key,
            // because an override never creates a property of its own.
            Assert.IsFalse(
                data.Get(DerivedPropertyElement.DerivedElementDataKey)
                    .TryGet("IsCrawler", out object _),
                "an override wrote a property under the derived key");
        }
    }

    /// <summary>
    /// The owning element declares the type every caller reads the
    /// property as, so a script producing anything else would break those
    /// callers on the requests where it had an answer. IsCrawler is a
    /// boolean and IsArtificialIntelligence is text, so the two are easy
    /// to write the wrong way round.
    /// </summary>
    [TestMethod]
    public void PipelineCheck_TargetPropertyOfAnotherTypeFails()
    {
        var element = new DerivedPropertyElementBuilder(_loggerFactory)
            .AddScript("CrawlerName", MistypedOverrideScript)
            .Build();
        var builder = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(Source("signature", Values("Verified", true)))
            .AddFlowElement(Device(new AspectPropertyValue<bool>(true)))
            .AddFlowElement(element);

        var exception = Assert.ThrowsExactly<PipelineConfigurationException>(
            () => builder.Build());
        Assert.Contains("'device.IsCrawler'", exception.Message);
        Assert.Contains("declares as Boolean", exception.Message);
        Assert.Contains("produces String", exception.Message);
        element.Dispose();
    }

    // -----------------------------------------------------------------
    // What the element advertises.
    // -----------------------------------------------------------------

    /// <summary>
    /// A script that creates a property defines one, and a script that
    /// replaces a value defines nothing, because the element that owns the
    /// property published the definition already and a second one would be
    /// a second definition of the same property.
    /// </summary>
    [TestMethod]
    public void Override_IsNotAdvertisedButACreatedPropertyIs()
    {
        using (var element = new DerivedPropertyElementBuilder(_loggerFactory)
            .AddScript("HumanConfidence", CreateScript)
            .AddScript("CrawlerProof", OverrideScript)
            .Build())
        {
            Assert.HasCount(2, element.Scripts);
            Assert.HasCount(1, element.Properties);
            Assert.AreEqual("HumanConfidence", element.Properties[0].Name);
            Assert.AreEqual(typeof(string), element.Properties[0].Type);
        }
    }

    // -----------------------------------------------------------------
    // The two kinds of script side by side.
    // -----------------------------------------------------------------

    /// <summary>
    /// derived.IsCrawler and device.IsCrawler are two different
    /// properties, so one element may write both, and the two answers stay
    /// apart.
    /// </summary>
    [TestMethod]
    public void Override_CreatedAndReplacedOfOneNameCoexistInOneElement()
    {
        using (var element = new DerivedPropertyElementBuilder(_loggerFactory)
            .AddScript("CrawlerGuess", CreateSameNameScript)
            .AddScript("CrawlerProof", OverrideScript)
            .Build())
        using (var pipeline = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(Source("signature", Values("Verified", true)))
            .AddFlowElement(Device(new AspectPropertyValue<bool>(true)))
            .AddFlowElement(element)
            .Build())
        using (var data = pipeline.CreateFlowData())
        {
            data.Process();
            AssertIsCrawler(data, inDevice: false, inDerived: true);
        }
    }

    /// <summary>
    /// The same two scripts in two elements of one pipeline. A script that
    /// replaces a property another element owns writes nothing under the
    /// derived key, so it cannot collide with the property a second
    /// derived property element creates there.
    /// </summary>
    [TestMethod]
    public void Override_CreatedAndReplacedOfOneNameCoexistInTwoElements()
    {
        using (var creating = new DerivedPropertyElementBuilder(_loggerFactory)
            .AddScript("CrawlerGuess", CreateSameNameScript)
            .Build())
        using (var replacing = new DerivedPropertyElementBuilder(_loggerFactory)
            .AddScript("CrawlerProof", OverrideScript)
            .Build())
        using (var pipeline = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(Source("signature", Values("Verified", true)))
            .AddFlowElement(Device(new AspectPropertyValue<bool>(true)))
            .AddFlowElement(creating)
            .AddFlowElement(replacing)
            .Build())
        using (var data = pipeline.CreateFlowData())
        {
            data.Process();
            AssertIsCrawler(data, inDevice: false, inDerived: true);
        }
    }

    /// <summary>
    /// Two elements replacing the same property fail the pipeline build.
    /// Both write the one element data the owning element holds, so
    /// whichever ran last would decide the value and the other script
    /// would have no effect that anybody could see. An override is not
    /// advertised in the element's properties, so this collision is
    /// invisible to the check that catches two elements creating the same
    /// derived property and has to be looked for separately.
    /// </summary>
    [TestMethod]
    public void PipelineCheck_TwoElementsReplacingOnePropertyFails()
    {
        var first = new DerivedPropertyElementBuilder(_loggerFactory)
            .AddScript("CrawlerProof", OverrideScript)
            .Build();
        var second = new DerivedPropertyElementBuilder(_loggerFactory)
            .AddScript("CrawlerProofAgain", SecondOverrideScript)
            .Build();
        var builder = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(Source("signature", Values("Verified", true)))
            .AddFlowElement(Device(new AspectPropertyValue<bool>(true)))
            .AddFlowElement(first)
            .AddFlowElement(second);

        var exception = Assert.ThrowsExactly<PipelineConfigurationException>(
            () => builder.Build());
        Assert.Contains("'device.IsCrawler'", exception.Message);
        Assert.Contains("CrawlerProof", exception.Message);
        Assert.Contains("CrawlerProofAgain", exception.Message);
        Assert.Contains(
            "One pipeline replaces each property once", exception.Message);
        first.Dispose();
        second.Dispose();
    }

    // -----------------------------------------------------------------
    // The canonical form.
    // -----------------------------------------------------------------

    /// <summary>
    /// The canonical JSON prints the name the script was written with, so
    /// a script that replaces device.IsCrawler prints the prefixed form and
    /// one that creates HumanConfidence prints the bare form. The canonical
    /// text is what every language implementation is compared against, so
    /// the prefix has to survive printing.
    /// </summary>
    [TestMethod]
    public void Override_CanonicalJsonPrintsThePrefixedName()
    {
        var replacing = DerivedScriptWriter.ToCanonicalJson(
            Validate(OverrideScript, "CrawlerProof"));
        Assert.Contains(
            "\"Output\": {\n    \"Name\": \"device.IsCrawler\",", replacing);
        Assert.Contains("\"Name\": \"CrawlerProof\",", replacing);

        var creating = DerivedScriptWriter.ToCanonicalJson(
            Validate(CreateScript, "HumanConfidence"));
        Assert.Contains(
            "\"Output\": {\n    \"Name\": \"HumanConfidence\",", creating);
    }

    /// <summary>
    /// The prefix reaches the model the writer prints from, which is what
    /// the element reads when it decides whether a script creates a
    /// property or replaces one.
    /// </summary>
    [TestMethod]
    public void Override_ThePrefixReachesTheModel()
    {
        var replacing = Validate(OverrideScript, "CrawlerProof").Output;
        Assert.AreEqual("device", replacing.ElementDataKey);
        Assert.AreEqual("IsCrawler", replacing.Name);
        Assert.AreEqual("device.IsCrawler", replacing.QualifiedName);
        Assert.IsTrue(replacing.IsOverride);

        var creating = Validate(CreateScript, "HumanConfidence").Output;
        Assert.AreEqual("derived", creating.ElementDataKey);
        Assert.AreEqual("HumanConfidence", creating.Name);
        Assert.AreEqual("derived.HumanConfidence", creating.QualifiedName);
        Assert.IsFalse(creating.IsOverride);
    }

    // -----------------------------------------------------------------
    // Scripts.
    // -----------------------------------------------------------------

    /// <summary>
    /// Replaces the device property with what a verified signature proves,
    /// which is the case the capability was written for. A verified
    /// signature says a person made the request, so IsCrawler is false.
    /// </summary>
    private const string OverrideScript =
        "Format: 1\n" +
        "Name: CrawlerProof\n" +
        "Version: 1.0.0\n" +
        "Output:\n" +
        "  Name: device.IsCrawler\n" +
        "  Description: What the signature proves about the requester.\n" +
        "  ValueType: bool\n" +
        "  IsList: false\n" +
        "Rules:\n" +
        "  - When: { Property: signature.Verified, Eq: true }\n" +
        "    Then: false\n" +
        "  - Else: true\n";

    /// <summary>
    /// A second script replacing the same property as
    /// <see cref="OverrideScript"/>, so that two elements holding one each
    /// collide on the property they both write.
    /// </summary>
    private const string SecondOverrideScript =
        "Format: 1\n" +
        "Name: CrawlerProofAgain\n" +
        "Version: 1.0.0\n" +
        "Output:\n" +
        "  Name: device.IsCrawler\n" +
        "  Description: What the signature proves about the requester.\n" +
        "  ValueType: bool\n" +
        "  IsList: false\n" +
        "Rules:\n" +
        "  - When: { Property: signature.Verified, Eq: true }\n" +
        "    Then: true\n" +
        "  - Else: false\n";

    /// <summary>
    /// The same target property written as text rather than as a boolean,
    /// which the pipeline build refuses.
    /// </summary>
    private const string MistypedOverrideScript =
        "Format: 1\n" +
        "Name: CrawlerName\n" +
        "Version: 1.0.0\n" +
        "Output:\n" +
        "  Name: device.IsCrawler\n" +
        "  Description: The wrong type for the property it replaces.\n" +
        "  ValueType: string\n" +
        "  IsList: false\n" +
        "Rules:\n" +
        "  - When: { Property: signature.Verified, Eq: true }\n" +
        "    Then: Person\n" +
        "  - Else: Crawler\n";

    /// <summary>
    /// A script that creates a property of its own, reading the same
    /// source property as the scripts above.
    /// </summary>
    private const string CreateScript =
        "Format: 1\n" +
        "Name: HumanConfidence\n" +
        "Version: 1.0.0\n" +
        "Output:\n" +
        "  Name: HumanConfidence\n" +
        "  Description: How far the signature supports a person.\n" +
        "  ValueType: string\n" +
        "  IsList: false\n" +
        "Rules:\n" +
        "  - When: { Property: signature.Verified, Eq: true }\n" +
        "    Then: High\n" +
        "  - Else: Low\n";

    /// <summary>
    /// A script creating derived.IsCrawler, which is a different property
    /// from the device.IsCrawler the override replaces. It answers the
    /// opposite way round, so a test can tell the two apart.
    /// </summary>
    private const string CreateSameNameScript =
        "Format: 1\n" +
        "Name: CrawlerGuess\n" +
        "Version: 1.0.0\n" +
        "Output:\n" +
        "  Name: IsCrawler\n" +
        "  Description: A property of this element sharing a bare name.\n" +
        "  ValueType: bool\n" +
        "  IsList: false\n" +
        "Rules:\n" +
        "  - When: { Property: signature.Verified, Eq: true }\n" +
        "    Then: true\n" +
        "  - Else: false\n";

    // -----------------------------------------------------------------
    // Helpers.
    // -----------------------------------------------------------------

    /// <summary>
    /// Validates a script, failing the test with every fault where it does
    /// not validate.
    /// </summary>
    private static DerivedScript Validate(string text, string name)
    {
        var result = DerivedScriptValidator.Validate(text, name, "code");
        Assert.IsTrue(
            result.IsValid,
            DerivedScriptValidationException.Describe(result.Faults));
        return result.Script;
    }

    /// <summary>
    /// The element that owns IsCrawler, declaring it as the boolean every
    /// caller reads it as and publishing the value given.
    /// </summary>
    private StubSourceElement Device(IAspectPropertyValue<bool> produced)
    {
        return new StubSourceElement(
            SourceLogger(),
            "device",
            new Dictionary<string, object> { { "IsCrawler", produced } },
            null,
            new Dictionary<string, Type> { { "IsCrawler", typeof(bool) } });
    }

    private StubSourceElement Source(
        string elementDataKey,
        IReadOnlyDictionary<string, object> values)
    {
        return new StubSourceElement(SourceLogger(), elementDataKey, values);
    }

    private ILogger<FlowElementBase<StubSourceData, ElementPropertyMetaData>>
        SourceLogger()
    {
        return _loggerFactory.CreateLogger<
            FlowElementBase<StubSourceData, ElementPropertyMetaData>>();
    }

    private static IReadOnlyDictionary<string, object> Values(
        string name,
        object value)
    {
        return new Dictionary<string, object> { { name, value } };
    }

    /// <summary>
    /// Reads IsCrawler out of both element data instances and checks each
    /// against what the script writing there chose.
    /// </summary>
    private static void AssertIsCrawler(
        IFlowData data,
        bool inDevice,
        bool inDerived)
    {
        var device = (IAspectPropertyValue)data.Get("device")["IsCrawler"];
        Assert.IsTrue(device.HasValue, device.NoValueMessage);
        Assert.AreEqual(inDevice, device.Value);

        var derived = (IAspectPropertyValue)data.Get(
            DerivedPropertyElement.DerivedElementDataKey)["IsCrawler"];
        Assert.IsTrue(derived.HasValue, derived.NoValueMessage);
        Assert.AreEqual(inDerived, derived.Value);
    }
}
