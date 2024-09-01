/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2023 51 Degrees Mobile Experts Limited, Davidson House,
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
using FiftyOne.Pipeline.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace FiftyOne.Pipeline.Web.Tests
{
    [TestClass]
    public class FlowDataProviderTest
    {
        private FlowDataProvider _provider;

        private HttpContext _context;

        private IFlowData _flowData = new Mock<IFlowData>().Object;

        /// <summary>
        /// Set up the test by creating a new flow data provider and its
        /// dependencies.
        /// </summary>
        [TestInitialize]
        public void SetUp() {
        
            IHttpContextAccessor accessor = new HttpContextAccessor();
            _context = new DefaultHttpContext();
            accessor.HttpContext = _context;

            _provider = new FlowDataProvider(accessor);
        }

        /// <summary>
        /// Test that the get flow data method returns the flow data instance
        /// which was added to the HTTP context items using the key in
        /// constants.
        /// </summary>
        [TestMethod]
        public void FlowDataProvider_GetFlowData()
        {
            _context.Items.Add(Constants.HTTPCONTEXT_FLOWDATA, _flowData);

            IFlowData flowData = _provider.GetFlowData();
            Assert.AreEqual(
                _flowData,
                flowData,
                "The provider did not return the flow data contained in the " +
                "HTTP context.");
        }

        /// <summary>
        /// Test that when there is no flow data stored in the HTTP context, an
        /// exception is not thrown, and null returned.
        /// </summary>
        [TestMethod]
        public void FlowDataProvider_NullFlowData()
        {
            IFlowData flowData = _provider.GetFlowData();
            Assert.IsNull(
                flowData,
                "GetFlowData returned an object but expected null.");
        }
    }
}
