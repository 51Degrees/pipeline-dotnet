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
using FiftyOne.Pipeline.Engines.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.Pipeline.Translation.Data;

/// <summary>
/// Translator used to translate values based on a set of translations.
/// The translator supports translating various string types. The result is
/// the same type as the source e.g. a string will be translated to a
/// string, a list of strings will be translated to a list of strings, etc.
/// </summary>
internal class Translator
{
    /// <summary>
    /// Internal translation dictionary.
    /// </summary>
    private readonly IReadOnlyDictionary<string, string> _translations;

    /// <summary>
    /// The behaviour to use when a translation is missing.
    /// </summary>
    private readonly MissingTranslationBehaviour _behaviour;

    /// <summary>
    /// Default constructor. Initializes the translator with an empty set of
    /// translations.
    /// </summary>
    /// <param name="behaviour">
    /// The behaviour to use when a translation is missing.
    /// </param>
    public Translator(MissingTranslationBehaviour behaviour)
        : this(new Dictionary<string, string>(), behaviour)
    { }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="translations">
    /// The translations to use for translation. The key is the source
    /// value and the value is the translated value.
    /// </param>
    /// <param name="behaviour">
    /// The behaviour to use when a translation is missing.
    /// </param>
    /// <exception cref="ArgumentNullException"></exception>
    public Translator(
        IReadOnlyDictionary<string, string> translations,
        MissingTranslationBehaviour behaviour)
    {
        if (translations == null)
        {
            throw new ArgumentNullException(nameof(translations));
        }
        _translations = new Dictionary<string, string>(
            translations,
            StringComparer.InvariantCultureIgnoreCase);
        _behaviour = behaviour;
    }

    /// <summary>
    /// Translate the object to the language this translator is configured
    /// for. The translator supports translating various string types.
    /// The result is the same type as the source e.g. a string will be
    /// translated to a string, a list of strings will be translated to a
    /// list of strings, etc.
    /// For AspectPropertyValue types, if the value has no value, then the
    /// no value message will be copied to the result.
    /// </summary>
    /// <param name="value">
    /// Value to translate.
    /// </param>
    /// <param name="errors">
    /// List of errors to add to if any errors are encountered during
    /// translation.
    /// </param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public object Translate(object value, IList<Exception> errors)
    {
        if (value is string stringValue)
        {
            return TranslateTyped(stringValue, errors);
        }
        if (value is IReadOnlyList<string> listValue)
        {
            return TranslateTyped(listValue, errors);
        }
        else if (value is IAspectPropertyValue<string> aspectValue)
        {
            return TranslateTyped(aspectValue, errors);
        }
        else if (value is IAspectPropertyValue<IReadOnlyList<string>> valueList)
        {
            return TranslateTyped(valueList, errors);
        }
        else if (value is IAspectPropertyValue<
                IReadOnlyList<
                    IWeightedValue<string>>> valueWeightedList)
        {
            return TranslateTyped(valueWeightedList, errors);
        }
        else
        {
            // The type is not one of the implemented types, so throw an
            // exception.
            throw new NotSupportedException($"The value type " +
                $"'{value.GetType().FullName}' is not supported for" +
                $"translation.");
        }
    }

    /// <summary>
    /// String type.
    /// This is the base type for translation, so the other types will
    /// call this method.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="errors"></param>
    /// <returns></returns>
    private string TranslateTyped(string value, IList<Exception> errors)
    {
        if (_translations.TryGetValue(value, out var result) &&
            string.IsNullOrWhiteSpace(result) == false)
        {
            return result;
        }
        else
        {
            switch (_behaviour)
            {
                case MissingTranslationBehaviour.EmptyString:
                    return string.Empty;
                case MissingTranslationBehaviour.FlowError:
                    errors.Add(new KeyNotFoundException(
                        $"There was no translation found for " +
                        $"the value '{value}'."));
                    return null;
                case MissingTranslationBehaviour.Original:
                default:
                    return value;
            }
        }
    }

    /// <summary>
    /// String list type.
    /// </summary>
    /// <param name="values"></param>
    /// <param name="errors"></param>
    /// <returns></returns>
    private IReadOnlyList<string> TranslateTyped(
        IReadOnlyList<string> values,
        IList<Exception> errors)
    {
        return values.Select(i => TranslateTyped(i, errors)).ToList();
    }

    /// <summary>
    /// Aspect property value string type.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="errors"></param>
    /// <returns></returns>
    private IAspectPropertyValue<string> TranslateTyped(
        IAspectPropertyValue<string> value,
        IList<Exception> errors)
    {
        if (value.HasValue)
        {
            return new AspectPropertyValue<string>(TranslateTyped(
                value.Value,
                errors));
        }
        else
        {
            return CopyNoValue(value);
        }
    }

    /// <summary>
    /// Aspect property value string list type.
    /// </summary>
    /// <param name="values"></param>
    /// <param name="errors"></param>
    /// <returns></returns>
    private IAspectPropertyValue<IReadOnlyList<string>> TranslateTyped(
        IAspectPropertyValue<IReadOnlyList<string>> values,
        IList<Exception> errors)
    {
        if (values.HasValue)
        {
            return new AspectPropertyValue<IReadOnlyList<string>>(
                TranslateTyped(values.Value, errors));
        }
        else
        {
            return CopyNoValue(values);
        }
    }

    /// <summary>
    /// Aspect property value weighted strings type.
    /// </summary>
    /// <param name="values"></param>
    /// <param name="errors"></param>
    /// <returns></returns>
    private IAspectPropertyValue<IReadOnlyList<IWeightedValue<string>>> TranslateTyped(
        IAspectPropertyValue<IReadOnlyList<IWeightedValue<string>>> values,
        IList<Exception> errors)
    {
        if (values.HasValue)
        {
            return new AspectPropertyValue<IReadOnlyList<IWeightedValue<string>>>(
                values.Value.Select(i => new WeightedValue<string>(
                    i.RawWeighting,
                    TranslateTyped(i.Value, errors))).ToList());
        }
        else
        {
            return CopyNoValue(values);
        }
    }

    /// <summary>
    /// Copy the no value message from the source value to the destination.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value"></param>
    /// <returns></returns>
    private IAspectPropertyValue<T> CopyNoValue<T>(IAspectPropertyValue<T> value)
    {
        var result = new AspectPropertyValue<T>();
        result.NoValueMessage = value.NoValueMessage;
        return result;
    }
}
