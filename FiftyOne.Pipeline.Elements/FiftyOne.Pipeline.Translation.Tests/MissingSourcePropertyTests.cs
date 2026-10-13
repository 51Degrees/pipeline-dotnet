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
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
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
    /// <summary>
    /// The kinds of source data the translation engine reads from.
    /// </summary>
    private enum SourceKind
    {
        /// <summary>
        /// Plain element data whose indexer throws
        /// PropertyMissingException for every property.
        /// </summary>
        ThrowingIndexer,

        /// <summary>
        /// Plain element data whose indexer returns null for every
        /// property.
        /// </summary>
        NullIndexer,

        /// <summary>
        /// Plain element data whose indexer computes the value on read and
        /// never stores it, as on-premise aspect data does.
        /// </summary>
        ComputedIndexer,

        /// <summary>
        /// Real <see cref="AspectDataBase"/> without the property, so the
        /// read goes through the aspect data's own missing property path.
        /// </summary>
        AspectDataMissing,

        /// <summary>
        /// Real <see cref="AspectDataBase"/> holding the property.
        /// </summary>
        AspectDataPresent
    }

    private const string SourceKey = "testsource";

    private readonly ILoggerFactory _loggerFactory =
        LoggerFactory.Create(loggingBuilder => { });

    // Each test writes its translation file to a directory of its own and
    // removes it afterwards. The file name has to stay
    // 'animals.fr_FR.yml' because the locale is read from it, and
    // TranslationEngineTests uses the same name in the shared temp
    // directory, so a fixed path would leak between the two classes.
    private DirectoryInfo _tempDirectory;
    private IPipeline _pipeline;
    private IFlowData _flowData;

    [TestCleanup]
    public void Cleanup()
    {
        _flowData?.Dispose();
        _pipeline?.Dispose();
        if (_tempDirectory != null && _tempDirectory.Exists == true)
        {
            _tempDirectory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// A source whose indexer throws PropertyMissingException for a missing
    /// property must not make the engine throw. The destination gets a
    /// no-value placeholder. The pipeline is built WITHOUT suppressed
    /// process exceptions so any escaping exception fails the test.
    /// </summary>
    [TestMethod]
    public void SourceThrowsPropertyMissing_ProducesNoValuePlaceholder()
    {
        var aspectValue = ProcessAndGet<IAspectPropertyValue<string>>(
            SourceKind.ThrowingIndexer);

        Assert.IsFalse(aspectValue.HasValue);
        StringAssert.Contains(aspectValue.NoValueMessage, "could not be found");
    }

    /// <summary>
    /// The same as <see cref="SourceThrowsPropertyMissing_ProducesNoValuePlaceholder"/>
    /// but through real aspect data, which is what a cloud engine
    /// produces. Its indexer asks the missing property service for the
    /// reason and throws, here with the reason a failed cloud request
    /// gives.
    /// </summary>
    [TestMethod]
    public void AspectDataMissing_ProducesNoValuePlaceholder()
    {
        var aspectValue = ProcessAndGet<IAspectPropertyValue<string>>(
            SourceKind.AspectDataMissing);

        Assert.IsFalse(aspectValue.HasValue);
        StringAssert.Contains(aspectValue.NoValueMessage, "could not be found");
    }

    /// <summary>
    /// Real aspect data that does hold the source property is still
    /// translated, so catching the missing property exception has not
    /// changed the path a present value takes.
    /// </summary>
    [TestMethod]
    public void AspectDataPresent_IsTranslated()
    {
        var aspectValue = ProcessAndGet<IAspectPropertyValue<string>>(
            SourceKind.AspectDataPresent);

        Assert.IsTrue(aspectValue.HasValue);
        Assert.AreEqual("chat", aspectValue.Value);
    }

    /// <summary>
    /// A source that computes its values on read, with nothing in its
    /// backing dictionary, is still translated. On-premise aspect data
    /// works this way, so a read through AsDictionary or TryGet, which
    /// only see the backing dictionary, would report every value missing.
    /// </summary>
    [TestMethod]
    public void ComputedIndexer_IsTranslated()
    {
        var aspectValue = ProcessAndGet<IAspectPropertyValue<string>>(
            SourceKind.ComputedIndexer);

        Assert.IsTrue(aspectValue.HasValue);
        Assert.AreEqual("chat", aspectValue.Value);
    }

    /// <summary>
    /// When the translation declares the value type readers of the
    /// destination expect, the no-value placeholder must be of that type so
    /// typed reads of the destination do not fail with an invalid cast.
    /// </summary>
    [TestMethod]
    public void SourceThrowsPropertyMissing_TypedPlaceholder()
    {
        var aspectValue = ProcessAndGet<
            IAspectPropertyValue<IReadOnlyList<IWeightedValue<string>>>>(
            SourceKind.ThrowingIndexer,
            typedDestination: true);

        Assert.IsFalse(aspectValue.HasValue);
        StringAssert.Contains(aspectValue.NoValueMessage, "could not be found");
    }

    /// <summary>
    /// The typed placeholder is also produced through real aspect data.
    /// </summary>
    [TestMethod]
    public void AspectDataMissing_TypedPlaceholder()
    {
        var aspectValue = ProcessAndGet<
            IAspectPropertyValue<IReadOnlyList<IWeightedValue<string>>>>(
            SourceKind.AspectDataMissing,
            typedDestination: true);

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
        var aspectValue = ProcessAndGet<
            IAspectPropertyValue<IReadOnlyList<IWeightedValue<string>>>>(
            SourceKind.NullIndexer,
            typedDestination: true);

        Assert.IsFalse(aspectValue.HasValue);
        StringAssert.Contains(aspectValue.NoValueMessage, "could not be found");
    }

    /// <summary>
    /// Build a pipeline of a source element followed by a translation
    /// engine, process a French request through it with process
    /// exceptions NOT suppressed, and return the translated value as the
    /// expected type.
    /// </summary>
    private TValue ProcessAndGet<TValue>(
        SourceKind sourceKind,
        bool typedDestination = false)
        where TValue : class
    {
        _tempDirectory = Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString()));
        var file = WriteTranslations(
            Path.Combine(_tempDirectory.FullName, "animals.fr_FR.yml"),
            new Dictionary<string, string>()
            {
                { "cat", "chat" },
                { "dog", "chien" }
            });

        var builder = new TranslationEngineBuilder(_loggerFactory)
            .SetSourceElementDataKey(SourceKey)
            .AddSource(file.FullName);
        if (typedDestination == true)
        {
            builder.AddTranslation<IReadOnlyList<IWeightedValue<string>>>(
                "Animal", "AnimalTranslated");
        }
        else
        {
            builder.AddTranslation("Animal", "AnimalTranslated");
        }

        _pipeline = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(new SourceElement(
                _loggerFactory.CreateLogger<SourceElement>(),
                _loggerFactory.CreateLogger<AspectDataBase>(),
                sourceKind))
            .AddFlowElement(builder.Build())
            .SetSuppressProcessExceptions(false)
            .Build();
        _flowData = _pipeline.CreateFlowData();
        _flowData.AddEvidence("header.accept-language", "fr_FR");
        _flowData.Process();

        var translation = _flowData.Get<ITranslationData>()["AnimalTranslated"];
        Assert.IsInstanceOfType<TValue>(translation);
        return (TValue)translation;
    }

    private static FileInfo WriteTranslations(
        string path,
        IReadOnlyDictionary<string, string> translations)
    {
        using (var writer = new StreamWriter(path))
        {
            foreach (var translation in translations)
            {
                writer.WriteLine($"{translation.Key}: {translation.Value}");
            }
        }
        return new FileInfo(path);
    }

    /// <summary>
    /// Element data whose indexer answers every read the same way without
    /// storing anything: by throwing PropertyMissingException, by
    /// returning null, or by returning a computed value.
    /// </summary>
    private class IndexerSourceData : ElementDataBase
    {
        private readonly SourceKind _sourceKind;

        public IndexerSourceData(IPipeline pipeline, SourceKind sourceKind)
            : base(null, pipeline)
        {
            _sourceKind = sourceKind;
        }

        public override object this[string key]
        {
            get
            {
                if (_sourceKind == SourceKind.ThrowingIndexer)
                {
                    throw new PropertyMissingException(
                        $"Property '{key}' not found in data for element " +
                        $"'{SourceKey}'.");
                }
                if (_sourceKind == SourceKind.ComputedIndexer)
                {
                    return new AspectPropertyValue<string>("cat");
                }
                return null;
            }
            set { base[key] = value; }
        }
    }

    /// <summary>
    /// Aspect data with no engine behind it, so it does not lazy load, and
    /// the missing property service a failed cloud request would leave.
    /// </summary>
    private class AspectSourceData : AspectDataBase
    {
        public AspectSourceData(
            ILogger<AspectDataBase> logger,
            IPipeline pipeline)
            : base(logger, pipeline, null, new CloudFailedMissingPropertyService())
        {
        }
    }

    /// <summary>
    /// Reports every missing property as missing because the cloud
    /// request failed, which is the reason the website saw.
    /// </summary>
    private class CloudFailedMissingPropertyService : IMissingPropertyService
    {
        private static readonly MissingPropertyResult _result =
            new MissingPropertyResult()
            {
                Reason = MissingPropertyReason.CloudRequestFailed,
                Description = "The cloud request failed."
            };

        public MissingPropertyResult GetMissingPropertyReason(
            string propertyName,
            IAspectEngine engine) => _result;

        public MissingPropertyResult GetMissingPropertyReason(
            string propertyName,
            IReadOnlyList<IAspectEngine> engines) => _result;

        public MissingPropertyResult GetMissingPropertyReason(
            string propertyName,
            IReadOnlyList<IAspectEngine> engines,
            IAspectData aspectData) => _result;
    }

    /// <summary>
    /// Element that adds source data of the configured kind.
    /// </summary>
    private class SourceElement : FlowElementBase<IElementData, ElementPropertyMetaData>
    {
        private readonly ILogger<AspectDataBase> _aspectDataLogger;
        private readonly SourceKind _sourceKind;

        public SourceElement(
            ILogger<FlowElementBase<IElementData, ElementPropertyMetaData>> logger,
            ILogger<AspectDataBase> aspectDataLogger,
            SourceKind sourceKind)
            : base(logger)
        {
            _aspectDataLogger = aspectDataLogger;
            _sourceKind = sourceKind;
        }

        public override string ElementDataKey => SourceKey;

        public override IEvidenceKeyFilter EvidenceKeyFilter =>
            new EvidenceKeyFilterWhitelist(new List<string>());

        public override IList<ElementPropertyMetaData> Properties =>
            new List<ElementPropertyMetaData>();

        protected override void ProcessInternal(IFlowData data)
        {
            data.GetOrAdd(ElementDataKey, CreateData);
        }

        private IElementData CreateData(IPipeline pipeline)
        {
            switch (_sourceKind)
            {
                case SourceKind.ThrowingIndexer:
                case SourceKind.NullIndexer:
                case SourceKind.ComputedIndexer:
                    return new IndexerSourceData(pipeline, _sourceKind);
                case SourceKind.AspectDataPresent:
                    var aspectData = new AspectSourceData(
                        _aspectDataLogger,
                        pipeline);
                    aspectData["Animal"] =
                        new AspectPropertyValue<string>("cat");
                    return aspectData;
                default:
                    return new AspectSourceData(_aspectDataLogger, pipeline);
            }
        }

        protected override void ManagedResourcesCleanup() { }

        protected override void UnmanagedResourcesCleanup() { }
    }
}
