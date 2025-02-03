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
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace FiftyOne.Pipeline.Engines.Tests.Configuration
{
    [TestClass]
    public class DataFileConfigurationBuilderTests
    {
        DataFileConfigurationBuilder _configBuilder = 
            new DataFileConfigurationBuilder();

        private class MyFormatter : IDataUpdateUrlFormatter
        {
            public Uri GetFormattedDataUpdateUri(IAspectEngineDataFile dataFile) { return null; }

            public string GetFormattedDataUpdateUrl(IAspectEngineDataFile dataFile) { return null; }
        }

        /// <summary>
        /// Ensure that setting the license key to null will also 
        /// disable the 'automatic update' and 'update on startup'
        /// flags.
        /// </summary>
        [TestMethod]
        public void LicenseKey_Null()
        {
            // Confirm the default state of auto update and 
            // update on startup.
            var initialState = _configBuilder.Build("test", true);

            Assert.IsTrue(initialState.AutomaticUpdatesEnabled,
                "Auto updates should be enabled by default");
            Assert.IsFalse(initialState.UpdateOnStartup, 
                "Update on startup should be disabled by default");

            // Enable update on startup then set the license key
            // to null before building another configuration
            // object.
            _configBuilder.SetUpdateOnStartup(true);
            _configBuilder.SetDataUpdateLicenseKey(null);
            var result = _configBuilder.Build("test", true);

            // Both features should now be disabled.
            Assert.AreEqual(0, result.DataUpdateLicenseKeys.Count);
            Assert.IsFalse(result.AutomaticUpdatesEnabled,
                "Auto updates should be disabled");
            Assert.IsFalse(result.UpdateOnStartup,
                "Update on startup should be disabled");
        }

        /// <summary>
        /// Check that the configuration methods for setting the 
        /// URL formatter are working as intended.
        /// </summary>
        [TestMethod]
        public void VerifyFormatter()
        {
            // Confirm the default state of auto update and 
            // update on startup.
            var initialState = _configBuilder.Build("test", true);

            Assert.IsNull(initialState.UrlFormatter,
                "Url formatter should be null by default");

            // Enable update on startup then set the license key
            // to null before building another configuration
            // object.
            _configBuilder.SetDataUpdateUrlFormatter(new MyFormatter());
            var result = _configBuilder.Build("test", true);

            Assert.IsInstanceOfType(result.UrlFormatter, typeof(MyFormatter),
                "Url formatter should be of the type specified");

            // Set 'use formatter' to true. This shouldn't change anything.
            _configBuilder.SetDataUpdateUseUrlFormatter(true);
            result = _configBuilder.Build("test", true);

            Assert.IsInstanceOfType(result.UrlFormatter, typeof(MyFormatter),
                "Url formatter should be of the type specified");

            // Finally, set 'use formatter' to false. This should
            // set the formatter to null.
            _configBuilder.SetDataUpdateUseUrlFormatter(false);
            result = _configBuilder.Build("test", true);

            Assert.IsNull(result.UrlFormatter,
                "Url formatter should be null after calling " +
                "SetDataUpdateUseUrlFormatter(false)");
        }
    }
}
