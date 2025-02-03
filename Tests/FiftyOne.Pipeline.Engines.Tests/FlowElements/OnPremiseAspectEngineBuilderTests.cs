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
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Engines.Tests.FlowElements
{
    [TestClass]
    public class OnPremiseAspectEngineBuilderTests
    {
        private class TestBuilder : OnPremiseAspectEngineBuilderBase<TestBuilder, IOnPremiseAspectEngine>
        {
            public Mock<IOnPremiseAspectEngine> MockEngine { get; private set; }

            public TestBuilder(IDataUpdateService dataUpdateService) : base(dataUpdateService)
            {
            }

            public override TestBuilder SetPerformanceProfile(PerformanceProfiles profile)
            {
                return this;
            }

            protected override IOnPremiseAspectEngine NewEngine(List<string> properties)
            {
                MockEngine = new Mock<IOnPremiseAspectEngine>();
                return MockEngine.Object;
            }

            public IOnPremiseAspectEngine Build()
            {
                return BuildEngine();
            }
        }


        /// <summary>
        /// Check that data files are registered with the update service if:
        /// 1. Auto updates are enabled OR
        /// 2. Update on startup is enabled OR
        /// 3. File watcher is enabled
        /// </summary>
        /// <param name="automaticUpdates"></param>
        /// <param name="updateOnStartup"></param>
        /// <param name="fileWatcher"></param>
        /// <param name="expectRegistration"></param>
        [DataTestMethod]
        [DataRow(false, false, false, false)]
        [DataRow(true, false, false, true)]
        [DataRow(false, true, false, true)]
        [DataRow(true, true, false, true)]
        [DataRow(false, false, true, true)]
        [DataRow(true, false, true, true)]
        [DataRow(false, true, true, true)]
        [DataRow(true, true, true, true)]
        public void OnPremiseAspectEngineBuilder_VerifyUpdateServiceRegistration(bool automaticUpdates, 
            bool updateOnStartup, bool fileWatcher, bool expectRegistration)
        {
            // Create the mock data update service
            var updateService = new Mock<IDataUpdateService>();
            // Create the test data file with the specified options
            var dataFile = new DataFileConfiguration()
            {
                AutomaticUpdatesEnabled = automaticUpdates,
                UpdateOnStartup = updateOnStartup,
                FileSystemWatcherEnabled = fileWatcher
            };

            // Create the engine using the test builder and passing
            // in the test data file.
            var engine = new TestBuilder(updateService.Object)
                .AddDataFile(dataFile)
                .Build();

            if (expectRegistration)
            {
                // Verify the data file was registered with the data update
                // service.
                updateService.Verify(u => u.RegisterDataFile(It.Is<AspectEngineDataFile>(f =>
                    f.AutomaticUpdatesEnabled == automaticUpdates &&
                    f.Configuration.UpdateOnStartup == updateOnStartup)), Times.Once());
            }
            else
            {
                // Verify that the data file was not registered with the 
                // data update service.
                updateService.Verify(u => u.RegisterDataFile(It.IsAny<AspectEngineDataFile>()), Times.Never());
            }
        }


        /// <summary>
        /// Test that exceptions are thrown in various scenarios where
        /// the configuration is not as required.
        /// </summary>
        [DataTestMethod]
        // Updates enabled and license key is supplied so should 
        // be fine.
        [DataRow(true, true, true, true, "ABC", false)]
        // Exception should be thrown if updates are enabled but
        // key is null or blank.
        [DataRow(true, true, true, true, null, true)]
        [DataRow(true, true, true, true, "", true)]
        // If license key is not required then it should work
        // regardless of the other settings.
        [DataRow(true, true, true, false, "ABC", false)]
        [DataRow(true, true, true, false, null, false)]
        [DataRow(true, true, true, false, "", false)]
        // If only file watcher is enabled then it should 
        // work regardless of other settings (as a license 
        // key will not be necessary)
        [DataRow(false, false, true, true, "ABC", false)]
        [DataRow(false, false, true, true, null, false)]
        [DataRow(false, false, true, true, "", false)]
        public void OnPremiseAspectEngineBuilder_VerifyLicenseKeyFailureModes(
            bool automaticUpdates,
            bool updateOnStartup, 
            bool fileWatcher, 
            bool requireLicenseKey,
            string licenseKey,
            bool expectException)
        {
            // Create the mock data update service
            var updateService = new Mock<IDataUpdateService>();
            // Create the test data file with the specified options
            var dataFile = new DataFileConfiguration()
            {
                AutomaticUpdatesEnabled = automaticUpdates,
                UpdateOnStartup = updateOnStartup,
                FileSystemWatcherEnabled = fileWatcher,
                LicenseKeyRequiredForUpdates = requireLicenseKey,
                DataUpdateLicenseKeys = new List<string>() { licenseKey }
            };

            // Create the engine using the supplied test configuration
            Exception thrown = null;
            try
            {
                var engine = new TestBuilder(updateService.Object)
                    .AddDataFile(dataFile)
                    .Build();
            }
            catch(Exception ex)
            {
                thrown = ex;
            }

            if (expectException)
            {
                Assert.IsNotNull(thrown, "Expected an exception to be thrown");
            }
            else
            {
                Assert.IsNull(thrown, "Expected now exceptions to be thrown");
            }
        }


        /// <summary>
        /// Test that exceptions are thrown in various scenarios where
        /// certain features have been requests but a data update service
        /// has not been supplied.
        /// </summary>
        [DataTestMethod]
        // If update service is supplied then there should be 
        // no exception thrown
        [DataRow(true, true, true, true, false)]
        [DataRow(true, false, false, true, false)]
        [DataRow(true, true, false, true, false)]
        [DataRow(true, false, true, true, false)]
        [DataRow(false, false, true, true, false)]
        [DataRow(false, true, true, true, false)]
        // If update service is not supplied then there should be 
        // an exception thrown
        [DataRow(true, true, true, false, true)]
        [DataRow(true, false, false, false, true)]
        [DataRow(true, true, false, false, true)]
        [DataRow(true, false, true, false, true)]
        [DataRow(false, false, true, false, true)]
        [DataRow(false, true, true, false, true)]
        public void OnPremiseAspectEngineBuilder_VerifyUpdateServiceFailureModes(
            bool automaticUpdates,
            bool updateOnStartup,
            bool fileWatcher,
            bool provideUpdateService,
            bool expectException)
        {
            // Create the mock data update service
            var updateService = new Mock<IDataUpdateService>();
            // Create the test data file with the specified options
            var dataFile = new DataFileConfiguration()
            {
                AutomaticUpdatesEnabled = automaticUpdates,
                UpdateOnStartup = updateOnStartup,
                FileSystemWatcherEnabled = fileWatcher
            };

            // Create the engine using the supplied test configuration
            Exception thrown = null;
            try
            {
                var engine = new TestBuilder(provideUpdateService ? updateService.Object : null)
                    .AddDataFile(dataFile)
                    .Build();
            }
            catch (Exception ex)
            {
                thrown = ex;
            }

            if (expectException)
            {
                Assert.IsNotNull(thrown, "Expected an exception to be thrown");
            }
            else
            {
                Assert.IsNull(thrown, "Expected now exceptions to be thrown");
            }
        }
    }

}
