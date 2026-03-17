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
using FiftyOne.Pipeline.Translation.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;

[assembly: InternalsVisibleTo("FiftyOne.Pipeline.TranslationTests")]
namespace FiftyOne.Pipeline.Translation.FlowElements;

/// <summary>
/// Flow element that translates values from a single source element and
/// stores translated values under its own element data key.
/// 
/// Translations are provided in YAML format files, where the naming defines
/// the language contained in the file. See <see cref="TranslationEngineBuilder.AddSource(string)"/>.
/// 
/// The language to translate to is deteined by looking through the evidence
/// for a key containing a locale code. The keys checked are defined in
/// <see cref="EvidenceKeyFilter"/>. If a fixed language is provided in the
/// builder, this will be used instead, regardless of the evidence.
/// 
/// The properties to be translated are configured in the builder by supplying
/// the name of the input property, and the name of the output property. See
/// <see cref="TranslationEngineBuilder.AddTranslation(string, string)"/>.
/// 
/// The element that the input properties are fetched from is configured by
/// supplying the element data key. See <see cref="TranslationEngineBuilder.SetSourceElementDataKey(string)"/>.
/// 
/// Only string based types are supported for translation e.g. string,
/// IList<string>, IAspectPropertyValue<string>, etc. and the type of the output
/// property will match the input.
/// 
/// The behaviour of the engine when a translation is missing for a value can be
/// configured using the <see cref="MissingTranslationBehaviour"/> enum.
/// See <see cref="TranslationEngineBuilder.SetMissingTranslationBehaviour(MissingTranslationBehaviour)"/>.
/// </summary>
public class TranslationEngine :
    FlowElementBase<ITranslationData, IElementPropertyMetaData>,
    ITranslationEngine
{
    /// <summary>
    /// Translator with no translations configured. Used to populate translations
    /// when there is no target language or translator available, to ensure the
    /// output properties are always populated with a value, even if that value
    /// is just an error message.
    /// </summary>
    private readonly Translator _emptyTranslator;

    /// <summary>
    /// Locale code of the fixed translation language, if provided.
    /// </summary>
    private readonly string _fixedLanguage;

    /// <summary>
    /// <see cref="EvidenceKeyFilter"/>.
    /// </summary>
    private readonly IEvidenceKeyFilter _evidenceKeyFilter;

    /// <summary>
    /// <see cref="Properties"/>.
    /// </summary>
    private readonly IList<IElementPropertyMetaData> _properties;

    /// <summary>
    /// <see cref="SourceElementDataKey"/>.
    /// </summary>
    private string _sourceElementDataKey;

    /// <summary>
    /// Defines the behaviour when there is no translation available for a
    /// value.
    /// </summary>
    private readonly MissingTranslationBehaviour _behaviour;

    /// <summary>
    /// Input and output property names to translate.
    /// </summary>
    private TranslationProperty[] _translationProperties;

    /// <summary>
    /// The keys which should be used to get the locale code for the language
    /// to translate to.
    /// </summary>
    private static readonly List<string> _evidenceKeyWhiteListValues = 
        new List<string>()
    {
        "query.translation",
        "query.accept-language",
        "header.accept-language"
    };

    /// <summary>
    /// Regex used to identify language locale codes in evidence
    /// </summary>
    private static Regex _localeRegex = new Regex(
        @"[a-z]{2}_[A-Z]{2}",
        RegexOptions.Compiled);

    /// <summary>
    /// Translation lookups
    /// </summary>
    internal readonly Languages Languages;

    /// <inheritdoc/>
    public override string ElementDataKey => "translation";

    /// <inheritdoc/>
    public string SourceElementDataKey => _sourceElementDataKey;

    /// <inheritdoc/>
    public override IEvidenceKeyFilter EvidenceKeyFilter => _evidenceKeyFilter;

    /// <inheritdoc/>
    public override IList<IElementPropertyMetaData> Properties => _properties;

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
    /// <param name="fixedLanguage">
    /// Fixed language to translate to. If this is set, the engine will always
    /// translate to this language. Otherwise (null or empty) the engine will
    /// get the language from the evidence.
    /// </param>
    /// <param name="behaviour">
    /// The behaviour of the translation engine when a translation is missing
    /// for a value.
    /// </param>
    /// <param name="logger">
    /// Logger instance.
    /// </param>
    /// <param name="elementDataFactory">
    /// Optional element data factory override.
    /// </param>
    public TranslationEngine(
        string sourceElementDataKey,
        IEnumerable<TranslationProperty> translations,
        IEnumerable<FileInfo> sources,
        string fixedLanguage,
        MissingTranslationBehaviour behaviour,
        ILogger<FlowElementBase<ITranslationData, IElementPropertyMetaData>> logger,
        Func<IPipeline,
            FlowElementBase<ITranslationData,
                IElementPropertyMetaData>, 
            ITranslationData> elementDataFactory)
        : base(logger, elementDataFactory)
    {
        if (translations == null || translations.Count() == 0)
        {
            throw new ArgumentNullException(
                nameof(translations),
                "At least one property translation must be configured.");
        }
        if (sources == null || sources.Count() == 0)
        {
            throw new ArgumentNullException(
                nameof(sources),
                "At least one source file must be configured.");
        }
        if (string.IsNullOrWhiteSpace(sourceElementDataKey))
        {
            throw new ArgumentNullException(
                nameof(sourceElementDataKey),
                "The source element key must be configured.");
        }

        _sourceElementDataKey = sourceElementDataKey.Trim();
        _behaviour = behaviour;
        _emptyTranslator = new Translator(behaviour);
        _fixedLanguage = fixedLanguage != null ? ValidateLocale(fixedLanguage) : null;
        Languages = ParseSources(sources, _behaviour);

        _evidenceKeyFilter = 
            new EvidenceKeyFilterWhitelist(_evidenceKeyWhiteListValues);
        _translationProperties = translations.ToArray();
        _properties = _translationProperties
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

    /// <inheritdoc/>
    protected override void ProcessInternal(IFlowData data)
    {
        // get or create a instance of TranslationElement Data.
        var translationData = data.GetOrAdd(
            ElementDataKeyTyped,
            CreateElementData);

        // Get the source data from the flowdata. 
        if (TryGetSourceData(data, out IElementData sourceData) == false)
        {
            data.AddError(
                new KeyNotFoundException($"The source data '{SourceElementDataKey}' " +
                $"could not be found in the FlowData."),
                this);
            return;
        }

        // Get the target language.
        if (TryGetTargetLanguage(
            data,
            out string language) == false)
        {
            if (_behaviour == MissingTranslationBehaviour.FlowError)
            {
                data.AddError(
                    new KeyNotFoundException($"The evidence did not contain a " +
                    $"language to translate to."),
                    this);
            }
            else
            {
                Populate(sourceData, _emptyTranslator, translationData, data);
            }
            return;
        }

        // Get the translator from the languages configured for this engine.
        if (Languages.TryGetTranslator(
            language,
            out var translations) == false)
        {
            if (_behaviour == MissingTranslationBehaviour.FlowError)
            {
                data.AddError(new KeyNotFoundException($"There was no translator " +
                    $"configured for the language '{language}'."),
                    this);
            }
            else
            {
                Populate(sourceData, _emptyTranslator, translationData, data);
            }
            return;
        }

        // Iterate over every translation and perform translation.
        Populate(sourceData, translations, translationData, data);
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
    /// Unpacks and parses source files into a Languages instance containing
    /// a Translator for each file.
    /// </summary>
    /// <param name="sources"></param>
    /// <param name="behaviour"></param>
    /// <returns></returns>
    private static Languages ParseSources(
        IEnumerable<FileInfo> sources,
        MissingTranslationBehaviour behaviour)
    {
        var languages = new Languages();

        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();

        foreach (var source in sources)
        {
            var language = GetLanguageName(source.Name);

            Dictionary<string, string> translations;
            using (var reader = source.OpenText())
            {
                translations = deserializer
                    .Deserialize<Dictionary<string, string>>(reader);
            }
            if (translations == null)
            {
                throw new InvalidDataException($"The source for file " +
                    $"{source.Name} could not be parsed into a valid " +
                    $"translation lookup.");
            }

            languages.AddLanguage(language, new Translator(translations, behaviour));
        }

        return languages;
    }

    /// <summary>
    /// Get the language locale code from the source file name.
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="InvalidDataException"></exception>
    private static string GetLanguageName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Source name cannot be null or whitespace.",
                nameof(name));
        }

        var parts = name.Split('.');
        if (parts.Length < 3)
        {
            throw new InvalidDataException(
                $"Source name '{name}' does not have the correct format " +
                $"name. It should be 'somename.locale.yml' " +
                $"e.g. 'countries.en_GB.yml'.");
        }
        var locale = ValidateLocale(parts[parts.Length - 2]);
        if (locale == null)
        {
            throw new InvalidDataException(
                $"Source name '{name}' does not contain a valid locale code.");
        }
        return locale;
    }

    /// <summary>
    /// Get the validated locale code.
    /// </summary>
    /// <param name="locale">Code to validate</param>
    /// <returns>Valid locale code, or null</returns>
    private static string ValidateLocale(string locale)
    {
        var match = _localeRegex.Match(locale);
        return match.Success ? match.Value : null;
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

    /// <summary>
    /// Populates the translation data wither all configured translations using
    /// the provided translator.
    /// </summary>
    /// <param name="sourceData"></param>
    /// <param name="translator"></param>
    /// <param name="translationData"></param>
    /// <param name="flowData"></param>
    private void Populate(
        IElementData sourceData,
        Translator translator,
        ITranslationData translationData,
        IFlowData flowData)
    {
        foreach (var property in _translationProperties)
        {
            if (TryGetSourceValue(
               sourceData,
               property.SourceProperty,
               out object sourceValue) == false)
            {
                // Here there is no way to know what type the source property
                // is, so we don't have to match it. Meaning we can use an
                // AspectPropertyValue.
                var value = new AspectPropertyValue<string>();
                value.NoValueMessage = $"The source property " +
                    $"'{property.SourceProperty}' could not be found in " +
                    $"the source data.";
                translationData[property.DestinationProperty] = value;
            }
            else
            {
                var errors = new List<Exception>();
                translationData[property.DestinationProperty] =
                    translator.Translate(sourceValue, errors);
                if (errors.Count > 0)
                {
                    foreach (var error in errors)
                    {
                        flowData.AddError(error, this);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Goes through the Evidence in the FlowData to find the highest 
    /// precidence evidence key and sets the target language.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="language"></param>
    /// <returns></returns>
    private bool TryGetTargetLanguage(
        IFlowData data,
        out string language)
    {
        language = null;

        if (_fixedLanguage != null)
        {
            // Check for a fixed language first.
            language = _fixedLanguage;
            return true;
        }

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
                language = match.Value;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Retrieves the source value data from the FlowData. 
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
}
