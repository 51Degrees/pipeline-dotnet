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

using FiftyOne.Pipeline.Math;
using FiftyOne.Pipeline.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Example_Website.Controllers
{
    public class HomeController : Controller
    {
        private IFlowDataProvider _flow;

        public HomeController(IFlowDataProvider flow)
        {
            _flow = flow;
        }

        public IActionResult Index()
        {
            var math = _flow.GetFlowData()?.Get(MathElement.math);
            if (math != null)
            {
                ViewData["Message"] = $"{math.Operation} = {math.Result}";
            }
            else
            {
                ViewData["Message"] = "No 'FlowData' found. This is usually " +
                    "because the 51Degrees middleware component has not run, " +
                    "possibly due to being after some other middleware " +
                    "component that has blocked further execution " +
                    "(For example, requiring HTTPS).";
            }
            return View();
        }
    }
}