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

using FiftyOne.Pipeline.Web.Framework.Providers;
using System;
using System.Web;
using System.Web.Configuration;

namespace FiftyOne.Pipeline.Web.Framework
{
    /// <summary>
    /// HTTP module which processes each HTTP request through the pipeline, and
    /// adds the results to the BrowserCapabilities.
    /// </summary>
    public class PipelineModule : IHttpModule
    {
        /// <summary>
        /// Initialize this module.
        /// </summary>
        /// <param name="application">
        /// The <see cref="HttpApplication"/> object.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if one of the required arguments is null
        /// </exception>
        public void Init(HttpApplication application)
        {
            if (application == null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            // Replace the browser capabilities provider with one that is 51Degrees
            // enabled if not done so already.
            if (HttpCapabilitiesBase.BrowserCapabilitiesProvider is
                FiftyOne.Pipeline.Web.Framework.Providers.CapabilitiesProvider == false)
            {
                HttpCapabilitiesBase.BrowserCapabilitiesProvider =
                    new FiftyOne.Pipeline.Web.Framework.Providers.CapabilitiesProvider();
            }
                

            // Register for an event to capture javascript requests.
            application.BeginRequest += OnBeginRequestJavascript;
            // Register an event to dispose the flow data once a request has 
            // ended.
            application.EndRequest += OnEndRequest;
        }

        private void OnBeginRequestJavascript(object sender, EventArgs e)
        {
            HttpContext context = ((HttpApplication)sender).Context;

            if (context != null &&
                  WebPipeline.GetInstance().ClientSideEvidenceEnabled)
            {
                if (context.Request.Path.EndsWith("51Degrees.core.js",
                      StringComparison.OrdinalIgnoreCase))
                {
                    FiftyOneJsProvider.GetInstance().ServeJavascript(context);
                    HttpContext.Current.ApplicationInstance.CompleteRequest();
                }
                if (context.Request.Path.EndsWith(Engines.Constants.DEFAULT_JSON_ENDPOINT,
                      StringComparison.OrdinalIgnoreCase))
                {
                    FiftyOneJsProvider.GetInstance().ServeJson(context);
                    HttpContext.Current.ApplicationInstance.CompleteRequest();
                }
            }
        }

        private void OnEndRequest(object sender, EventArgs e)
        {
            HttpContext context = ((HttpApplication)sender).Context;

            if (context != null)
            {
                PipelineCapabilities caps = context.Request.Browser as PipelineCapabilities;
                if (caps != null)
                {
                    caps.FlowData.Dispose();
                }
            }
        }

        /// <summary>
        /// Dispose of this instance
        /// </summary>
        public void Dispose()
        {

        }
    }
}
