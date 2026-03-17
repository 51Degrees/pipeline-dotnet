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
        /// 'abc.en_GB.yml' where 'abc' can be any idenitifier, 'en_GB' is the
        /// locale code, and 'yml' is the file extension. The locale code is used 
        /// to determine which language is contained in the translation files.
        /// Files must be in YAML format.
        /// </summary>
        private List<FileInfo> _sources;

        /// <summary>
        /// The behaviour of the translation engine when a translation is missing
        /// for a value.
        /// </summary>
        private MissingTranslationBehaviour _behaviour;
        private string _fixedLanguage = null;
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
            _behaviour = MissingTranslationBehaviour.Original;
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
        /// Language to transate to.
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
        /// Set the behaviour of the translation engine when a translation is
        /// missing for a value.
        /// </summary>
        /// <param name="behaviour"></param>
        /// <returns>
        /// This builder.
        /// </returns>
        public TranslationEngineBuilder SetMissingTranslationBehaviour(
        MissingTranslationBehaviour behaviour)
        {
            _behaviour = behaviour;
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
        /// Add a source file containing translations. These follow the naming convention
        /// 'abc.en_GB.yml' where 'abc' can be any idenitifier, 'en_GB' is the
        /// locale code, and 'yml' is the file extension. The locale code is used 
        /// to determine which language is contained in the translation files.
        /// Files must be in YAML format.
        /// The source can contain a wildcard to add multiple files e.g.
        /// 'abc.*.yml' to add all languages for the 'abc' identifier.
        /// </summary>
        /// <returns>
        /// This builder.
        /// </returns>
        public TranslationEngineBuilder AddSource(string source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (source.Contains('*'))
            {
                // The source is a wildcard, so get the directory and file name
                // and find all matching files.
                var directory = Path.GetDirectoryName(source);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    directory = Directory.GetCurrentDirectory();
                }
                var fileName = Path.GetFileName(source);
                var files = Directory.GetFiles(directory, fileName);
                _sources.AddRange(files.Select(f => new FileInfo(f)));
            }
            else
            {
                _sources.Add(new FileInfo(source));
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
                _behaviour,
                _loggerFactory.CreateLogger<TranslationEngine>(),
                CreateData);
        }
    }
}
