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

using FiftyOne.Pipeline.Core.Exceptions;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.CloudRequestEngine
{
    /// <summary>
    /// Contains details about exceptions that occur when making 
    /// requests to the cloud service.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/exception-handling.md#cloud-request-exception">Specification</see> 
    /// </summary>
    public class CloudRequestException : PipelineException
    {
        private Dictionary<string, string> _responseHeaders;

        /// <summary>
        /// The HTTP status code from the response.
        /// </summary>
        public int HttpStatusCode { get; private set; }

        /// <summary>
        /// All HTTP headers that were present in the response.
        /// </summary>
        public IReadOnlyDictionary<string, string> ResponseHeaders => _responseHeaders;

        /// <summary>
        /// Constructor
        /// </summary>
        public CloudRequestException()
        {
            _responseHeaders = new Dictionary<string, string>();
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message"></param>
        public CloudRequestException(string message) : base(message)
        {
            _responseHeaders = new Dictionary<string, string>();
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public CloudRequestException(string message, Exception innerException) : base(message, innerException)
        {
            _responseHeaders = new Dictionary<string, string>();
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message"></param>
        /// <param name="httpStatusCode"></param>
        /// <param name="responseHeaders"></param>
        public CloudRequestException(string message, 
            int httpStatusCode, 
            Dictionary<string, string> responseHeaders) : base(message)
        {
            HttpStatusCode = httpStatusCode;
            _responseHeaders = responseHeaders;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message"></param>
        /// <param name="httpStatusCode"></param>
        /// <param name="responseHeaders"></param>
        /// <param name="innerException"></param>
        public CloudRequestException(string message,
            int httpStatusCode,
            Dictionary<string, string> responseHeaders,
            Exception innerException) : base(message, innerException)
        {
            HttpStatusCode = httpStatusCode;
            _responseHeaders = responseHeaders;
        }
    }
}
