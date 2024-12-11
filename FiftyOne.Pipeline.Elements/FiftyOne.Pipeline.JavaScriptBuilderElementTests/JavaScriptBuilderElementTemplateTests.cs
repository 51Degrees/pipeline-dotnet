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
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Core.TypedMap;
using FiftyOne.Pipeline.JavaScriptBuilder.Data;
using FiftyOne.Pipeline.JavaScriptBuilder.FlowElement;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using System.Text;

namespace FiftyOne.Pipeline.JavaScript.Tests
{
    [TestClass]
    public class JavaScriptBuilderElementTemplateTests: JavaScriptBuilderElementTestsBase
    {
        private WebApplication webApp { get; set; }
        private CancellationTokenSource webAppKillSwitch { get; set; }
        private JObject jsonData { get; set; } = new();
        private string fullJS { get; set; } = "";

        [TestInitialize]
        public override async Task Init()
        {
            await base.Init();

            var builder = WebApplication.CreateBuilder();
            webApp = builder.Build();

            webApp.MapGet("/51dpipeline/js", () => Results.Content(fullJS, "text/javascript"));
            webApp.MapGet("/{*rest}", (string rest) =>
            Results.Content(
            """
            <!DOCTYPE html>
            <html>
              <head>
                <title>Test Page</title>
              </head>
              <script src="/51dpipeline/js">
              </script>
              <body>
                Did the script already finish??
              </body>
            </html>
            """
            , "text/html"));
            webApp.MapPost("/51dpipeline/json", () => jsonData);
            webApp.MapPost("/51dpipeline/completed", () => Results.Empty);

            webApp.Use((ctx, next) =>
            {
                ctx.Response.Headers["Access-Control-Allow-Origin"] = "*";
                ctx.Response.Headers["Cache-Control"] = "no-store";
                return next();
            });

            webApp.Urls.Add(ClientServerUrl);

            webAppKillSwitch = new CancellationTokenSource();
            await webApp.StartAsync(webAppKillSwitch.Token);

            Driver.Manage().Cookies.DeleteAllCookies();
            // Navigate to the client site.
            Driver.Navigate().GoToUrl(ClientServerUrl);
        }

        private static string BuildXHRJS(string dstUrl, string method = "POST", string postData = "dummy", bool acceptJson = false)
        {
            StringBuilder s = new();
            s.Append("xhr = new XMLHttpRequest();");
            s.Append($"xhr.open('{method}', '{dstUrl}', true);");
            if (acceptJson)
            {
                s.Append("xhr.setRequestHeader(\"Accept\", \"application/json;charset=UTF-8\");");
            }
            s.Append($"xhr.send('{postData}');");
            return s.ToString();
        }


        [TestMethod]
        [Timeout(300_000)]
        public void JavaScriptBuilderTemplate_VerifyInterception()
        {
            bool testDone = false;
            OnRequestSent = e => {
                var p = e.RequestPostData;
                testDone = true;
            };
            IJavaScriptExecutor js = Driver;
            var q = js.ExecuteScript(BuildXHRJS($"{ClientServerUrl}51dpipeline/json"));
            while (!testDone)
            {
                DumpNewLogs();
                Thread.Sleep(1000);
            }
            DumpNewLogs();
            Assert.IsTrue(testDone);
        }

        public static IEnumerable<object[]> GetValidateSetCookieBlockData()
        {
            string[][] snippetFragments =
            {
                ["javascriptalpha", "", "\"51D_alpha=\" + 42", "", "51D_alpha=42"], // plain plus
                ["javascriptbeta", "if(true){", "\"51D_beta=\" + 29", "}", "51D_beta=29"], // plus in block
                ["gammajavascript", "", "\"51D_gamma=\" + \"zoomies\"", ", a=7", "51D_gamma=zoomies"], // plus in comma
                ["kappajavascript", "let q = x => x(3); q(e => ", "\"51D_kappa=\" + e", ")", "51D_kappa=3"], // plus in lambda
                ["omegajavascript", "let q = x => x(7); q(e => ", "`51D_omega=${e}`", ")", "51D_omega=7"], // template in lambda
            };
            bool[] useCookies = { true, false };
            return snippetFragments.SelectMany(snippet => useCookies.Select(use =>
            {
                var args = new object[snippet.Length + 1];
                for(int i = 0; i < snippet.Length; ++i)
                {
                    args[i] = snippet[i];
                }
                args[snippet.Length] = use;
                return args;
            }));
        }

