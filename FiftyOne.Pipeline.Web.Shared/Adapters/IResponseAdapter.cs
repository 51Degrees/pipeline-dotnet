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

namespace FiftyOne.Pipeline.Web.Shared.Adapters
{
    /// <summary>
    /// Interface for an adapter class that can be used to translate 
    /// requests from common services into the appropriate calls
    /// for a specific HttpResponse implementation.
    /// </summary>
    public interface IResponseAdapter
    {
        /// <summary>
        /// Get/set the HTTP status code 
        /// </summary>
        int StatusCode { get; set; }

        /// <summary>
        /// Write the specified content to the response
        /// </summary>
        /// <param name="content"></param>
        void Write(string content);

        /// <summary>
        /// Add the specified response header name and value.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        void SetHeader(string name, string value);

        /// <summary>
        /// Clear any existing HTTP headers
        /// </summary>
        void ClearHeaders();

        /// <summary>
        /// Clear any existing content
        /// </summary>
        void Clear();
    }
}
