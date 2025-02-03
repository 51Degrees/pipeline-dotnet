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

using System;

namespace FiftyOne.Pipeline.Engines.Data
{
    /// <summary>
    /// Represents an class that can build a complete update URL from
    /// a supplied <see cref="IAspectEngineDataFile"/> instance.
    /// </summary>
    public interface IDataUpdateUrlFormatter
    {
        /// <summary>
        /// Get the URL to call to request an updated version of the 
        /// supplied data file.
        /// </summary>
        /// <param name="dataFile">
        /// The data file to build an update URL for.
        /// </param>
        /// <returns>
        /// The URL to call in order to check for and download an update.
        /// </returns>
        [Obsolete("Use the GetFormattedDataUpdateUri method instead." +
            "This method may be removed in future versions.")]
#pragma warning disable CA1055 // Uri return values should not be strings
        string GetFormattedDataUpdateUrl(IAspectEngineDataFile dataFile);
#pragma warning restore CA1055 // Uri return values should not be strings

        /// <summary>
        /// Get the URL to call to request an updated version of the 
        /// supplied data file.
        /// </summary>
        /// <param name="dataFile">
        /// The data file to build an update URL for.
        /// </param>
        /// <returns>
        /// The URL to call in order to check for and download an update.
        /// </returns>
        Uri GetFormattedDataUpdateUri(IAspectEngineDataFile dataFile);
    }
}
