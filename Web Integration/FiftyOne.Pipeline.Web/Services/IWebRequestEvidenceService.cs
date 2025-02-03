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

using FiftyOne.Pipeline.Core.Data;
using Microsoft.AspNetCore.Http;

namespace FiftyOne.Pipeline.Web.Services
{
    /// <summary>
    /// Interface for <see cref="WebRequestEvidenceService"/>
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/web-integration.md#populating-evidence">Specification</see>
    /// </summary>
    public interface IWebRequestEvidenceService
    {
        /// <summary>
        /// Use the specified <see cref="HttpRequest"/> to populated the 
        /// <see cref="IFlowData"/> with evidence.
        /// </summary>
        /// <param name="flowData">
        /// The <see cref="IFlowData"/> to populate.
        /// </param>
        /// <param name="httpRequest">
        /// The <see cref="HttpRequest"/> to pull values from.
        /// </param>
        void AddEvidenceFromRequest(IFlowData flowData, HttpRequest httpRequest);
    }
}
