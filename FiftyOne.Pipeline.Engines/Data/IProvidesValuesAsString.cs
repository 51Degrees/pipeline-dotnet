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

namespace FiftyOne.Pipeline.Engines.Data
{
    /// <summary>
    /// Element data that can hand back a property's value as the string it
    /// was stored as, rather than as the type the property declares.
    /// </summary>
    /// <remarks>
    /// A typed accessor has to answer in its own type, so a stored value
    /// that is not one of that type's values has to become something. A
    /// Bool property storing "Unknown" answers False, and the caller
    /// cannot tell that from a stored "False". Device detection does
    /// exactly this for the properties the 51Degrees JavaScript fills in,
    /// where "Unknown" means the JavaScript has not run, so a request
    /// that nobody has measured reads as one that was measured and found
    /// negative.
    /// <para>
    /// This offers the stored string alongside the typed accessor rather
    /// than instead of it. Callers that want the declared type carry on
    /// unchanged, and a caller that needs to tell "not measured" from
    /// "measured false" can ask for the string and decide for itself.
    /// </para>
    /// <para>
    /// The value is still wrapped in an
    /// <see cref="IAspectPropertyValue{T}"/>, so a property that is
    /// genuinely absent, or that this request is not entitled to, is
    /// reported the same way it would be through any other accessor.
    /// </para>
    /// </remarks>
    public interface IProvidesValuesAsString
    {
        /// <summary>
        /// The value stored for the named property, as a string.
        /// </summary>
        /// <param name="propertyName">
        /// The property to read, in the same form the typed accessors
        /// take.
        /// </param>
        /// <returns>
        /// The stored value as a string, or a value with no value where
        /// the property could not be read on this request.
        /// </returns>
        IAspectPropertyValue<string> GetValueAsString(string propertyName);
    }
}
