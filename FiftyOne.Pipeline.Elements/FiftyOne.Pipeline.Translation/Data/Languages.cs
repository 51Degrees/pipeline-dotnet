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

using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Translation.Data;

/// <summary>
/// Set of translators for one or more languages.
/// Keys are the locale code e.g. "en_GB", "fr_FR", etc. and values are the
/// translators for those languages.
/// </summary>
internal class Languages
{
    /// <summary>
    /// Internal dictionary of translators.
    /// </summary>
    private readonly IDictionary<string, Translator> _translators;

    public Languages()
    {
        _translators = new Dictionary<string, Translator>(
            StringComparer.InvariantCultureIgnoreCase);
    }

    /// <summary>
    /// Add a language and its translator to the set of languages.
    /// </summary>
    /// <param name="language">
    /// Locale code for the language e.g. "en_GB", "fr_FR", etc.
    /// </param>
    /// <param name="translator">
    /// Translator for the language.
    /// </param>
    /// <exception cref="ArgumentNullException"></exception>
    public void AddLanguage(string language, Translator translator)
    {
        if (language == null || translator == null)
        {
            throw new ArgumentNullException();
        }

        _translators[language] = translator;
    }

    /// <summary>
    /// Gets the translator for the specified language if it exists.
    /// Returns true if it does, otherwise false.
    /// </summary>
    /// <param name="language"></param>
    /// <param name="translator"></param>
    /// <returns></returns>
    public bool TryGetTranslator(string language, out Translator translator)
    {
        return _translators.TryGetValue(language, out translator);
    }
}
