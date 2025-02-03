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

using FiftyOne.Common.TestHelpers;
using FiftyOne.Common.Wrappers.IO;
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FiftyOne.Data;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Tests.Data
{
    [TestClass]
    public class UrlFormatterTests
    {
        private TestLogger<DataUpdateService> _logger;
        private Mock<IFileWrapper> _fileWrapper;
        private Mock<IDirectoryWrapper> _directoryWarpper;
        private Mock<IFileSystem> _fileSystem;
        private Mock<Func<TimerCallback, object, TimeSpan, Timer>> _timerFactory;
        private Mock<MockHttpMessageHandler> _httpHandler;
        private HttpClient _httpClient;
        private DataUpdateService _dataUpdate;

        /// <summary>
        /// Initialise the test instance.
        /// </summary>
        [TestInitialize]
        public void Init()
        {
            // Create mocks
            _logger = new TestLogger<DataUpdateService>();
            _fileWrapper = new Mock<IFileWrapper>();
            _directoryWarpper = new Mock<IDirectoryWrapper>();
            _fileSystem = new Mock<IFileSystem>();
            _fileSystem.Setup(f => f.File).Returns(_fileWrapper.Object);
            _fileSystem.Setup(f => f.Directory).Returns(_directoryWarpper.Object);
            _timerFactory = new Mock<Func<TimerCallback, object, TimeSpan, Timer>>();

            // Create the HttpClient using the mock handler
            _httpHandler = new Mock<MockHttpMessageHandler>() { CallBase = true };
            _httpClient = new HttpClient(_httpHandler.Object);

            // Configure the mock handler to return an 'OK' status code.
            _httpHandler.Setup(h => h.Send(It.IsAny<HttpRequestMessage>()))
                .Callback((HttpRequestMessage request) =>
                { })
                .Returns(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("<empty />", Encoding.UTF8, "application/xml"),
                });

            // Create the data update service
            _dataUpdate = new DataUpdateService(
                _logger,
                _httpClient,
                _fileSystem.Object,
                _timerFactory.Object);

        }

        /// <summary>
        /// Check that license keys are used when constructing a download URL for
        /// an on premise engine. Also check that only the license key for that
        /// data file is used.
        /// </summary>
        [TestMethod]
        public void FiftyOneUrlFormatter_FormattedUri_MultipleEngines()
        {
            Mock<IFiftyOneAspectEngine> engine1 = new Mock<IFiftyOneAspectEngine>();
            engine1.Setup(e => e.GetDataDownloadType(It.IsAny<string>())).Returns("test");
            var config1 = new DataFileConfiguration()
            {
                AutomaticUpdatesEnabled = true,
                FileSystemWatcherEnabled = false,
                LicenseKeyRequiredForUpdates = true,
                DataUpdateLicenseKeys = new List<string>() { "license1" },
                DataUpdateUrl = "https://download.test.com",
                UrlFormatter = new FiftyOneUrlFormatter()
            };
            var file1 = new FiftyOneDataFile(typeof(IOnPremiseAspectEngine))
            {
                Engine = engine1.Object,
                Configuration = config1,
                DataDownloadType = "test"
            };
            Mock<IFiftyOneAspectEngine> engine2 = new Mock<IFiftyOneAspectEngine>();
            engine2.Setup(e => e.GetDataDownloadType(It.IsAny<string>())).Returns("test");
            var config2 = new DataFileConfiguration()
            {
                AutomaticUpdatesEnabled = true,
                FileSystemWatcherEnabled = false,
                LicenseKeyRequiredForUpdates = true,
                DataUpdateLicenseKeys = new List<string>() { "license2" },
                DataUpdateUrl = "https://download.test.com",
                UrlFormatter = new FiftyOneUrlFormatter()
            };
            var file2 = new FiftyOneDataFile(typeof(IOnPremiseAspectEngine))
            {
                Engine = engine2.Object,
                Configuration = config2,
                DataDownloadType = "test"
            };
            _dataUpdate.RegisterDataFile(file1);
            _dataUpdate.RegisterDataFile(file2);

            var url1 = file1.FormattedUri.AbsoluteUri;
            var url2 = file2.FormattedUri.AbsoluteUri;
            Assert.IsTrue(url1.Contains("license1"));
            Assert.IsFalse(url1.Contains("license2"));
            Assert.IsTrue(url2.Contains("license2"));
            Assert.IsFalse(url2.Contains("license1"));
        }

        /// <summary>
        /// Check that license keys are used when constructing a download URL for
        /// an on premise engine. Also check that only the license key for that
        /// data file is used.
        /// </summary>
        [TestMethod]
        public void FiftyOneUrlFormatter_RequestedUrl_MultipleEngines()
        {
            ConfigureTimerImmediateCallbackOnce();
            Mock<IFiftyOneAspectEngine> engine1 = new Mock<IFiftyOneAspectEngine>();
            engine1.Setup(e => e.GetDataDownloadType(It.IsAny<string>())).Returns("test");
            engine1.SetupGet(e => e.TempDataDirPath).Returns("temp");
            var config1 = new DataFileConfiguration()
            {
                AutomaticUpdatesEnabled = true,
                FileSystemWatcherEnabled = false,
                LicenseKeyRequiredForUpdates = true,
                DataUpdateLicenseKeys = new List<string>() { "license1" },
                DataUpdateUrl = "https://download.test.com",
                CreateTempCopy = true,
                UrlFormatter = new FiftyOneUrlFormatter(),
                DataFilePath = "test/test.hash"
            };
            var file1 = new FiftyOneDataFile(typeof(IOnPremiseAspectEngine))
            {
                Engine = engine1.Object,
                Configuration = config1,
                DataDownloadType = "test"
            };
            Mock<IFiftyOneAspectEngine> engine2 = new Mock<IFiftyOneAspectEngine>();
            engine2.Setup(e => e.GetDataDownloadType(It.IsAny<string>())).Returns("test");
            var config2 = new DataFileConfiguration()
            {
                AutomaticUpdatesEnabled = true,
                FileSystemWatcherEnabled = false,
                LicenseKeyRequiredForUpdates = true,
                DataUpdateLicenseKeys = new List<string>() { "license2" },
                DataUpdateUrl = "https://download.test.com",
                CreateTempCopy = true,
                UrlFormatter = new FiftyOneUrlFormatter()
            };
            var file2 = new FiftyOneDataFile(typeof(IOnPremiseAspectEngine))
            {
                Engine = engine2.Object,
                Configuration = config2,
                DataDownloadType = "test"
            };
            ManualResetEventSlim completeFlag = new ManualResetEventSlim(false);
            _dataUpdate.CheckForUpdateComplete += (object sender, DataUpdateCompleteArgs e) =>
            {
                if (e.DataFile.Equals(file1))
                {
                    completeFlag.Set();
                }
            };
            _dataUpdate.RegisterDataFile(file1);
            _dataUpdate.RegisterDataFile(file2);
            // Wait until processing is complete.
            completeFlag.Wait(10000);

            // Assert
            _httpHandler.Verify(h => h.Send(It.IsAny<HttpRequestMessage>()),
                Times.Once);
            _httpHandler.Verify(h => h.Send(It.Is<HttpRequestMessage>(m =>
                m.RequestUri.Query.Contains("license1"))),
                Times.Once);
            _httpHandler.Verify(h => h.Send(It.Is<HttpRequestMessage>(m =>
                m.RequestUri.Query.Contains("license2"))),
                Times.Never);
        }

        private void ConfigureTimerImmediateCallbackOnce()
        {
            // Configure the timer factory to return a timer that will
            // execute the callback immediately
            int counter = 0;
            _timerFactory.Setup(f => f(
                It.IsAny<TimerCallback>(),
                It.IsAny<object>(),
                It.IsAny<TimeSpan>()))
                .Returns((TimerCallback callback, object state, TimeSpan interval) =>
                {
                    // Only return the timer of the first call.
                    // For subsequent calls (when the engine is registered 
                    // again following a successful update) just return
                    // null to prevent repeated updates.
                    if (counter == 0)
                    {
                        counter++;
                        return new Timer(callback, state, 0, Timeout.Infinite);
                    }
                    else
                    {
                        return null;
                    }
                });
        }
    }
}
