/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2020 51 Degrees Mobile Experts Limited, 5 Charlotte Close,
 * Caversham, Reading, Berkshire, United Kingdom RG4 7BY.
 *
 * This Original Work is licensed under the European Union Public Licence (EUPL) 
 * v.1.2 and is subject to its terms as set out below.
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
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

/// @example ExampleFrameworkWebSite/Controllers/HomeController.cs
/// ASP.NET core example
/// 
/// This example shows how to:
/// 
/// 1. Set up configuration options to add elements to the 51Degrees Pipeline.
/// ```{json}
/// {
///     "PipelineOptions": {
///         "Elements": [
///             {
///                 "BuilderName": "math"
///             },
///             {
///                 "BuilderName": "JavaScriptBundlerElement"
///             }
///         ],
///         "ClientSideEnabled":  true
///     }
/// }
/// ```
/// 
/// 2. Configure client-side code to be run by enabling it in the Web.config.
/// ```{xml}
/// <configuration>
///     <system.webServer>
///         <modules runAllManagedModulesForAllRequests = "true" >
///         ...
/// ```
/// 
/// 4. Use the results contained in the `Request.Browser` instance to display
/// something on a page view.
/// ```{cs}
/// public class HomeController : Controller
/// {
///     ...
///     public IActionResult Index()
///     {
///         ViewData["Message"] =
///             Request.Browser["math.operation"] + " = " + Request.Browser["math.result"];
///         return View();
///     }
///     ...
/// ```
/// 
/// ## Controller
/// 
namespace Example_Framework_Website.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            ViewData["Message"] =
                Request.Browser["math.operation"] +
                " = " +
                Request.Browser["math.result"];
            return View();
        }
    }
}