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

using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Web.Adapters;
using FiftyOne.Pipeline.Web.Shared;
using FiftyOne.Pipeline.Web.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System;

namespace FiftyOne.Pipeline.Web.Services
{
    /// <summary>
    /// Service that provides the 51Degrees JavaScript when requested
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/web-integration.md#client-side-features">Specification</see>
    /// </summary>
    public class FiftyOneJSService : IFiftyOneJSService
    {
        /// <summary>
        /// The ClientsidePropertyService determines the JavaScript
        /// content to be returned when it is requested.
        /// </summary>
        protected IClientsidePropertyService ClientsidePropertyService { get; private set; }
        /// <summary>
        /// The configuration options for this service.
        /// </summary>
        protected IOptions<PipelineWebIntegrationOptions> Options { get; private set; }
        /// <summary>
        /// Provides a mechanism to access the current 
        /// <see cref="IFlowData"/> instance.
        /// </summary>
        protected IFlowDataProvider FlowDataProvider { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="clientsidePropertyService">
        /// The <see cref="IClientsidePropertyService"/> to use when 
        /// JavaScript needs to be returned to the user.
        /// </param>
        /// <param name="options">
        /// The configuration options for this service
        /// </param>
        /// <param name="flowDataProvider">
        /// The provider to use when accessing the <see cref="IFlowData"/>
        /// instance.
        /// </param>
        public FiftyOneJSService(
            IClientsidePropertyService clientsidePropertyService,
            IOptions<PipelineWebIntegrationOptions> options,
            IFlowDataProvider flowDataProvider)
        {
            ClientsidePropertyService = clientsidePropertyService;
            Options = options;
            FlowDataProvider = flowDataProvider;
        }

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
        /// <exception cref="ArgumentNullException">
        /// Thrown if a required parameter is null.
        /// </exception>
        public bool ServeJS(HttpContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            bool result = false;
            if (context.Request.Path.Value.EndsWith("51Degrees.core.js", 
                StringComparison.OrdinalIgnoreCase))
            {
                ServeCoreJS(context);
                result = true;
            }
            return result;
        }

        private void ServeCoreJS(HttpContext context)
        {
            if (Options.Value.ClientSideEvidenceEnabled)
            {
                ClientsidePropertyService.ServeJavascript(
                    new ContextAdapter(context), 
                    FlowDataProvider.GetFlowData());
            }
        }

        /// <summary>
        /// Check if the 51Degrees JSON is being requested and
        /// write it to the response if it is
        /// </summary>
        /// <param name="context">
        /// The HttpContext
        /// </param>
        /// <returns>
        /// True if JSON was written to the response, false otherwise.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if a required parameter is null.
        /// </exception>
        public bool ServeJson(HttpContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            bool result = false;
            if (context.Request.Path.Value.EndsWith(Engines.Constants.DEFAULT_JSON_ENDPOINT,
                StringComparison.OrdinalIgnoreCase))
            {
                ServeCoreJson(context);
                result = true;
            }
            return result;
        }

        private void ServeCoreJson(HttpContext context)
        {
            if (Options.Value.ClientSideEvidenceEnabled)
            {
                ClientsidePropertyService.ServeJson(
                    new ContextAdapter(context),
                    FlowDataProvider.GetFlowData());
            }
        }
    }
}
