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

using FiftyOne.Pipeline.Web.Services;
using Microsoft.AspNetCore.Mvc;
using SimpleClientSideElement.Data;

namespace Examples.ClientSideEvidence.MVC.Controllers
{
    //! [usage]
    public class HomeController : Controller
    {
        private IFlowDataProvider _flowDataProvider;

        public HomeController(IFlowDataProvider flowDataProvider)
        {
            // The flow data provider is injected here, so it can be used to
            // get a pre-processed flow data.
            _flowDataProvider = flowDataProvider;
        }

        public IActionResult Index()
        {
            // Get the flow data for this request.
            var flowData = _flowDataProvider.GetFlowData();

            // Set the message to be displayed in the view. The JavaScript is
            // added in the view.
            ViewData["message"] = $"With a date of birth of" +
                $" {flowData.GetEvidence()["cookie.date-of-birth"]}," +
                $" your star sign is" +
                $" {flowData.Get<IStarSignData>().StarSign}.";
            return View();
        }
    }
    //! [usage]
}
