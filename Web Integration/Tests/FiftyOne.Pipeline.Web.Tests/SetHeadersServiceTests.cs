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

using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Core.TypedMap;
using FiftyOne.Pipeline.Engines.FiftyOne.Data;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Web.Tests
{
    /// <summary>
    /// Tests for <see cref="SetHeaderService"/>. The service runs on every
    /// request from the middleware, so it must tolerate a flow data with no
    /// set-headers entry: the element's processing can fail before its data
    /// is added (for example when the cloud service could not be reached,
    /// with the pipeline configured to suppress process exceptions).
    /// </summary>
    [TestClass]
    public class SetHeadersServiceTests
    {
        private Mock<IPipeline> _pipeline;
        private Mock<IFlowData> _flowData;
        private Mock<ISetHeadersElement> _element;
        private HttpContext _context;

        [TestInitialize]
        public void SetUp()
        {
            _pipeline = new Mock<IPipeline>();
            _flowData = new Mock<IFlowData>();
            _flowData.SetupGet(f => f.Pipeline).Returns(_pipeline.Object);
            _element = new Mock<ISetHeadersElement>();
            _element.SetupGet(e => e.ElementDataKey)
                .Returns(SetHeadersElement.DEFAULT_ELEMENT_DATA_KEY);
            _context = new DefaultHttpContext();
        }

        /// <summary>
        /// The happy path: headers from the set-headers data are appended
        /// to the response.
        /// </summary>
        [TestMethod]
        public void SetHeaders_AppendsHeaders()
        {
            var setHeadersData = new Mock<ISetHeadersData>();
            setHeadersData.SetupGet(d => d.ResponseHeaderDictionary)
                .Returns(new Dictionary<string, string>()
                {
                    { "Accept-CH", "Sec-CH-UA-Model" }
                });
            SetupPipeline(setHeadersData.Object);

            SetHeaderService.SetHeaders(_context, _flowData.Object);

            Assert.AreEqual(
                "Sec-CH-UA-Model",
                (string)_context.Response.Headers["Accept-CH"]);
        }

        /// <summary>
        /// A pipeline without a set headers element results in no headers
        /// and no exception.
        /// </summary>
        [TestMethod]
        public void SetHeaders_NoElement_DoesNotThrow()
        {
            _pipeline.Setup(p => p.GetElement<ISetHeadersElement>())
                .Returns((ISetHeadersElement)null);

            SetHeaderService.SetHeaders(_context, _flowData.Object);

            Assert.AreEqual(0, _context.Response.Headers.Count);
        }

        /// <summary>
        /// A flow data without set-headers data (the element's processing
        /// failed before the data was added) results in no headers and no
        /// exception.
        /// </summary>
        [TestMethod]
        public void SetHeaders_NoElementData_DoesNotThrow()
        {
            SetupPipeline(null);

            SetHeaderService.SetHeaders(_context, _flowData.Object);

            Assert.AreEqual(0, _context.Response.Headers.Count);
        }

        /// <summary>
        /// Set-headers data whose dictionary was never populated (the
        /// element's processing failed after the data was added) results in
        /// no headers and no exception.
        /// </summary>
        [TestMethod]
        public void SetHeaders_NullDictionary_DoesNotThrow()
        {
            var setHeadersData = new Mock<ISetHeadersData>();
            setHeadersData.SetupGet(d => d.ResponseHeaderDictionary)
                .Returns((IReadOnlyDictionary<string, string>)null);
            SetupPipeline(setHeadersData.Object);

            SetHeaderService.SetHeaders(_context, _flowData.Object);

            Assert.AreEqual(0, _context.Response.Headers.Count);
        }

        /// <summary>
        /// Configure the pipeline to contain the set headers element and the
        /// flow data to hold the given data for it (no data when null).
        /// </summary>
        private void SetupPipeline(ISetHeadersData setHeadersData)
        {
            _pipeline.Setup(p => p.GetElement<ISetHeadersElement>())
                .Returns(_element.Object);
            var outData = setHeadersData;
            _flowData.Setup(f => f.TryGetValue(
                    It.IsAny<ITypedKey<ISetHeadersData>>(), out outData))
                .Returns(setHeadersData != null);
        }
    }
}
