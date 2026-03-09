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
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.TestHelpers;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.Pipeline.Engines.Tests.FlowElements;

/// <summary>
/// Tests for <see cref="TranslationEngine"/> behavior.
/// </summary>
[TestClass]
public class TranslationEngineTests
{
    /// <summary>
    /// Simple destination data
    /// </summary>
    private class DestinationData(
        ILogger<ElementDataBase> logger,
        IPipeline pipeline) : ElementDataBase(logger, pipeline)
    {
    }

    /// <summary>
    /// In memory translation map
    /// </summary>
    private class TestTranslation(
        string sourceProperty,
        string destinationProperty,
        IReadOnlyDictionary<string, string> map,
        IEnumerable<string> evidenceKeys = null) : ITranslation
    {
        private readonly IEnumerable<string> _evidenceKeys =
            evidenceKeys ?? [];

        public string SourceProperty { get; } = sourceProperty;

        public string DestinationProperty { get; } = destinationProperty;

        public IEnumerable<string> EvidenceKeys 
            { get { return _evidenceKeys; } }

        public bool TryTranslate(
            IFlowData data,
            IAspectPropertyValue<string> value,
            out IAspectPropertyValue<string> translatedValue)
        {
            translatedValue = null;
            if (value == null || value.HasValue == false)
            {
                return false;
            }

            if (map.TryGetValue(value.Value, out string mapped))
            {
                translatedValue = new AspectPropertyValue<string>(mapped);
                return true;
            }

            return false;
        }
    }

    private const string DestinationDataKey = "destination";
    private TestLoggerFactory _loggerFactory;

    [TestInitialize]
    public void Initialize()
    {
        _loggerFactory = new TestLoggerFactory();
    }

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var logger in _loggerFactory.Loggers)
        {
            logger.AssertMaxErrors(0);
            logger.AssertMaxWarnings(0);
        }
    }

    /// <summary>
    /// Verifies translation using a source value taken from evidence.
    /// </summary>
    [TestMethod]
    public void TranslateFromPropertyValue()
    {
        var translation = new TestTranslation(
            "country",
            "countrytranslated",
            new Dictionary<string, string>()
            {
                { "United Kingdom", "Royaume-Uni" }
            });
        var engine = CreateEngine(translation);

        var flowData = MockFlowData.CreateFromEvidence(
            [],
            false);
        ConfigurePipelineForDestination(flowData);
        var destinationData = CreateDestinationData(flowData);
        flowData.Setup(d => d.Get(DestinationDataKey))
            .Returns(destinationData);
        flowData.Setup(d => d.GetWhere(
            It.IsAny<Func<IElementPropertyMetaData, bool>>()))
            .Returns(
            [
                new KeyValuePair<string, object>(
                    "countryaspect.country", "United Kingdom")
            ]);

        engine.Process(flowData.Object);

        var translated = destinationData["countrytranslated"] 
            as IAspectPropertyValue<string>;
        Assert.AreEqual("Royaume-Uni", translated.Value);
    }

    /// <summary>
    /// Verifies translation when the destination property is fully qualified.
    /// </summary>
    [TestMethod]
    public void TranslateFromEvidenceValue()
    {
        var translation = new TestTranslation(
            "country",
            "countrytranslated",
            new Dictionary<string, string>()
            {
                { "Spain", "Espana" }
            });
        var engine = CreateEngine(translation);

        var flowData = MockFlowData.CreateFromEvidence(
            [],
            false);
        ConfigurePipelineForDestination(flowData);
        var destinationData = CreateDestinationData(flowData);
        flowData.Setup(d => d.Get(DestinationDataKey))
            .Returns(destinationData);
        flowData.Setup(d => d.GetWhere(
            It.IsAny<Func<IElementPropertyMetaData, bool>>()))
            .Returns([]);
        object evidenceValue = "Spain";
        flowData.Setup(d => d.TryGetEvidence("country", out evidenceValue))
            .Returns(true);

        engine.Process(flowData.Object);

        var translated = destinationData["countrytranslated"]
            as IAspectPropertyValue<string>;
        Assert.AreEqual("Espana", translated.Value);
    }

    /// <summary>
    /// Verifies that ambiguous source property matches result in an exception.
    /// </summary>
    [TestMethod]
    public void MultipleMatches_Throws()
    {
        var translation = new TestTranslation(
            "country",
            "countrytranslated",
            new Dictionary<string, string>());
        var engine = CreateEngine(translation);

        var flowData = MockFlowData.CreateFromEvidence(
            [],
            false);
        flowData.Setup(d => d.GetWhere(
            It.IsAny<Func<IElementPropertyMetaData, bool>>()))
            .Returns(
            [
                new KeyValuePair<string, object>("device.country", "France"),
                new KeyValuePair<string, object>("profile.country", "France")
            ]);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            engine.Process(flowData.Object);
        });
    }

    /// <summary>
    /// Creates a tranlation engine for testing.
    /// </summary>
    /// <param name="translation"></param>
    /// <returns></returns>
    private TranslationEngine CreateEngine(ITranslation translation)
    {
        return new TranslationEngine(
            new List<ITranslation>() { translation },
            _loggerFactory.CreateLogger<TranslationEngine>());
    }

    /// <summary>
    /// Create the destination / translated data. 
    /// </summary>
    /// <param name="flowData"></param>
    /// <returns></returns>
    private DestinationData CreateDestinationData(Mock<IFlowData> flowData)
    {
        return new DestinationData(
            _loggerFactory.CreateLogger<ElementDataBase>(),
            flowData.Object.Pipeline);
    }

    /// <summary>
    /// Configure pipeline metadata.
    /// </summary>
    private static void ConfigurePipelineForDestination(
        Mock<IFlowData> flowData)
    {
        var pipeline = new Mock<IPipeline>();
        var destinationProperties =
            new Dictionary<string, IElementPropertyMetaData>(
                StringComparer.OrdinalIgnoreCase)
            {
                { "countrytranslated", null }
            };
        var availableProperties =
            new Dictionary<string,
            IReadOnlyDictionary<
                string, 
                IElementPropertyMetaData>>(
                StringComparer.OrdinalIgnoreCase)
            {
                { DestinationDataKey, destinationProperties }
            };
        pipeline.SetupGet(p => p.ElementAvailableProperties)
            .Returns(availableProperties);
        flowData.SetupGet(d => d.Pipeline)
            .Returns(pipeline.Object);
    }
}
