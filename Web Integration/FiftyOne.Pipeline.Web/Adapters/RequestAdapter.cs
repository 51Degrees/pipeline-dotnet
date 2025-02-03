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

using FiftyOne.Pipeline.Web.Shared.Adapters;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;

namespace FiftyOne.Pipeline.Web.Adapters
{
    /// <summary>
    /// Adapter class that is used to translate requests from common 
    /// services into the appropriate calls for the ASP.NET Core
    /// <see cref="HttpRequest"/> implementation.
    /// </summary>
    class RequestAdapter : IRequestAdapter
    {
        private HttpRequest _request;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="request"></param>
        public RequestAdapter(HttpRequest request)
        {
            _request = request;
        }

        /// <summary>
        /// Get the value for the specified header.
        /// If there is no header with the given name, an empty 
        /// string is returned.
        /// </summary>
        /// <param name="name">
        /// The name of the header to get the value for
        /// </param>
        /// <returns></returns>
        public string GetHeaderValue(string name)
        {
            var result = string.Empty;
            var query = _request.Headers.Keys.Where(k => 
                k.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (query.Any())
            {
                result = _request.Headers[query.First()];
            }
            return result;
        }
    }
}
