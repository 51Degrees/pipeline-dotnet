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
using FiftyOne.Pipeline.Core.TypedMap;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.TestHelpers;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FiftyOne.Pipeline.Engines.Tests.FlowElements
{
    /// <summary>
    /// Tests for <see cref="TranslationEngine"/>.
    /// </summary>
    [TestClass]
    public class TranslationEngineTests
    {
        /// <summary>
        /// Simple source element data for tests.
        /// </summary>
        private class SourceData : ElementDataBase
        {
            /// <summary>
            /// Creates source data.
            /// </summary>
            public SourceData(
                ILogger<ElementDataBase> logger,
                IPipeline pipeline)
                : base(logger, pipeline)
            {
            }
        }

        private const string SourceElementDataKey = "source-element";
        private TestLoggerFactory _loggerFactory;
        private List<string> _tempPaths;

        /// <summary>
        /// Set up logger factory for each test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            _loggerFactory = new TestLoggerFactory();
            _tempPaths = new List<string>();
        }

        /// <summary>
        /// Assert no warnings or errors were emitted.
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            foreach (var tempPath in _tempPaths)
            {
                try
                {
                    if (Directory.Exists(tempPath))
                    {
                        Directory.Delete(tempPath, true);
                    }
                }
                catch
                {
                }
            }

            foreach (var logger in _loggerFactory.Loggers)
            {
                logger.AssertMaxErrors(0);
                logger.AssertMaxWarnings(0);
            }
        }

        /// <summary>
        /// Verifies source and destination mapping is respected and output is
        /// written to destination property using en_GB language format.
        /// </summary>
        [TestMethod]
        public void WritesTranslatedStringToDestinationProperty_EnGb()
        {
            var tempDir = Helpers.CreateTempDirectory(_tempPaths);
            var enGb = Helpers.CreateTranslationFile(
                tempDir,
                "en_GB.yaml",
                new Dictionary<string, string>()
                {
                    { "Spain", "Spain-GB" }
                });

            var flowData = MockFlowData.CreateFromEvidence(
                new Dictionary<string, object>()
                {
                    { "query.translation", "en_GB" }
                },
                false);
            var pipeline = ConfigurePipeline(flowData);

            var sourceData = new SourceData(
                _loggerFactory.CreateLogger<ElementDataBase>(),
                pipeline.Object);
            sourceData["country"] = new AspectPropertyValue<string>("Spain");

            Helpers.ConfigureSourceData(flowData, sourceData);

            var translationData = new TranslationEngineData(
                _loggerFactory.CreateLogger<TranslationEngineData>(),
                pipeline.Object);
            ConfigureTranslationData(flowData, translationData);

            var engine = CreateEngine(
                [new Translation("country", "countrytranslated")],
                [enGb]);

            engine.Process(flowData.Object);

            var translated = (IAspectPropertyValue<string>)
                translationData["countrytranslated"];
            Assert.AreEqual("Spain-GB", translated.Value);
            Assert.IsFalse(translationData.AsDictionary().ContainsKey("country"));
        }

        /// <summary>
        /// Verifies language evidence keys are used in order.
        /// </summary>
        [TestMethod]
        public void UsesFirstMatchingLanguageEvidenceKey()
        {
            var tempDir = Helpers.CreateTempDirectory(_tempPaths);
            var enGb = Helpers.CreateTranslationFile(
                tempDir,
                "en_GB.yaml",
                new Dictionary<string, string>()
                {
                    { "Spain", "Spain-GB" }
                });
            var enEs = Helpers.CreateTranslationFile(
                tempDir,
                "en_ES.yaml",
                new Dictionary<string, string>()
                {
                    { "Spain", "Espana" }
                });

            var flowData = MockFlowData.CreateFromEvidence(
                new Dictionary<string, object>()
                {
                    { "query.translation", "en_GB" },
                    { "header.accept-language", "en_ES,en;q=0.8" }
                },
                false);
            var pipeline = ConfigurePipeline(flowData);

            var sourceData = new SourceData(
                _loggerFactory.CreateLogger<ElementDataBase>(),
                pipeline.Object);
            sourceData["country"] = new AspectPropertyValue<string>("Spain");

            Helpers.ConfigureSourceData(flowData, sourceData);

            var translationData = new TranslationEngineData(
                _loggerFactory.CreateLogger<TranslationEngineData>(),
                pipeline.Object);
            ConfigureTranslationData(flowData, translationData);

            var engine = CreateEngine(
                [new Translation("country", "countrytranslated")],
                [enGb, enEs]);

            engine.Process(flowData.Object);

            var translated = (IAspectPropertyValue<string>)
                translationData["countrytranslated"];
            Assert.AreEqual("Spain-GB", translated.Value);
        }

        /// <summary>
        /// Verifies header evidence is used when query evidence is missing.
        /// </summary>
        [TestMethod]
        public void UsesHeaderAcceptLanguageWhenQueryMissing()
        {
            var tempDir = Helpers.CreateTempDirectory(_tempPaths);
            var enEs = Helpers.CreateTranslationFile(
                tempDir,
                "en_ES.yaml",
                new Dictionary<string, string>()
                {
                    { "Spain", "Espana" }
                });

            var flowData = MockFlowData.CreateFromEvidence(
                new Dictionary<string, object>()
                {
                    { "header.accept-language", "en_ES,en;q=0.8" }
                },
                false);
            var pipeline = ConfigurePipeline(flowData);

            var sourceData = new SourceData(
                _loggerFactory.CreateLogger<ElementDataBase>(),
                pipeline.Object);
            sourceData["country"] = new AspectPropertyValue<string>("Spain");

            Helpers.ConfigureSourceData(flowData, sourceData);

            var translationData = new TranslationEngineData(
                _loggerFactory.CreateLogger<TranslationEngineData>(),
                pipeline.Object);
            ConfigureTranslationData(flowData, translationData);

            var engine = CreateEngine(
                [new Translation("country", "countrytranslated")],
                [enEs]);

            engine.Process(flowData.Object);

            var translated = (IAspectPropertyValue<string>)
                translationData["countrytranslated"];
            Assert.AreEqual("Espana", translated.Value);
        }

        /// <summary>
        /// Verifies list properties are translated element-by-element and
        /// fall back to original values when no translation exists.
        /// </summary>
        [TestMethod]
        public void TranslatesStringListProperty_WithFallback()
        {
            var tempDir = Helpers.CreateTempDirectory(_tempPaths);
            var enEs = Helpers.CreateTranslationFile(
                tempDir,
                "en_ES.yaml",
                new Dictionary<string, string>()
                {
                    { "Spain", "Espana" },
                    { "Germany", "Alemania" }
                });

            var flowData = MockFlowData.CreateFromEvidence(
                new Dictionary<string, object>()
                {
                    { "query.translation", "en_ES" }
                },
                false);
            var pipeline = ConfigurePipeline(flowData);

            var sourceData = new SourceData(
                _loggerFactory.CreateLogger<ElementDataBase>(),
                pipeline.Object);
            sourceData["countries"] = 
                new AspectPropertyValue<IReadOnlyList<string>>(
                ["Spain", "Italy"]);

            Helpers.ConfigureSourceData(flowData, sourceData);

            var translationData = new TranslationEngineData(
                _loggerFactory.CreateLogger<TranslationEngineData>(),
                pipeline.Object);
            ConfigureTranslationData(flowData, translationData);

            var engine = CreateEngine(
                [new Translation("countries", "countriestranslated")],
                [enEs]);

            engine.Process(flowData.Object);

            var translated = (IAspectPropertyValue<List<string>>)
                translationData["countriestranslated"];
            CollectionAssert.AreEqual(
                new[] { "Espana", "Italy" },
                translated.Value.ToArray());
        }

        /// <summary>
        /// Verifies weighted list properties are translated element-by-element
        /// and fall back to original values when no translation exists.
        /// </summary>
        [TestMethod]
        public void TranslatesWeightedStringListProperty_WithFallback()
        {
            var tempDir = Helpers.CreateTempDirectory(_tempPaths);
            var enGb = Helpers.CreateTranslationFile(
                tempDir,
                "en_GB.yaml",
                new Dictionary<string, string>()
                {
                    { "Spain", "Spain-GB" }
                });

            var flowData = MockFlowData.CreateFromEvidence(
                new Dictionary<string, object>()
                {
                    { "query.translation", "en_GB" }
                },
                false);
            var pipeline = ConfigurePipeline(flowData);

            var sourceData = new SourceData(
                _loggerFactory.CreateLogger<ElementDataBase>(),
                pipeline.Object);
            sourceData["countriesWeighted"] = new AspectPropertyValue<IReadOnlyList<IWeightedValue<string>>>(
                new List<IWeightedValue<string>>()
                {
                    new WeightedValue<string>(100, "Spain"),
                    new WeightedValue<string>(50, "Italy")
                });

            Helpers.ConfigureSourceData(flowData, sourceData);

            var translationData = new TranslationEngineData(
                _loggerFactory.CreateLogger<TranslationEngineData>(),
                pipeline.Object);
            ConfigureTranslationData(flowData, translationData);

            var engine = CreateEngine(
                [new Translation(
                    "countriesWeighted", 
                    "countriesWeightedTranslated")],
                [enGb]);

            engine.Process(flowData.Object);

            var translated = (IAspectPropertyValue<List<WeightedValue<string>>>)
                translationData["countriesWeightedTranslated"];
            Assert.AreEqual(2, translated.Value.Count);
            Assert.AreEqual((ushort)100, translated.Value[0].RawWeighting);
            Assert.AreEqual("Spain-GB", translated.Value[0].Value);
            Assert.AreEqual((ushort)50, translated.Value[1].RawWeighting);
            Assert.AreEqual("Italy", translated.Value[1].Value);
        }

        /// <summary>
        /// Verifies no output is set when language evidence is missing.
        /// </summary>
        [TestMethod]
        public void NoLanguageEvidence_SkipsTranslation()
        {
            var tempDir = Helpers.CreateTempDirectory(_tempPaths);
            var enGb = Helpers.CreateTranslationFile(
                tempDir,
                "en_GB.yaml",
                new Dictionary<string, string>()
                {
                    { "Spain", "Spain-GB" }
                });

            var flowData = MockFlowData.CreateFromEvidence(
                new Dictionary<string, object>(),
                false);
            var pipeline = ConfigurePipeline(flowData);

            var sourceData = new SourceData(
                _loggerFactory.CreateLogger<ElementDataBase>(),
                pipeline.Object);
            sourceData["country"] = new AspectPropertyValue<string>("Spain");

            Helpers.ConfigureSourceData(flowData, sourceData);

            var translationData = new TranslationEngineData(
                _loggerFactory.CreateLogger<TranslationEngineData>(),
                pipeline.Object);
            ConfigureTranslationData(flowData, translationData);

            var engine = CreateEngine(
                [new Translation("country", "countrytranslated")],
                [enGb]);

            engine.Process(flowData.Object);

            Assert.IsFalse(translationData
                .AsDictionary()
                .ContainsKey("countrytranslated"));
        }

        /// <summary>
        /// Create a translation engine for tests.
        /// </summary>
        private TranslationEngine CreateEngine(
            IEnumerable<ITranslation> translations,
            IEnumerable<FileInfo> sources)
        {
            return new TranslationEngine(
                SourceElementDataKey,
                translations,
                sources,
                _loggerFactory.CreateLogger<
                    FlowElementBase<
                        ITranslationEngineData, 
                        IElementPropertyMetaData>>(),
                (pipeline, element) => new TranslationEngineData(
                    _loggerFactory.CreateLogger<TranslationEngineData>(),
                    pipeline));
        }

        /// <summary>
        /// Configure base pipeline and flow data mocks.
        /// </summary>
        private static Mock<IPipeline> ConfigurePipeline(
            Mock<IFlowData> flowData)
        {
            var pipeline = new Mock<IPipeline>();
            flowData.SetupGet(d => d.Pipeline).Returns(pipeline.Object);
            return pipeline;
        }

        /// <summary>
        /// Configure flow data to return translation output data.
        /// </summary>
        private static void ConfigureTranslationData(
            Mock<IFlowData> flowData,
            TranslationEngineData translationData)
        {
            flowData.Setup(d => d.GetOrAdd(
                    It.IsAny<ITypedKey<ITranslationEngineData>>(),
                    It.IsAny<Func<IPipeline, ITranslationEngineData>>()))
                .Returns(translationData);
        }
    }
}






