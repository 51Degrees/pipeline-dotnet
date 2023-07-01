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

using FiftyOne.Pipeline.Web.Shared.Adapters;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Web.Adapters
{
    /// <summary>
    /// Adapter class that is used to translate requests from common 
    /// services into the appropriate calls for the ASP.NET Core
    /// <see cref="HttpResponse"/> implementation.
    /// </summary>
    class ResponseAdapter : IResponseAdapter
    {
        private HttpResponse _response;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="response"></param>
        public ResponseAdapter(HttpResponse response)
        {
            _response = response;
        }

        /// <inheritdoc/>
        public int StatusCode
        {
            get => _response.StatusCode;
            set => _response.StatusCode = value;
        }

        /// <inheritdoc/>
        public void Clear()
        {
            _response.Clear();
        }

        /// <inheritdoc/>
        public void ClearHeaders()
        {
            _response.Headers.Clear();
        }

        /// <inheritdoc/>
        public void SetHeader(string name, string value)
        {
            _response.Headers.Add(name, value);
        }

        /// <inheritdoc/>
        public void Write(string content)
        {
            _response.WriteAsync(content).Wait();
        }
    }
}
