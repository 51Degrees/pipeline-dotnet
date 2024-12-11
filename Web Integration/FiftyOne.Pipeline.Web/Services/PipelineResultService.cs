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

using System;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.AspNetCore.Http;

namespace FiftyOne.Pipeline.Web.Services
{
    /// <summary>
    /// The PipelineResultService passes the current request 
    /// to the <see cref="IPipeline"/> and makes the results 
    /// accessible through the <see cref="HttpContext"/>.
    /// </summary>
    public class PipelineResultService : IPipelineResultService
    {
        private IPipeline _pipeline;

        private IWebRequestEvidenceService _evidenceService;
        
        /// <summary>
        /// Construct a new instance of the PipelineResultService.
        /// </summary>
        /// <param name="evidenceService">
        /// Service used to retrieve evidence from a web request
        /// </param>
        /// <param name="pipeline">
        /// Pipeline used to process the evidence
        /// </param>
        public PipelineResultService(
            IWebRequestEvidenceService evidenceService,
            IPipeline pipeline)
        {
            _evidenceService = evidenceService;
            _pipeline = pipeline;
        }

        /// <summary>
        /// Take evidence from the given <see cref="HttpContext"/> and
        /// pass it into the action <see cref="IPipeline"/>.
        /// Add the result to the <see cref="HttpContext.Items"/>
        /// collection for downstream components to use.
        /// </summary>
        /// <param name="context">
        /// The current <see cref="HttpContext"/>
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if one of the required arguments is null
        /// </exception>
        public void Process(HttpContext context)
        {
            if (context == null) { throw new ArgumentNullException(nameof(context)); }

            // Remove the existing flow data if there is one.
            // This will be from a previous request and the evidence may have 
            // changed so we need to update it.
            if (context.Items.ContainsKey(Constants.HTTPCONTEXT_FLOWDATA))
            {
                context.Items.Remove(Constants.HTTPCONTEXT_FLOWDATA);
            }

            // Create the flowData
            var flowData = _pipeline.CreateFlowData();
            context.Response.RegisterForDispose(flowData);

            // Store the FlowData in the HttpContext
            context.Items.Add(Constants.HTTPCONTEXT_FLOWDATA, flowData);

            try
            {
                // Extract the required pieces of evidence from the request
                _evidenceService.AddEvidenceFromRequest(flowData, context.Request);

                // Start processing the data
                flowData.Process();
            }
            catch (Exception ex) 
            when (ex is PipelineTemporarilyUnavailableException
            || (ex is AggregateException
            && ex.InnerException is PipelineTemporarilyUnavailableException))
            {
                // nop -- just suppress thrown error
            }
        }
    }
}
