/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2025 51 Degrees Mobile Experts Limited, Davidson House,
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

namespace FiftyOne.Pipeline.Core.Data
{
    /// <summary>
    /// Represents an object that filters evidence key names based on some
    /// criteria.
    /// For example, a filter that only included evidence items relating to
    /// HTTP headers might use key.StartsWith("header.")
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/advertize-accepted-evidence.md">Specification</see>
    /// </summary>
    public interface IEvidenceKeyFilter
    {
        /// <summary>
        /// Check if the specified evidence key is included by this filter.
        /// </summary>
        /// <param name="key">
        /// The key to check
        /// </param>
        /// <returns>
        /// True if the key is included and false if not.
        /// </returns>
        bool Include(string key);

        /// <summary>
        /// Get the order of precedence of the specified key
        /// </summary>
        /// <param name="key">
        /// The key to check
        /// </param>
        /// <returns>
        /// The order, where lower values indicate a higher order of 
        /// precedence. 
        /// Null if the key is not recognized.
        /// </returns>
        int? Order(string key);
    }
}
