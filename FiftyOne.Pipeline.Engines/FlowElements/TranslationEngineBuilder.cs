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
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace FiftyOne.Pipeline.Engines.FlowElements
{
    /// <summary>
    /// Fluent builder for <see cref="TranslationEngine"/>.
    /// </summary>
    public class TranslationEngineBuilder
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly List<ITranslation> _translations;
        private string _sourceElementDataKey;
        private List<FileInfo> _sources;
        private readonly ILogger<TranslationEngineData> _dataLogger;

        /// <summary>
        /// Constructor.
        /// </summary>
        public TranslationEngineBuilder(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory 
                ?? throw new ArgumentNullException(nameof(loggerFactory));
            _translations = new List<ITranslation>();
            _sources = new List<FileInfo>();
            _dataLogger = _loggerFactory.CreateLogger<TranslationEngineData>();
        }

        /// <summary>
        /// Set source element data key.
        /// </summary>
        public TranslationEngineBuilder SetSourceElementDataKey(
            string sourceElementDataKey)
        {
            _sourceElementDataKey = sourceElementDataKey;
            return this;
        }

        /// <summary>
        /// Add a translation.
        /// </summary>
        public TranslationEngineBuilder AddTranslation(
            string source, 
            string destination)
        {
            if (source == null || destination == null)
            {
                throw new ArgumentNullException();
            }

            _translations.Add(new Translation(source, destination));
            return this;
        }

        /// <summary>
        /// Add multiple translation.
        /// </summary>
        public TranslationEngineBuilder AddTranslations(
            IReadOnlyDictionary<string,string> translations)
        {
            if (translations == null)
            {
                throw new ArgumentNullException(nameof(translations));
            }

            foreach (var translation in translations)
            {
                AddTranslation(translation.Key, translation.Value);
            }
            return this;
        }

        /// <summary>
        /// Add a source.
        /// </summary>
        public TranslationEngineBuilder AddSource(string source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            _sources.Add(new FileInfo(source));
            return this;
        }


        /// <summary>
        /// Add multiple sources.
        /// </summary>
        public TranslationEngineBuilder AddSources(IEnumerable<string> sources)
        {
            if (sources == null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            foreach (var source in sources)
            {
                AddSource(source);
            }
            return this;
        }

        /// <summary>
        /// Creates an instance of ElementData
        /// </summary>
        /// <param name="pipeline"></param>
        /// <param name="flowElement"></param>
        /// <returns></returns>
        private ITranslationEngineData CreateData(
            IPipeline pipeline,
            FlowElementBase<
                ITranslationEngineData, 
                IElementPropertyMetaData> flowElement)
        {
            return new TranslationEngineData(
                _dataLogger,
                pipeline);
        }

        /// <summary>
        /// Build a translation engine.
        /// </summary>
        public TranslationEngine Build()
        {
            if (string.IsNullOrWhiteSpace(_sourceElementDataKey))
            {
                throw new ArgumentException(
                    "Source element data key must be supplied.",
                    nameof(_sourceElementDataKey));
            }

            return new TranslationEngine(
                _sourceElementDataKey,
                _translations,
                _sources,
                _loggerFactory.CreateLogger<TranslationEngine>(),
                CreateData);
        }
    }
}
