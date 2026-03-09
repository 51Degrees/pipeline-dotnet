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
using System;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.Pipeline.Engines.FlowElements
{
    /// <summary>
    /// Flow element that applies one or more translations and assigns the
    /// results into existing destination data based on pipeline property
    /// metadata.
    /// </summary>
    public class TranslationEngine :
        FlowElementBase<IElementData, IElementPropertyMetaData>,
        ITranslationEngine
    {
        private readonly IEvidenceKeyFilter _evidenceKeyFilter;
        private readonly IList<IElementPropertyMetaData> _properties;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="translations">
        /// The set of translations this engine should execute.
        /// </param>
        /// <param name="logger">
        /// Logger for this engine.
        /// </param>
        public TranslationEngine(
            IReadOnlyCollection<ITranslation> translations,
            ILogger<
                FlowElementBase<
                IElementData, 
                IElementPropertyMetaData>> logger)
            : base(logger)
        {
            if (translations == null)
            {
                throw new ArgumentNullException(nameof(translations));
            }

            if (translations.Count == 0)
            {
                throw new ArgumentException(
                    "At least one translation must be supplied.",
                    nameof(translations));
            }

            Translations = translations.ToList();
            _evidenceKeyFilter = BuildEvidenceFilter(Translations);
            _properties = new List<IElementPropertyMetaData>();
        }

        /// <inheritdoc/>
        public override string ElementDataKey => "translation";

        /// <inheritdoc/>
        public override IEvidenceKeyFilter EvidenceKeyFilter => _evidenceKeyFilter;

        /// <inheritdoc/>
        public override IList<IElementPropertyMetaData> Properties => _properties;

        /// <inheritdoc/>
        public IReadOnlyList<ITranslation> Translations { get; }

        /// <inheritdoc/>
        protected override void ProcessInternal(IFlowData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            foreach (var translation in Translations)
            {
                if (TryGetSourceValue(
                        data, 
                        translation.SourceProperty,
                        out object sourceValue) &&
                    TryGetAspectStringValue(
                        sourceValue, 
                        out IAspectPropertyValue<string> typedValue) &&
                    translation.TryTranslate(
                        data,
                        typedValue,
                        out IAspectPropertyValue<string> translatedValue))
                {
                    TryAssignTranslatedValue(
                        data,
                        translation.DestinationProperty,
                        translatedValue);
                }
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

        /// <summary>
        /// Builds the evidence key filter from all source
        /// evidence keys required by the configured translations.
        /// </summary>
        private static IEvidenceKeyFilter BuildEvidenceFilter(
            IReadOnlyList<ITranslation> translations)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var translation in translations)
            {
                if (translation == null)
                {
                    throw new ArgumentException(
                        "Translations cannot contain null entries.",
                        nameof(translations));
                }

                AddKey(keys, translation.SourceProperty);

                if (translation.EvidenceKeys != null)
                {
                    foreach (var key in translation.EvidenceKeys)
                    {
                        AddKey(keys, key);
                    }
                }
            }

            return new EvidenceKeyFilterWhitelist(keys.ToList());
        }

        /// <summary>
        /// Gets the source value from flow data using property metadata first,
        /// then falls back to evidence keys.
        /// </summary>s
        private static bool TryGetSourceValue(
            IFlowData data,
            string sourceProperty,
            out object sourceValue)
        {
            sourceValue = null;
            if (string.IsNullOrWhiteSpace(sourceProperty))
            {
                return false;
            }

            var propertyMatches = data.GetWhere(metaData =>
                    string.Equals(
                        metaData.Name,
                        sourceProperty,
                        StringComparison.InvariantCultureIgnoreCase))
                .ToArray();

            if (propertyMatches.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Source property '{sourceProperty}' " +
                    "matched multiple properties.");
            }
            if (propertyMatches.Length == 1)
            {
                sourceValue = propertyMatches.Single().Value;
                return true;
            }

            return data.TryGetEvidence(sourceProperty, out sourceValue);
        }

        /// <summary>
        /// Adds a key to the filter set if it is non-empty.
        /// </summary>
        private static void AddKey(ISet<string> keys, string key)
        {
            if (string.IsNullOrWhiteSpace(key) == false)
            {
                keys.Add(key.Trim());
            }
        }

        /// <summary>
        /// Converts raw values into <see cref="IAspectPropertyValue{T}"/>
        /// for translation processing.
        /// </summary>
        private static bool TryGetAspectStringValue(
            object value,
            out IAspectPropertyValue<string> aspectValue)
        {
            aspectValue = value as IAspectPropertyValue<string>;
            if (aspectValue != null)
            {
                return aspectValue.HasValue;
            }

            var text = value as string;
            if (text != null)
            {
                aspectValue = new AspectPropertyValue<string>(text);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Assigns the translated value to the destination element data
        /// resolved from pipeline metadata.
        /// </summary>
        private static bool TryAssignTranslatedValue(
            IFlowData data,
            string destinationProperty,
            IAspectPropertyValue<string> translatedValue)
        {
            if (TryGetDestinationElementDataKey(
                data.Pipeline,
                destinationProperty,
                out string elementDataKey))
            {
                var dataKeys = data.GetDataKeys();
                if (dataKeys != null &&
                    dataKeys.Contains(
                        elementDataKey,
                        StringComparer.InvariantCultureIgnoreCase) == false)
                {
                    return false;
                }

                var destinationData = data.Get(elementDataKey);
                if (destinationData != null)
                {
                    destinationData[destinationProperty] = translatedValue;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Resolves which element data key owns the destination property.
        /// Supports either "property" or "element.property" input.
        /// </summary>
        private static bool TryGetDestinationElementDataKey(
            IPipeline pipeline,
            string destinationProperty,
            out string elementDataKey)
        {
            elementDataKey = null;
            if (string.IsNullOrWhiteSpace(destinationProperty))
            {
                return false;
            }

            var separatorIndex = destinationProperty.LastIndexOf('.');

            if (separatorIndex > 0 &&
                separatorIndex < destinationProperty.Length - 1)
            {
                var explicitElementDataKey =
                    destinationProperty.Substring(0, separatorIndex);
                var explicitMatches = pipeline.ElementAvailableProperties
                    .Where(e =>
                        string.Equals(
                            e.Key,
                            explicitElementDataKey,
                            StringComparison.InvariantCultureIgnoreCase) &&
                        e.Value.Keys.Any(k =>
                            string.Equals(
                                k,
                                destinationProperty,
                                StringComparison.InvariantCultureIgnoreCase)))
                    .Select(e => e.Key)
                    .ToList();

                if (explicitMatches.Count == 1)
                {
                    elementDataKey = explicitMatches[0];
                    return true;
                }
            }

            var matches = pipeline.ElementAvailableProperties
                .Where(e => e.Value.Keys.Any(k =>
                    string.Equals(
                        k,
                        destinationProperty,
                        StringComparison.OrdinalIgnoreCase)))
                .Select(e => e.Key)
                .ToList();

            if (matches.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Destination property '{destinationProperty}' matched multiple elements.");
            }

            if (matches.Count == 1)
            {
                elementDataKey = matches[0];
                return true;
            }

            return false;
        }
    }
}
