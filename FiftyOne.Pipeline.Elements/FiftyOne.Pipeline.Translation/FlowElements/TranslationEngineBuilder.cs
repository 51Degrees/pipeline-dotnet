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
using FiftyOne.Pipeline.Translation.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FiftyOne.Pipeline.Translation.FlowElements
{

    /// <summary>
    /// Fluent builder for <see cref="TranslationEngine"/>.
    /// </summary>
    public class TranslationEngineBuilder
    {
        private readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// List of properties to translate.
        /// </summary>
        private readonly List<TranslationProperty> _translationProperties;

        /// <summary>
        /// Key to the source element which the translation engine will read from.
        /// This is where the values to be translated come from.
        /// </summary>
        private string _sourceElementDataKey;

        /// <summary>
        /// Source files containing translations. These follow the naming convention
        /// 'abc.en_GB.yml' where 'abc' can be any identifier, 'en_GB' is the
        /// locale code, and 'yml' is the file extension. The locale code is used 
        /// to determine which language is contained in the translation files.
        /// Files must be in YAML format.
        /// </summary>
        private List<FileInfo> _sources;

        /// <summary>
        /// The behavior of the translation engine when a translation is missing
        /// for a value.
        /// </summary>
        private MissingTranslationBehavior _behavior;

        /// <summary>
        /// Optional fixed language for the translation engine to translate to.
        /// </summary>
        private string _fixedLanguage;

        /// <summary>
        /// Logger passed to the <see cref="TranslationData"/> instances
        /// created by the translation engine.
        /// </summary>
        private readonly ILogger<TranslationData> _dataLogger;

        /// <summary>
        /// Constructor.
        /// </summary>
        public TranslationEngineBuilder(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory
                ?? throw new ArgumentNullException(nameof(loggerFactory));
            _translationProperties = new List<TranslationProperty>();
            _sources = new List<FileInfo>();
            _dataLogger = _loggerFactory.CreateLogger<TranslationData>();
            _fixedLanguage = null;
            _behavior = MissingTranslationBehavior.Original;
        }

        /// <summary>
        /// Set source element data key.
        /// </summary>
        /// <param name="sourceElementDataKey">
        /// Key to the source element which the translation engine will read from.
        /// This is where the values to be translated come from.
        /// </param>
        /// <returns>
        /// This builder.
        /// </returns>
        public TranslationEngineBuilder SetSourceElementDataKey(
            string sourceElementDataKey)
        {
            _sourceElementDataKey = sourceElementDataKey;
            return this;
        }

        /// <summary>
        /// Set a fixed language for the translation engine to translate to.
        /// If this is set, the engine will not determine the language to translate
        /// to from the source element data, it will instead always translate to
        /// this language.
        /// </summary>
        /// <param name="language">
        /// Language to translate to.
        /// </param>
        /// <returns>
        /// This builder.
        /// </returns>
        public TranslationEngineBuilder SetFixedLanguage(
            string language)
        {
            _fixedLanguage = language;
            return this;
        }

        /// <summary>
        /// Set the behavior of the translation engine when a translation is
        /// missing for a value.
        /// </summary>
        /// <param name="behavior"></param>
        /// <returns>
        /// This builder.
        /// </returns>
        public TranslationEngineBuilder SetMissingTranslationBehaviour(
            MissingTranslationBehavior behavior)
        {
            _behavior = behavior;
            return this;
        }

        /// <summary>
        /// Add translated property. This defines a translation from one property
        /// to another.
        /// </summary>
        /// <param name="source">
        /// The key for the source property to translate.
        /// </param>
        /// <param name="destination">
        /// The key to store the translated value under on the translation engine data.
        /// </param>
        /// <returns>
        /// This builder.
        /// </returns>
        public TranslationEngineBuilder AddTranslation(
            string source,
            string destination)
        {
            if (source == null || destination == null)
            {
                throw new ArgumentNullException();
            }

            _translationProperties.Add(new TranslationProperty(source, destination));
            return this;
        }

        /// <summary>
        /// Add a source file containing translations. These follow the naming
        /// convention 'abc.en_GB.yml' where 'abc' can be any identifier, 
        /// 'en_GB' is the locale code, and 'yml' is the file extension. The 
        /// locale code is used to determine which language is contained in the
        /// translation files.
        /// Files must be in YAML format.
        /// The source can contain a wildcard to add multiple files e.g.
        /// 'abc.*.yml' to add all languages for the 'abc' identifier.
        /// </summary>
        /// <param name="filePath">
        /// The path to the source file where a wildcard can be used for the
        /// language component of the filename.
        /// </param>
        /// <returns>
        /// This builder.
        /// </returns>
        public TranslationEngineBuilder AddSource(string filePath)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }
            if (filePath.Contains('*'))
            {
                // The source is a wildcard, so get the directory and file name
                // and find all matching files.
                var directory = Path.GetDirectoryName(filePath);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    directory = Directory.GetCurrentDirectory();
                }
                var fileName = Path.GetFileName(filePath);
                var files = Directory.GetFiles(directory, fileName);
                _sources.AddRange(files.Select(f => new FileInfo(f)));
            }
            else
            {
                _sources.Add(new FileInfo(filePath));
            }
            return this;
        }

        /// <summary>
        /// Creates an instance of ElementData
        /// </summary>
        /// <param name="pipeline"></param>
        /// <param name="flowElement"></param>
        /// <returns></returns>
        private ITranslationData CreateData(
            IPipeline pipeline,
            FlowElementBase<
                ITranslationData,
                IElementPropertyMetaData> flowElement)
        {
            return new TranslationData(
                _dataLogger,
                pipeline);
        }

        /// <summary>
        /// Build a translation engine.
        /// </summary>
        public TranslationEngine Build()
        {
            return new TranslationEngine(
                _sourceElementDataKey,
                _translationProperties,
                _sources,
                _fixedLanguage,
                _behavior,
                _loggerFactory.CreateLogger<TranslationEngine>(),
                CreateData);
        }
    }
}