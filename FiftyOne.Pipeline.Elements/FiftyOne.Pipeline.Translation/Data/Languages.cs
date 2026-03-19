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
using System.Linq;
using System.Net.Http.Headers;

namespace FiftyOne.Pipeline.Translation.Data
{
    /// <summary>
    /// Set of translators for one or more languages, with static utility
    /// methods for parsing Accept-Language headers and resolving language
    /// tags against available locales.
    /// </summary>
    public class Languages
    {
        /// <summary>
        /// Internal dictionary of translators.
        /// </summary>
        private readonly IDictionary<string, Translator> _translators;

        /// <summary>
        /// Constructor.
        /// </summary>
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
        internal void AddLanguage(string language, Translator translator)
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
        /// <param name="language">
        /// A locale code (e.g. "fr_FR") or a full Accept-Language header
        /// value (e.g. "es,de-DE;q=0.8,en;q=0.5").
        /// </param>
        /// <param name="translator">
        /// The translator for the matched language, or null if not found.
        /// </param>
        /// <returns></returns>
        internal bool TryGetTranslator(
            string language, out Translator translator)
        {
            return TryGetTranslator(language, out translator, out _);
        }

        /// <summary>
        /// Gets the translator and matched locale for the specified language
        /// if it exists. Returns true if it does, otherwise false.
        /// </summary>
        /// <param name="language">
        /// A locale code (e.g. "fr_FR") or a full Accept-Language header
        /// value (e.g. "es,de-DE;q=0.8,en;q=0.5").
        /// </param>
        /// <param name="translator">
        /// The translator for the matched language, or null if not found.
        /// </param>
        /// <param name="matchedLocale">
        /// The locale key that was matched (e.g. "fr_FR", "es_ES"), or
        /// null if no match was found.
        /// </param>
        /// <returns></returns>
        internal bool TryGetTranslator(
            string language,
            out Translator translator,
            out string matchedLocale)
        {
            translator = null;
            matchedLocale = null;

            if (TryResolveLocale(
                language, _translators.Keys, out var locale) &&
                _translators.TryGetValue(locale, out translator))
            {
                matchedLocale = locale;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Parses an Accept-Language header value (e.g.
        /// "es,de-DE;q=0.8,en;q=0.5") into an ordered list of normalized
        /// language tags. Tags are ordered by quality (descending), with
        /// dashes replaced by underscores (e.g. "en-GB" becomes "en_GB").
        /// </summary>
        /// <param name="acceptLanguage">
        /// The raw Accept-Language header value.
        /// </param>
        /// <returns>
        /// An ordered enumerable of normalized language tags, highest
        /// preference first.
        /// </returns>
        public static IEnumerable<string> ParseAcceptLanguage(
            string acceptLanguage)
        {
            if (string.IsNullOrWhiteSpace(acceptLanguage))
            {
                return Enumerable.Empty<string>();
            }

            return acceptLanguage.Split(',')
                .Select(StringWithQualityHeaderValue.Parse)
                .OrderByDescending(i => i.Quality ?? 1)
                .Select(i => i.Value.ToString().Trim().Replace('-', '_'))
                .Where(s => !string.IsNullOrEmpty(s));
        }

        /// <summary>
        /// Resolves an Accept-Language header value against a set of
        /// available locale keys, returning the best matching locale.
        /// Handles both exact locale matches (e.g. "fr_FR") and 2-char
        /// language code fallbacks (e.g. "fr" matching "fr_FR").
        /// </summary>
        /// <param name="acceptLanguage">
        /// The raw Accept-Language header value.
        /// </param>
        /// <param name="availableLocales">
        /// The set of available locale keys (e.g. "fr_FR", "de_DE").
        /// </param>
        /// <param name="matchedLocale">
        /// The locale key that was matched, or null if no match was found.
        /// </param>
        /// <returns>
        /// True if a matching locale was found, false otherwise.
        /// </returns>
        public static bool TryResolveLocale(
            string acceptLanguage,
            IEnumerable<string> availableLocales,
            out string matchedLocale)
        {
            matchedLocale = null;

            var localeSet = availableLocales as ICollection<string>
                ?? availableLocales.ToList();

            foreach (var candidate in ParseAcceptLanguage(acceptLanguage))
            {
                // Try exact match.
                var key = localeSet.FirstOrDefault(k =>
                    k.Equals(candidate,
                        StringComparison.InvariantCultureIgnoreCase));

                // Try 2-char language code fallback.
                if (key == null && candidate.Length == 2)
                {
                    key = localeSet.FirstOrDefault(k =>
                        k.StartsWith(candidate,
                            StringComparison.InvariantCultureIgnoreCase));
                }

                if (key != null)
                {
                    matchedLocale = key;
                    return true;
                }
            }

            return false;
        }
    }
}
