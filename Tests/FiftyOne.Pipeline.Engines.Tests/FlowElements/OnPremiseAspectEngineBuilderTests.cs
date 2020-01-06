/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2019 51 Degrees Mobile Experts Limited, 5 Charlotte Close,
 * Caversham, Reading, Berkshire, United Kingdom RG4 7BY.
 *
 * This Original Work is licensed under the European Union Public Licence (EUPL) 
 * v.1.2 and is subject to its terms as set out below.
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
using FiftyOne.Pipeline.Engines.TestHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;

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
    }

}
