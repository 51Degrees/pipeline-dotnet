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
using System.Web;
using System.Web.Configuration;

[assembly: PreApplicationStartMethod(typeof(FiftyOne.Pipeline.Web.Framework.PreApplicationStart), "Start")]

namespace FiftyOne.Pipeline.Web.Framework
{

    /// <summary>
    /// Class used by ASP.NET v4 to activate 51Degrees Pipeline removing
    /// the need to activate from the web.config file.
    /// </summary>
    public static class PreApplicationStart
    {
        /// <summary>
        /// Flag to indicate if the method has already been called.
        /// </summary>
        private static bool _initialised = false;

        /// <summary>
        /// Method called with the worker process starts to activate the
        /// capabilities of the DLL without requiring web.config entries.
        /// </summary>
        public static void Start()
        {
            if (_initialised == false)
            {
                // Replace the browser capabilities provider with one that is 51Degrees
                // enabled if not done so already and detection is enabled.
                if (HttpCapabilitiesBase.BrowserCapabilitiesProvider is
                    FiftyOne.Pipeline.Web.Framework.Providers.CapabilitiesProvider == false)
                {
                    HttpCapabilitiesBase.BrowserCapabilitiesProvider =
                        new FiftyOne.Pipeline.Web.Framework.Providers.CapabilitiesProvider();
                }

                // Include the pipeline module if the Microsoft.Web.Infrastructure assembly is
                // available. This is needed to perform background actions such as automatic
                // updates.
                try
                {
                    RegisterModule();
                }
#pragma warning disable CA1031 // Do not catch general exception types
                // Regardless of the exception that occurs here,
                // we don't want to prevent the server from starting up.
                catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    // Don't prevent the server from starting up.
                }
                _initialised = true;
            }
        }

        /// <summary>
        /// Registers the HttpModule.
        /// </summary>
        /// <remarks>
        /// The functionality of the modules is controlled
        /// via the 51Degrees pipeline configuration file.
        /// </remarks>
        private static void RegisterModule()
        {
            Microsoft.Web.Infrastructure.DynamicModuleHelper.DynamicModuleUtility.RegisterModule(
                typeof(FiftyOne.Pipeline.Web.Framework.PipelineModule));
        }
    }
}
