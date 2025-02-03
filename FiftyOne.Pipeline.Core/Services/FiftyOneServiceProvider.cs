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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FiftyOne.Pipeline.Core.Services
{
    /// <summary>
    /// Basic implementation of <see cref="IServiceProvider"/>.
    /// An instance contains a list of services which can be added
    /// to.
    /// </summary>
    public class FiftyOneServiceProvider : IServiceProvider
    {
        private readonly IList<object> _services = new List<object>();

        /// <summary>
        /// Add a service instance to the provider. This builds the
        /// collection used to return services from the GetService
        /// method.
        /// </summary>
        /// <param name="service">
        /// Service instance to add.
        /// </param>
        public void AddService(object service)
        {
            _services.Add(service);
        }


        /// <summary>
        /// Get the service from the service collection if it exists.
        /// If it does not exist, but we can create a new instance, then do so.
        /// If we cannot create a new instance, return null.
        /// Note that if more than one instance implementing the same service
        /// is added to the services, the first will be returned.
        /// </summary>
        /// <param name="serviceType">
        /// The service type to be returned.
        /// </param>
        /// <returns>
        /// Service or null.
        /// </returns>
        public object GetService(Type serviceType)
        {
            if (serviceType != null)
            {
                foreach (var service in _services)
                {
                    if (serviceType.IsAssignableFrom(service.GetType()))
                    {
                        return service;
                    }
                }
                // We don't have the requested service.
                // Do we have the services to create a new instance?
                return CreateService(serviceType);
            }
            return null;
        }

        private object CreateService(Type serviceType)
        {
            object result = null;
            var constructor = GetConstructor(serviceType);
            if(constructor != null)
            {
                result = CreateInstance(constructor);
            }
            return result;
        }

        /// <summary>
        /// Get the services required for the constructor, and call it with them.
        /// </summary>
        /// <param name="constructor">
        /// The constructor to call.
        /// </param>
        /// <returns>
        /// Instance returned by the constructor.
        /// </returns>
        private object CreateInstance(ConstructorInfo constructor)
        {
            ParameterInfo[] parameters = constructor.GetParameters();
            object[] services = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                services[i] = GetService(parameters[i].ParameterType);
            }
            return Activator.CreateInstance(constructor.DeclaringType, services);
        }


        /// <summary>
        /// Get the best constructor for the list of constructors. Best meaning
        /// the constructor with the most parameters which can be fulfilled.
        /// </summary>
        /// <param name="requiredType">
        /// The type we want a constructor for
        /// </param>
        /// <returns>
        /// Best constructor or null if none have parameters that can be
        /// fulfilled.
        /// </returns>
        private ConstructorInfo GetConstructor(Type requiredType)
        {
            var constructors = requiredType.GetConstructors()
                .OrderByDescending(c => c.GetParameters().Length)
                .Where(c => c.GetParameters().All(p => GetService(p.ParameterType) != null));

            return constructors.FirstOrDefault();
        }
    }
}
