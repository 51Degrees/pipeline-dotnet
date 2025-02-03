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

using System.Collections.Generic;

namespace FiftyOne.Pipeline.Web.Shared.Data
{
    /// <summary>
    /// Generic interface allowing access to a session instance. This is used
    /// by engines to access both ASP.NET Core and ASP.NET Framework sessions
    /// without needing to reference either.
    /// </summary>
    public interface IAspSession
    {
        /// <summary>
        /// The keys available in the session.
        /// </summary>
        IEnumerable<string> Keys { get; }

        /// <summary>
        /// Add a string value to the session.
        /// </summary>
        /// <param name="key">
        /// The name of what is being stored. This must be unique.
        /// </param>
        /// <param name="value">
        /// Value to store.
        /// </param>
        void SetString(string key, string value);

        /// <summary>
        /// Get the value of the specified key from the session.
        /// </summary>
        /// <param name="key">
        /// The key to get the value for.
        /// </param>
        /// <returns>
        /// The value in the session which is stored using the key provided.
        /// </returns>
        string GetString(string key);
    }
}
