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
using FiftyOne.Pipeline.Core.TypedMap;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;

namespace FiftyOne.Pipeline.Engines.FlowElements
{
    /// <summary>
    /// Flow element that translates values from a single source element and
    /// stores translated values under its own element data key.
    /// </summary>
    public class TranslationEngine :
        FlowElementBase<ITranslationEngineData, IElementPropertyMetaData>,
        ITranslationEngine
    {
        private readonly IEvidenceKeyFilter _evidenceKeyFilter;
        private readonly IList<IElementPropertyMetaData> _properties;
        private string _sourceElementDataKey;
        private ITranslation[] _translations;
        private static readonly List<string> _evidenceKeyWhiteListValues = 
            new List<string>()
        {
            "query.translation",
            "header.accept-language"
        };

        /// <summary>
        /// Translation lookups
        /// </summary>
        private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>
            _translationLookups;

        /// <summary>
        /// Create a new translationLookupKey engine.
        /// </summary>
        /// <param name="sourceElementDataKey">
        /// Element data key of the source flow element.
        /// </param>
        /// <param name="translations">
        /// Translations to execute, keyed by source property name.
        /// </param>
        /// <param name="sources">
        /// sources used to perform translations. 
        /// </param>
        /// <param name="logger">
        /// Logger instance.
        /// </param>
        /// <param name="elementDataFactory">
        /// Optional element data factory override.
        /// </param>
        public TranslationEngine(
            string sourceElementDataKey,
            IEnumerable<ITranslation> translations,
            IEnumerable<FileInfo> sources,
            ILogger<FlowElementBase<ITranslationEngineData, IElementPropertyMetaData>> logger,
            Func<IPipeline,
                FlowElementBase<ITranslationEngineData,
                    IElementPropertyMetaData>, 
                ITranslationEngineData> elementDataFactory)
            : base(logger, elementDataFactory)
        {
            if (translations == null)
            {
                throw new ArgumentNullException(nameof(translations));
            }
            if (sources == null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            _translationLookups = ParseSources(sources);

            _sourceElementDataKey = sourceElementDataKey.Trim();

            _evidenceKeyFilter = 
                new EvidenceKeyFilterWhitelist(_evidenceKeyWhiteListValues);
            _translations = translations.ToArray();
            _properties = _translations
                .Select(translation => translation.DestinationProperty)
                .Distinct(StringComparer.InvariantCultureIgnoreCase)
                .Select(name =>
                    (IElementPropertyMetaData)new ElementPropertyMetaData(
                        this,
                        name,
                        typeof(object),
                        true))
                .ToList();
        }

        /// <summary>
        /// Unpacks and parses each source into a translationLookupKey lookup.
        /// </summary>
        /// <param name="sources"></param>
        /// <returns></returns>
        private IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>
            ParseSources(IEnumerable<FileInfo> sources)
        {
            var lookups = new Dictionary<string,
                IReadOnlyDictionary<string, string>>(
                    StringComparer.InvariantCultureIgnoreCase);

            var deserializer = new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .Build();

            foreach (var source in sources)
            {
                var lookupKey = Path.GetFileNameWithoutExtension(source.Name);

                Dictionary<string, string> parsed;
                using (var reader = source.OpenText())
                {
                    parsed = deserializer
                        .Deserialize<Dictionary<string, string>>(reader);
                }

                var lookup = parsed == null
                    ? new Dictionary<string, string>(
                        StringComparer.InvariantCultureIgnoreCase)
                    : new Dictionary<string, string>(
                        parsed,
                        StringComparer.InvariantCultureIgnoreCase);

                if (lookups.ContainsKey(lookupKey))
                {
                    throw new InvalidDataException(
                        $"Duplicate translation source key '{lookupKey}' from " +
                        $"'{source.FullName}'.");
                }

                lookups.Add(lookupKey, lookup);
            }

            return lookups;
        }

        /// <inheritdoc/>
        public override string ElementDataKey => "translation";

        /// <inheritdoc/>
        public string SourceElementDataKey => _sourceElementDataKey;

        /// <inheritdoc/>
        public override IEvidenceKeyFilter EvidenceKeyFilter => _evidenceKeyFilter;

        /// <inheritdoc/>
        public override IList<IElementPropertyMetaData> Properties => _properties;

        /// <inheritdoc/>
        protected override void ProcessInternal(IFlowData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            // Get the translation lookup key
            if (TryGetTargetTranslation(
                data,
                out string translationLookupKey) == false)
            {
                return;
            }

            // Get the source data from the flowdata. 
            if (TryGetSourceData(data, out IElementData sourceData) == false)
            {
                return;
            }

            // get or create a instance of TranslationElement Data.
            var translationData = data.GetOrAdd(
                ElementDataKeyTyped,
                CreateElementData);

            // Get the lookup required from the translation lookups
            if (_translationLookups.TryGetValue(
                    translationLookupKey, 
                    out var lookup) == false ||
                lookup == null)
            {
                return;
            }

            // iteration over every translation and perform translation 
            foreach (var translation in _translations)
            {
                if (TryGetSourceValue(
                   sourceData,
                   translation.SourceProperty,
                   out object sourceValue) == false)
                {
                    continue;
                }

                // process string 
                if (sourceValue is IAspectPropertyValue<string> aspectString &&
                    aspectString.HasValue)
                {
                    if (lookup.TryGetValue(
                        aspectString.Value, 
                        out string translatedValue) == false)
                    {
                        translatedValue = aspectString.Value;
                    }

                    translationData[translation.DestinationProperty] =
                        new AspectPropertyValue<string>(translatedValue);
                }

                // Process list of strings 
                if (sourceValue is IAspectPropertyValue<
                        IReadOnlyList<string>> aspectStringList &&
                    aspectStringList.HasValue)
                {
                    var newTranslatedValue = 
                        new AspectPropertyValue<List<string>>();
                    var values = new List<string>();
                    foreach (var listValue in aspectStringList.Value)
                    {
                        if (lookup.TryGetValue(
                            listValue,
                            out string translatedValue) == false)
                        {
                            translatedValue = listValue;
                        }
                        values.Add(translatedValue);
                    }
                    newTranslatedValue.Value = values;
                    translationData[translation.DestinationProperty]
                        = newTranslatedValue;
                }

                // Process Weighted values 
                if (sourceValue is IAspectPropertyValue<
                    IReadOnlyList<
                        IWeightedValue<string>>> aspectWeightedStringList &&
                    aspectWeightedStringList.HasValue)
                {
                    var newTranslatedValue = 
                        new AspectPropertyValue<List<WeightedValue<string>>>();
                    var values = new List<WeightedValue<string>>();
                    foreach (var listValue in aspectWeightedStringList.Value)
                    {
                        if (lookup.TryGetValue(
                            listValue.Value, 
                            out string translatedValue) == false)
                        {
                            translatedValue = listValue.Value;
                        }
                        values.Add(new WeightedValue<string>(
                            listValue.RawWeighting,
                            translatedValue));
                    }
                    newTranslatedValue.Value = values;
                    translationData[translation.DestinationProperty] 
                        = newTranslatedValue;
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
        /// Regex used to identify language locale codes in evidence
        /// </summary>
        private Regex _localeRegex = new Regex(
            @"[a-z]{2}_[A-Z]{2}", 
            RegexOptions.Compiled);

        /// <summary>
        ///  Goes through the Evidence in the flowdata to find the highest 
        ///  precidence evidence key and sets the translationLookupKey lookup
        ///  key.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="translationLookupKey"></param>
        /// <returns></returns>
        private bool TryGetTargetTranslation(
            IFlowData data,
            out string translationLookupKey)
        {
            translationLookupKey = null;

            if (data == null)
            {
                return false;
            }
            var evidence = data.GetEvidence();

            // get the highest precidence evidence key that has a locale code 
            foreach (var key in _evidenceKeyWhiteListValues)
            {
                var value = evidence[key] as string;
                
                if (value != null && _localeRegex.IsMatch(value))
                {
                    var match = _localeRegex.Match(value);
                    translationLookupKey = match.Value;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Retrieves the source value data from the flowdata. 
        /// </summary>
        /// <param name="data"></param>
        /// <param name="sourceData"></param>
        /// <returns></returns>
        private bool TryGetSourceData(IFlowData data, out IElementData sourceData)
        {
            sourceData = null;

            if (data == null || string.IsNullOrWhiteSpace(SourceElementDataKey))
            {
                return false;
            }

            var key = new TypedKey<IElementData>(SourceElementDataKey);
            return data.TryGetValue(key, out sourceData);
        }

        /// <summary>
        /// Gets the source value from the sourcedata
        /// </summary>
        /// <param name="sourceData"></param>
        /// <param name="sourceProperty"></param>
        /// <param name="sourceValue"></param>
        /// <returns></returns>
        private static bool TryGetSourceValue(
            IElementData sourceData,
            string sourceProperty,
            out object sourceValue)
        {
            sourceValue = null;

            if (sourceData == null ||
                string.IsNullOrWhiteSpace(sourceProperty))
            {
                return false;
            }

            sourceValue = sourceData[sourceProperty];
            return sourceValue != null;
        }
    }
}





