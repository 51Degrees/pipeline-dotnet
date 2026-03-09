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

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Engines.FlowElements
{
    /// <summary>
    /// Fluent builder for <see cref="TranslationEngine"/>.
    /// </summary>
    public class TranslationEngineBuilder
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly List<ITranslation> _translations;
        private string _elementDataKey;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="loggerFactory">
        /// Logger factory used when creating engine instances.
        /// </param>
        public TranslationEngineBuilder(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory 
                ?? throw new ArgumentNullException(nameof(loggerFactory));
            _translations = new List<ITranslation>();
        }

        /// <summary>
        /// Set the element data key for the built engine.
        /// </summary>
        /// <param name="elementDataKey">
        /// Element data key.
        /// </param>
        /// <returns>
        /// This builder instance.
        /// </returns>
        public TranslationEngineBuilder SetElementDataKey(
            string elementDataKey)
        {
            _elementDataKey = elementDataKey;
            return this;
        }

        /// <summary>
        /// Add a translation to the engine.
        /// </summary>
        /// <param name="translation">
        /// Translation implementation.
        /// </param>
        /// <returns>
        /// This builder instance.
        /// </returns>
        public TranslationEngineBuilder AddTranslation(
            ITranslation translation)
        {
            if (translation == null)
            {
                throw new ArgumentNullException(nameof(translation));
            }

            _translations.Add(translation);
            return this;
        }

        /// <summary>
        /// Add multiple translations to the engine.
        /// </summary>
        /// <param name="translations">
        /// Translation implementations.
        /// </param>
        /// <returns>
        /// This builder instance.
        /// </returns>
        public TranslationEngineBuilder AddTranslations(
            IEnumerable<ITranslation> translations)
        {
            if (translations == null)
            {
                throw new ArgumentNullException(nameof(translations));
            }

            foreach (var translation in translations)
            {
                AddTranslation(translation);
            }
            return this;
        }

        /// <summary>
        /// Build a translation engine.
        /// </summary>
        /// <returns>
        /// A new <see cref="TranslationEngine"/> instance.
        /// </returns>
        public TranslationEngine Build()
        {
            return new TranslationEngine(
                _translations,
                _loggerFactory.CreateLogger<TranslationEngine>());
        }
    }
}