        [TestMethod]
        [DynamicData(nameof(GetValidateSetCookieBlockData), DynamicDataSourceType.Method)]
        [Timeout(600_000)]
        public void JavaScriptBuilderTemplate_ValidateSetCookieBlockCall(
            string propName,
            string prefixJunk,
            string propSetExpr,
            string suffixJunk,
            string expectedData,
            bool enableCookies)
        {
            // ----- ----- PHASE 0. Preparation ----- -----

            // Build code fragments

            Func<string, string, string, string, string, (JObject json, string expectedData, string testCode)> BuildNewDataObject = (propName, prefixJunk, propSetExpr, suffixJunk, expectedData)
                =>
            {
                string propSetFlag = $"{propName}_snippet_set_block_called_51d";
                string propCode =
                    $"""
                console.log("starting snippet ({propName})");
                {prefixJunk}document.cookie = {propSetExpr}{suffixJunk}
                ;
                window.{propSetFlag} = true;
                console.log("leaving snippet ({propName})");
                """;
                string testCode = $"return window.{propSetFlag};";

                return (
                    new JObject {
                        { "device", new JObject { { propName, propCode } } },
                        { "javascriptProperties", new JArray { $"device.{propName}" } },
                    },
                    expectedData,
                    testCode
                );
            };

            // Reusable blocks

            Func<JObject, string> BuildScript = (jsonData) =>
            {
                int firstColon = ClientServerUrl.IndexOf(":");
                var javaScriptBuilderElement =
                    new JavaScriptBuilderElementBuilder(LoggerFactory)
                    .SetMinify(false)
                    .SetProtocol(ClientServerUrl.Substring(0, firstColon))
                    .SetHost(ClientServerUrl.Substring(firstColon + 3))
                    .SetEnableCookies(enableCookies)
                    .Build();
                var flowData = new Mock<IFlowData>();
                Configure(flowData, jsonData);

                IJavaScriptBuilderElementData result = null;
                flowData.Setup(d => d.GetOrAdd(
                    It.IsAny<ITypedKey<IJavaScriptBuilderElementData>>(),
                    It.IsAny<Func<IPipeline, IJavaScriptBuilderElementData>>()))
                    .Returns<ITypedKey<IJavaScriptBuilderElementData>, Func<IPipeline, IJavaScriptBuilderElementData>>((k, f) =>
                    {
                        result = f(flowData.Object.Pipeline);
                        return result;
                    });

                javaScriptBuilderElement.Process(flowData.Object);
                return result.JavaScript;
            };

            // Completion testers

            string postData = null;
            bool completed = false;
            OnRequestSent = e =>
            {
                if (e.RequestUrl.EndsWith("json"))
                {
                    postData = e.RequestPostData;
                    Console.WriteLine($"[POST DATA] > {postData}");
                }
                if (e.RequestUrl.EndsWith("completed"))
                {
                    completed = true;
                }
            };
            Action<string> WaitAndValidatePostData = (expectedData) =>
            {
                while (postData == null)
                {
                    DumpNewLogs();
                    Thread.Sleep(1000);
                }
                Assert.IsTrue(postData.Contains(expectedData), $"[{postData}] does not contain [{expectedData}]");
            };
            Action WaitAndValidateScriptCompletion = () => {
                while (!completed)
                {
                    DumpNewLogs();
                    Thread.Sleep(1000);
                }
            };
            IJavaScriptExecutor js = Driver;
            Action<string> WaitAndValidateSnippetCalled = (testCode) =>
            {
                while (true)
                {
                    var objResult = js.ExecuteScript(testCode);
                    if (objResult is bool newResult)
                    {
                        Assert.IsTrue(newResult);
                        break;
                    }
                    DumpNewLogs();
                    Thread.Sleep(1000);
                };
            };

            string additionalCode
                = "; window.onload = (e) => { fod.complete(function (data) { "
                + BuildXHRJS($"{ClientServerUrl}51dpipeline/completed", acceptJson: true)
                + " }); };";

            // ----- ----- PHASE 1. Run script to set values ----- -----

            Console.WriteLine(
                """
                ===== ===== ===== ===== =====
                           PHASE 1
                
                """);

            var mainData = BuildNewDataObject(propName, prefixJunk, propSetExpr, suffixJunk, expectedData);
            jsonData = mainData.json;
            fullJS = BuildScript(jsonData) + additionalCode;

            // Reload page to trigger script execution
            Driver.Manage().Cookies.DeleteAllCookies();
            Driver.Navigate().GoToUrl(ClientServerUrl);

            WaitAndValidatePostData(expectedData);
            WaitAndValidateScriptCompletion();
            WaitAndValidateSnippetCalled(mainData.testCode);

            // ----- ----- PHASE 2. Replace snippet code, check post data still contains saved value ----- -----

            Console.WriteLine(
                """
                ===== ===== ===== ===== =====
                           PHASE 2
                
                """);

            var secondaryData = BuildNewDataObject("prop2javascript", "", "\"51D_prop2=\"+769", "", "51D_prop2=769");
            jsonData = secondaryData.json;
            postData = null;
            completed = false;

            string lastFullJS = fullJS;
            fullJS = BuildScript(jsonData) + additionalCode;
            Assert.AreNotEqual(lastFullJS, fullJS);

            string extraCookie = "51D_saved_ext=829";
            if (enableCookies)
            {
                Driver.Manage().Cookies.AddCookie(new Cookie(extraCookie.Split("=")[0], extraCookie.Split("=")[1]));
            }

            // Reload page to trigger script execution
            Driver.Navigate().GoToUrl(ClientServerUrl);

            WaitAndValidatePostData(secondaryData.expectedData);
            WaitAndValidatePostData(mainData.expectedData);
            if (enableCookies)
            {
                WaitAndValidatePostData(extraCookie);
            }
            WaitAndValidateScriptCompletion();
            WaitAndValidateSnippetCalled(secondaryData.testCode);
            Assert.IsNull(js.ExecuteScript(mainData.testCode));

            // Check for no cookies
            // TODO: Make more strict after template PR merge
            int cookieCount = Driver.Manage().Cookies.AllCookies.Count;
            if (!enableCookies && cookieCount > 0)
            {
                Assert.Fail($"Detected cookies after script completion: {cookieCount}");
            }
        }

        private void DumpNewLogs()
        {
            bool hrPrinted = false;
            var entries = Driver.Manage().Logs.GetLog(LogType.Browser);
            foreach (var entry in entries)
            {
                if (!hrPrinted)
                {
                    Console.WriteLine("----- ----- -----");
                    hrPrinted = true;
                }
                Console.WriteLine($"[BROWSER] > ${entry}");
            }
        }

        [TestCleanup]
        public override async Task Cleanup()
        {
            await base.Cleanup();
            webAppKillSwitch.Cancel();
            await webApp.StopAsync();
            jsonData.RemoveAll();
        }
    }
}
