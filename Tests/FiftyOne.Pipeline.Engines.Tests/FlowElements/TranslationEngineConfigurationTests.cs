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
using FiftyOne.Pipeline.Core.Configuration;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FiftyOne.Pipeline.Engines.Tests.FlowElements
{
    /// <summary>
    /// Minimal source data used by configuration integration tests.
    /// </summary>
    public class TranslationConfigSourceData : ElementDataBase
    {
        /// <summary>
        /// Creates source data.
        /// </summary>
        public TranslationConfigSourceData(
            ILogger<ElementDataBase> logger,
            IPipeline pipeline)
            : base(logger, pipeline)
        {
        }
    }

    /// <summary>
    /// Minimal source element for configuration integration tests.
    /// </summary>
    public class TranslationConfigSourceElement :
        FlowElementBase<IElementData, IElementPropertyMetaData>
    {
        private readonly string _elementDataKey;
        private readonly IList<IElementPropertyMetaData> _properties;
        private readonly IEvidenceKeyFilter _evidenceKeyFilter;

        /// <summary>
        /// Creates source element.
        /// </summary>
        public TranslationConfigSourceElement(
            ILogger<FlowElementBase<IElementData, IElementPropertyMetaData>> logger,
            string elementDataKey)
            : base(
                  logger,
                  (pipeline, element) => new TranslationConfigSourceData(
                      Microsoft.Extensions.Logging.Abstractions.NullLogger<ElementDataBase>.Instance,
                      pipeline))
        {
            _elementDataKey = elementDataKey;
            _properties = new List<IElementPropertyMetaData>()
            {
                new ElementPropertyMetaData(this, "Country", typeof(string), true)
            };
            _evidenceKeyFilter = new EvidenceKeyFilterWhitelist(
                new List<string>() { "query.country" });
        }

        /// <inheritdoc/>
        public override string ElementDataKey => _elementDataKey;

        /// <inheritdoc/>
        public override IEvidenceKeyFilter EvidenceKeyFilter => _evidenceKeyFilter;

        /// <inheritdoc/>
        public override IList<IElementPropertyMetaData> Properties => _properties;

        /// <inheritdoc/>
        protected override void ProcessInternal(IFlowData data)
        {
            if (data.TryGetEvidence("query.country", out string country))
            {
                var sourceData = data.GetOrAdd(ElementDataKeyTyped, CreateElementData);
                sourceData["Country"] = new AspectPropertyValue<string>(country);
            }
        }

        /// <inheritdoc/>
        protected override void ManagedResourcesCleanup()
        {
        }

        /// <inheritdoc/>
        protected override void UnmanagedResourcesCleanup()
        {
        }
    }

    /// <summary>
    /// Builder for <see cref="TranslationConfigSourceElement"/> used by
    /// configuration integration tests.
    /// </summary>
    public class TranslationConfigSourceElementBuilder
    {
        private readonly ILoggerFactory _loggerFactory;
        private string _elementDataKey = "source-element";

        /// <summary>
        /// Creates builder.
        /// </summary>
        public TranslationConfigSourceElementBuilder(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        /// <summary>
        /// Sets element data key.
        /// </summary>
        public TranslationConfigSourceElementBuilder SetElementDataKey(string elementDataKey)
        {
            _elementDataKey = elementDataKey;
            return this;
        }

        /// <summary>
        /// Builds source element.
        /// </summary>
        public TranslationConfigSourceElement Build()
        {
            return new TranslationConfigSourceElement(
                _loggerFactory.CreateLogger<TranslationConfigSourceElement>(),
                _elementDataKey);
        }
    }

    /// <summary>
    /// Configuration-friendly builder for translation engine tests.
    /// </summary>
    public class TranslationEngineConfigBuilder
    {
        private readonly TranslationEngineBuilder _builder;

        /// <summary>
        /// Creates builder.
        /// </summary>
        public TranslationEngineConfigBuilder(ILoggerFactory loggerFactory)
        {
            _builder = new TranslationEngineBuilder(loggerFactory);
        }

        /// <summary>
        /// Sets the source element data key.
        /// </summary>
        public TranslationEngineConfigBuilder SetSourceElementDataKey(string sourceElementDataKey)
        {
            _builder.SetSourceElementDataKey(sourceElementDataKey);
            return this;
        }

        /// <summary>
        /// Adds translation sources.
        /// </summary>
        public TranslationEngineConfigBuilder SetSources(IList<string> sources)
        {
            _builder.AddSources(sources);
            return this;
        }

        /// <summary>
        /// Adds translations from "Source:Destination" mappings.
        /// </summary>
        public TranslationEngineConfigBuilder SetTranslations(IList<string> translations)
        {
            if (translations == null)
            {
                throw new ArgumentNullException(nameof(translations));
            }

            foreach (var mapping in translations)
            {
                var parts = mapping.Split(new[] { ':' }, 2);
                if (parts.Length != 2 ||
                    string.IsNullOrWhiteSpace(parts[0]) ||
                    string.IsNullOrWhiteSpace(parts[1]))
                {
                    throw new ArgumentException(
                        "Translation mapping must be in the form 'Source:Destination'.",
                        nameof(translations));
                }

                _builder.AddTranslation(parts[0], parts[1]);
            }

            return this;
        }

        /// <summary>
        /// Builds translation engine.
        /// </summary>
        public TranslationEngine Build()
        {
            return _builder.Build();
        }
    }

    /// <summary>
    /// Configuration integration tests for translation engine.
    /// </summary>
    [TestClass]
    public class TranslationEngineConfigurationTests
    {
        private TestLoggerFactory _loggerFactory;
        private List<string> _tempPaths;

        /// <summary>
        /// Creates logger factory.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            _loggerFactory = new TestLoggerFactory();
            _tempPaths = new List<string>();
        }

        /// <summary>
        /// Verifies no warnings/errors were logged.
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
        /// Verifies a pipeline built from configuration can translate values
        /// from a source element using file-based translation sources.
        /// </summary>
        [TestMethod]
        public void PipelineFromConfiguration_TranslationEngine_TranslatesFromSourceElement()
        {
            var tempDir = CreateTempDirectory();
            var enGb = CreateTranslationFile(
                tempDir,
                "en_GB.yaml",
                new Dictionary<string, string>()
                {
                    { "Spain", "Spain-GB" }
                });

            var pipelineBuilder = new PipelineBuilder(_loggerFactory);
            var options = new PipelineOptions();
            options.Elements.Add(new ElementOptions()
            {
                BuilderName = "TranslationConfigSourceElementBuilder",
                BuildParameters = new Dictionary<string, object>()
                {
                    { "ElementDataKey", "source-element" }
                }
            });
            options.Elements.Add(new ElementOptions()
            {
                BuilderName = "TranslationEngineConfigBuilder",
                BuildParameters = new Dictionary<string, object>()
                {
                    { "SourceElementDataKey", "source-element" },
                    { "Sources", new List<string>() { enGb.FullName } },
                    { "Translations", new List<string>() { "Country:CountryTranslated" } }
                }
            });

            using (var pipeline = pipelineBuilder.BuildFromConfiguration(options))
            {
                using (var flowData = pipeline.CreateFlowData())
                {
                    flowData.AddEvidence("query.country", "Spain");
                    flowData.AddEvidence("query.translation", "en_GB");
                    flowData.Process();

                    var translationData = flowData.Get("translation");
                    var translated =
                        (IAspectPropertyValue<string>)translationData["CountryTranslated"];

                    Assert.AreEqual("Spain-GB", translated.Value);
                }
            }
        }

        private string CreateTempDirectory()
        {
            var path = Path.Combine(
                Path.GetTempPath(),
                "translation-engine-config-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            _tempPaths.Add(path);
            return path;
        }

        private static FileInfo CreateTranslationFile(
            string directory,
            string fileName,
            IDictionary<string, string> translations)
        {
            var path = Path.Combine(directory, fileName);
            var lines = translations.Select(kvp => $"{kvp.Key}: {kvp.Value}");
            File.WriteAllLines(path, lines);
            return new FileInfo(path);
        }
    }
}
