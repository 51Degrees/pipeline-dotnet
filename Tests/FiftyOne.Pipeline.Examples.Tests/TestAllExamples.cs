/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2022 51 Degrees Mobile Experts Limited, Davidson House,
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Ex = Examples;

namespace FiftyOne.Pipeline.Examples.Tests
{
    /// <summary>
    /// This test class ensures that all examples execute successfully.
    /// </summary>
    /// <remarks>
    /// Note that these test do not generally ensure the correctness 
    /// of the example, only that the example will run without 
    /// crashing or throwing any unhandled exceptions.
    /// </remarks>
    [TestClass]
    public class TestAllExamples
    {
        /// <summary>
        /// Test that the caching example runs successfully.
        /// </summary>
        [TestMethod]
        public void TestCachingExample()
        {
            var example = new Ex.ResultCaching.Program();
            example.RunExample();
        }

        /// <summary>
        /// Test that the simple custom flow element example runs successfully.
        /// </summary>
        [TestMethod]
        public void TestSimpleFlowElementExample()
        {
            var example = new Ex.CustomFlowElement.Program();
            example.RunExample();
        }

        /// <summary>
        /// Test that the simple custom flow element example runs successfully.
        /// </summary>
        [TestMethod]
        public void TestOnPremiseElementExample()
        {
            var example = new Ex.OnPremiseEngine.Program();
            example.RunExample();
        }

        /// <summary>
        /// Test that the usage sharing example runs successfully.
        /// </summary>
        [TestMethod]
        public void TestUsageSharingExample()
        {
            var example = new Ex.UsageSharing.Program();
            example.RunExample();
        }
    }
}
