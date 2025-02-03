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

using FiftyOne.Pipeline.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FiftyOne.Pipeline.Core.Tests.Services
{
    [TestClass]
    public class FiftyOneServiceProviderTests
    {
        /// <summary>
        /// Empty interface used for testing FiftyOneServiceProvider
        /// </summary>
        public interface ITestService
        {
        }

        /// <summary>
        /// Empty class used for testing FiftyOneServiceProvider
        /// </summary>
        public class TestService : ITestService
        {
        }

        /// <summary>
        /// Class used for testing FiftyOneServiceProvider.
        /// Takes a TestService as a constructor parameter.
        /// </summary>
        public class HighLevelService
        {
            public TestService TestService { get; set; }

            public HighLevelService(TestService testService)            
            {
                TestService = testService;
            }
        }

        /// <summary>
        /// Class used for testing FiftyOneServiceProvider.
        /// Takes an ITestService as a constructor parameter.
        /// </summary>
        public class HighLevelServiceUsingInterface
        {
            public ITestService TestService { get; set; }

            public HighLevelServiceUsingInterface(ITestService testService)
            {
                TestService = testService;
            }
        }

        /// <summary>
        /// Verify that the provider will return the expected service instance.
        /// </summary>
        [TestMethod]
        public void TestProvider()
        {
            var service = new TestService();
            FiftyOneServiceProvider provider = new FiftyOneServiceProvider();
            provider.AddService(service);

            // Get the service from the provider
            var suppliedService = provider.GetService(typeof(TestService));

            // Verify the instance is the same as the one added to the provider. 
            Assert.AreEqual(service.GetHashCode(), suppliedService.GetHashCode());
        }

        /// <summary>
        /// Verify that the provider will return the expected service instance if an
        /// interface is requested.
        /// </summary>
        [TestMethod]
        public void TestProvider_Interface()
        {
            var service = new TestService();
            FiftyOneServiceProvider provider = new FiftyOneServiceProvider();
            provider.AddService(service);

            var suppliedService = provider.GetService(typeof(ITestService));

            Assert.AreEqual(service.GetHashCode(), suppliedService.GetHashCode());
        }

        /// <summary>
        /// Verify that the provider will return the expected service instance if a service
        /// is requested that does not exist in the provider. 
        /// Instead, the provider contains a service which matches the type required by the 
        /// constructor of the requested type.
        /// </summary>
        [TestMethod]
        public void TestProvider_HighLevelService()
        {
            var service = new TestService();
            FiftyOneServiceProvider provider = new FiftyOneServiceProvider();
            provider.AddService(service);

            var suppliedService = provider.GetService(typeof(HighLevelService)) as HighLevelService;
            Assert.AreEqual(service.GetHashCode(), suppliedService.TestService.GetHashCode());

            // Instances stored in FiftyOneServiceProvider are singletons.
            // In contrast, instances created by it are transient.
            // Verify this by requesting another instance of the same service and comparing
            // their hash codes.
            var suppliedService2 = provider.GetService(typeof(HighLevelService)) as HighLevelService;
            Assert.AreNotEqual(suppliedService2.GetHashCode(), suppliedService.GetHashCode());
        }

        /// <summary>
        /// Verify that the provider will return the expected service instance if a service
        /// is requested that does not exist in the provider.
        /// Instead, the provider contains a service that implements an interface which 
        /// matches the interface required by the constructor of the requested type.
        /// </summary>
        [TestMethod]
        public void TestProvider_HighLevelServiceUsingInterface()
        {
            var service = new TestService();
            FiftyOneServiceProvider provider = new FiftyOneServiceProvider();
            provider.AddService(service);

            var suppliedService = provider.GetService(typeof(HighLevelServiceUsingInterface)) 
                as HighLevelServiceUsingInterface;
            Assert.AreEqual(service.GetHashCode(), suppliedService.TestService.GetHashCode());

            // Instances stored in FiftyOneServiceProvider are singletons.
            // In contrast, instances created by it are transient.
            // Verify this by requesting another instance of the same service and comparing
            // their hash codes.
            var suppliedService2 = provider.GetService(typeof(HighLevelServiceUsingInterface)) 
                as HighLevelServiceUsingInterface;
            Assert.AreNotEqual(suppliedService2.GetHashCode(), suppliedService.GetHashCode());
        }
    }
}
