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

using FiftyOne.Pipeline.Web.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FiftyOne.Pipeline.Web
{
    /// <summary>
    /// View component that injects some JavaScript into the page in
    /// order to determine more details about the device.
    /// e.g. using screen size to narrow down iPhone model as it is not
    /// possible to work this out with just the user agent string.
    /// </summary>
    public class FiftyOneJSViewComponent : ViewComponent
    {
        /// <summary>
        /// Configuration options.
        /// </summary>
        protected IOptions<PipelineWebIntegrationOptions> Options { get; private set; }

        /// <summary>
        /// Creates a new <see cref="FiftyOneJSViewComponent"/>.
        /// </summary>
        /// <param name="options">Configuration</param>
        public FiftyOneJSViewComponent(IOptions<PipelineWebIntegrationOptions> options)
        {
            Options = options;
        }

        /// <summary>
        /// Called by MVC to render the component.
        /// </summary>
        /// <returns>The rendered component</returns>
        public IViewComponentResult Invoke()
        {
            return View(Options);
        }
    }
}
