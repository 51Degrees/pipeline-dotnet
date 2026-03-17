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

namespace FiftyOne.Pipeline.Translation.Data
{
    /// <summary>
    /// Defines the behavior of the translation engine when a translation is
    /// missing for a value.
    /// </summary>
    public enum MissingTranslationBehavior
    {
        /// <summary>
        /// Use the original value if there is no translation for it.
        /// This is the default behavior.
        /// </summary>
        Original,

        /// <summary>
        /// Use an empty string if there is no translation for the value.
        /// </summary>
        EmptyString,

        /// <summary>
        /// Add a flow error if there is no translation for the value. The error
        /// contains the reason there was no translation.
        /// </summary>
        FlowError
    }
}
