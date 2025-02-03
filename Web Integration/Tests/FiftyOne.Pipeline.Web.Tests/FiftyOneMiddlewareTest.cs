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

using FiftyOne.Pipeline.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Web.Tests
{
    [TestClass]
    public class FiftyOneMiddlewareTest
    {
        private FiftyOneMiddleware _middleware;

        private Mock<IPipelineResultService> _resultsService;

        private Mock<IFiftyOneJSService> _jsService;

        private Mock<IFlowDataProvider> _flowDataProvider;

        private Mock<ISetHeadersService> _setHeadersService;

        private bool _movedNext;

        /// <summary>
        /// Set up the test by creating a new middleware instance and its
        /// dependencies, where the next delegate flags that is has been called
        /// then returns null.
        /// </summary>
        [TestInitialize]
        public void SetUp()
        {
            _movedNext = false;
            _resultsService = new Mock<IPipelineResultService>();
            _jsService = new Mock<IFiftyOneJSService>();
            _flowDataProvider = new Mock<IFlowDataProvider>();
            _setHeadersService = new Mock<ISetHeadersService>();              
            _middleware = new FiftyOneMiddleware(
                delegate (HttpContext context)
                {
                    _movedNext = true;
                    return Task.FromResult<Object>(null);
                },
                _resultsService.Object,
                _jsService.Object,
                _flowDataProvider.Object,
                _setHeadersService.Object);
        }

        /// <summary>
        /// Test that when the request is not for JavaScript, the process
        /// method is called, and then the next delegate is called.
        /// </summary>
        [TestMethod]
        public void FiftyOneMiddleware_Invoke()
        {
            HttpContext context = new DefaultHttpContext();
            _jsService.Setup(s => s.ServeJS(It.IsAny<HttpContext>()))
                .Returns(false);

            _middleware.Invoke(context).Wait();

            _resultsService.Verify(
                s => s.Process(context),
                Times.Once,
                "The results were not processed.");

            _setHeadersService.Verify(
                s => s.SetHeaders(context),
                Times.Once,
                "The response headers were not set as expected");

            Assert.IsTrue(_movedNext, "The next middleware was not called.");
        }

        /// <summary>
        /// Test that when the request is for JavaScript, the process method
        /// is called, but the next delegate is not called as the JavaScript
        /// service has dealt with the request.
        /// </summary>
        [TestMethod]
        public void FiftyOneMiddleware_InvokeJs()
        {
            HttpContext context = new DefaultHttpContext();
            _jsService.Setup(s => s.ServeJS(It.IsAny<HttpContext>()))
                .Returns(true);

            _middleware.Invoke(context).Wait();

            _resultsService.Verify(
                s => s.Process(It.IsAny<HttpContext>()), 
                Times.Once,
                "The results were not processed.");

            _setHeadersService.Verify(
                s => s.SetHeaders(context),
                Times.Once,
                "The response headers were not set as expected");

            Assert.IsFalse(
                _movedNext,
                "The next middleware should not have been called.");
        }
    }
}
