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

using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.FiftyOne.Data;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Configuration
{
    /// <summary>
    /// Builder class for 51Degrees specific data file configuration
    /// instances.
    /// </summary>
    public class FiftyOneDataFileConfigurationBuilder : 
        DataFileConfigurationBuilderBase<FiftyOneDataFileConfigurationBuilder,
            FiftyOneDataFileConfiguration>
    {
        /// <summary>
        /// Constructor
        /// Specify configuration options for 51Degrees data files.
        /// </summary>
        public FiftyOneDataFileConfigurationBuilder()
        {
            // Default to using the 51Degrees URL formatter
            SetDataUpdateUrlFormatter(new FiftyOneUrlFormatter());
            // Set the flag to let the update service know that a license key is required 
            // when requesting data updates.
            SetLicenseKeyRequired(true);
        }

    }
}
