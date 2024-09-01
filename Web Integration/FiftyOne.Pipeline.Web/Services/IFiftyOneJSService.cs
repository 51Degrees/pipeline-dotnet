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

using Microsoft.AspNetCore.Http;

namespace FiftyOne.Pipeline.Web.Services
{
    /// <summary>
    /// Interface for <see cref="FiftyOneJSService"/>.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/web-integration.md#client-side-features">Specification</see>
    /// </summary>
    public interface IFiftyOneJSService
    {
        /// <summary>
        /// Check if the 51Degrees JavaScript is being requested and
        /// write it to the response if it is
        /// </summary>
        /// <param name="context">
        /// The HttpContext
        /// </param>
        /// <returns>
        /// True if JavaScript was written to the response, false otherwise.
        /// </returns>
        bool ServeJS(HttpContext context);

        /// <summary>
        /// Check if the current request is for the Pipeline JSON data or not.
        /// Write it to the response if it is
        /// </summary>
        /// <param name="context">
        /// The HttpContext
        /// </param>
        /// <returns>
        /// True if JSON was written to the response, false otherwise.
        /// </returns>
        bool ServeJson(HttpContext context);
    }
}
