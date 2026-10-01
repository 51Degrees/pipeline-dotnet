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
using FiftyOne.Pipeline.Engines;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Translation.Data;
using FiftyOne.Pipeline.Translation.FlowElements;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;

namespace FiftyOne.Pipeline.Translation.Tests;

/// <summary>
/// Tests for the behavior of the translation engine when the source
/// property cannot be read. Aspect data (for example a cloud engine's data
/// when the resource key does not include the property, or when the cloud
/// request failed) throws <see cref="PropertyMissingException"/> from its
/// indexer where plain element data returns null; both must result in a
/// no-value placeholder rather than a per-request exception.
/// </summary>
[TestClass]
public class MissingSourcePropertyTests
{
    private ILoggerFactory _loggerFactory = LoggerFactory.Create(b => { });

    /// <summary>
    /// A source whose indexer throws PropertyMissingException for a missing
    /// property (as aspect data does) must not make the engine throw. The
    /// destination gets a no-value placeholder. The pipeline is built
    /// WITHOUT suppressed process exceptions so any escaping exception
    /// fails the test.
    /// </summary>
    [TestMethod]
    public void SourceThrowsPropertyMissing_ProducesNoValuePlaceholder()
    {
        using var flowData = Setup();

        flowData.AddEvidence("header.accept-language", "fr_FR");
        flowData.Process();

        var result = flowData.Get<ITranslationData>();
        var translation = result["AnimalTranslated"];

        Assert.IsNotNull(translation);
        Assert.IsInstanceOfType(translation, typeof(IAspectPropertyValue<string>));
        var aspectValue = translation as IAspectPropertyValue<string>;
        Assert.IsFalse(aspectValue.HasValue);
        StringAssert.Contains(aspectValue.NoValueMessage, "could not be found");
    }

    /// <summary>
    /// When the translation declares the value type readers of the
    /// destination expect, the no-value placeholder must be of that type so
    /// typed reads of the destination do not fail with an invalid cast.
    /// </summary>
    [TestMethod]
    public void SourceThrowsPropertyMissing_TypedPlaceholder()
    {
        using var flowData = Setup(typedDestination: true);

        flowData.AddEvidence("header.accept-language", "fr_FR");
        flowData.Process();

        var result = flowData.Get<ITranslationData>();
        var translation = result["AnimalTranslated"];

        Assert.IsNotNull(translation);
        Assert.IsInstanceOfType(
            translation,
            typeof(IAspectPropertyValue<IReadOnlyList<IWeightedValue<string>>>));
        var aspectValue = translation
            as IAspectPropertyValue<IReadOnlyList<IWeightedValue<string>>>;
        Assert.IsFalse(aspectValue.HasValue);
        StringAssert.Contains(aspectValue.NoValueMessage, "could not be found");
    }

    /// <summary>
    /// A source property that is absent (null) also gets the typed
    /// placeholder when a destination value type is declared.
    /// </summary>
    [TestMethod]
    public void SourceAbsent_TypedPlaceholder()
    {
        using var flowData = Setup(
            typedDestination: true,
            sourceThrows: false);

        flowData.AddEvidence("header.accept-language", "fr_FR");
        flowData.Process();

        var result = flowData.Get<ITranslationData>();
        var translation = result["AnimalTranslated"];

        Assert.IsNotNull(translation);
        Assert.IsInstanceOfType(
            translation,
            typeof(IAspectPropertyValue<IReadOnlyList<IWeightedValue<string>>>));
        var aspectValue = translation
            as IAspectPropertyValue<IReadOnlyList<IWeightedValue<string>>>;
        Assert.IsFalse(aspectValue.HasValue);
        StringAssert.Contains(aspectValue.NoValueMessage, "could not be found");
    }

    /// <summary>
    /// Build a pipeline of a source element (throwing or returning null for
    /// every property read) followed by a translation engine, and return a
    /// flow data ready to process. Process exceptions are NOT suppressed.
    /// </summary>
    private IFlowData Setup(
        bool typedDestination = false,
        bool sourceThrows = true)
    {
        var translations = new Dictionary<string, string>()
        {
            { "cat", "chat" },
            { "dog", "chien" }
        };
        var file = CreateFile("animals.fr_FR.yml", translations);

        var builder = new TranslationEngineBuilder(_loggerFactory)
            .SetSourceElementDataKey("throwingsource")
            .AddSource(file.File.FullName);
        if (typedDestination)
        {
            builder.AddTranslation<IReadOnlyList<IWeightedValue<string>>>(
                "Animal", "AnimalTranslated");
        }
        else
        {
            builder.AddTranslation("Animal", "AnimalTranslated");
        }
        var engine = builder.Build();

        var pipeline = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(new ThrowingSourceElement(
                _loggerFactory.CreateLogger<ThrowingSourceElement>(),
                sourceThrows))
            .AddFlowElement(engine)
            .Build();

        return pipeline.CreateFlowData();
    }

    private TempFile CreateFile(
        string name,
        IReadOnlyDictionary<string, string> translations)
    {
        var path = Path.Combine(Path.GetTempPath(), name);
        using (var writer = new StreamWriter(path))
        {
            foreach (var translation in translations)
            {
                writer.WriteLine($"{translation.Key}: {translation.Value}");
            }
        }
        return new TempFile(new FileInfo(path));
    }

    private class TempFile : IDisposable
    {
        public TempFile(FileInfo file)
        {
            File = file;
        }

        public FileInfo File { get; }

        public void Dispose()
        {
            File.Delete();
        }
    }

    /// <summary>
    /// Element data whose indexer throws PropertyMissingException for every
    /// read, mirroring an aspect data whose property is not available.
    /// When configured not to throw it returns null instead.
    /// </summary>
    private class ThrowingSourceData : ElementDataBase
    {
        private readonly bool _throws;

        public ThrowingSourceData(IPipeline pipeline, bool throws)
            : base(null, pipeline)
        {
            _throws = throws;
        }

        public override object this[string key]
        {
            get
            {
                if (_throws)
                {
                    throw new PropertyMissingException(
                        $"Property '{key}' not found in data for element " +
                        $"'throwingsource'.");
                }
                return null;
            }
            set { base[key] = value; }
        }
    }

    /// <summary>
    /// Element whose data throws (or returns null) for every property read.
    /// </summary>
    private class ThrowingSourceElement
        : FlowElementBase<ThrowingSourceData, ElementPropertyMetaData>
    {
        private readonly bool _throws;

        public ThrowingSourceElement(
            ILogger<FlowElementBase<ThrowingSourceData, ElementPropertyMetaData>> logger,
            bool throws)
            : base(logger)
        {
            _throws = throws;
        }

        public override string ElementDataKey => "throwingsource";

        public override IEvidenceKeyFilter EvidenceKeyFilter =>
            new EvidenceKeyFilterWhitelist(new List<string>());

        public override IList<ElementPropertyMetaData> Properties =>
            new List<ElementPropertyMetaData>();

        protected override void ProcessInternal(IFlowData data)
        {
            data.GetOrAdd(
                ElementDataKey,
                p => new ThrowingSourceData(p, _throws));
        }

        protected override void ManagedResourcesCleanup() { }

        protected override void UnmanagedResourcesCleanup() { }
    }
}
