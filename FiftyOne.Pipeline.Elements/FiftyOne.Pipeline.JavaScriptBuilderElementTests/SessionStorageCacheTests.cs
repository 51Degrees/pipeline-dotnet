/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2026 51 Degrees Mobile Experts Limited, Davidson House,
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

using FiftyOne.Common.TestHelpers;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.Engines.TestHelpers;
using FiftyOne.Pipeline.JavaScriptBuilder.Data;
using FiftyOne.Pipeline.JavaScriptBuilder.FlowElement;
using FiftyOne.Pipeline.JsonBuilder.Data;
using FiftyOne.Pipeline.JsonBuilder.FlowElement;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace FiftyOne.Pipeline.JavaScript.Tests
{
    /// <summary>
    /// Checks that the JavaScript keeps using its session storage cache
    /// when the user moves to another page in the same tab.
    /// </summary>
    [TestClass]
    public class SessionStorageCacheTests
    {
        private readonly TestLoggerFactory _loggerFactory = new TestLoggerFactory();

        private const string PageHtml = """
            <!DOCTYPE html>
            <html>
              <head>
                <title>Session storage cache test</title>
                <script src="/51dpipeline/js"></script>
                <script>
                  window.fodDone = false;
                  window.fodValue = '';
                  window.addEventListener('load', function () {
                    fod.complete(function (data) {
                      window.fodDone = true;
                      window.fodValue =
                        (data && data.device && data.device.testvalue) || '';
                    });
                  });
                </script>
              </head>
              <body>
                Session storage cache test page
              </body>
            </html>
            """;

        private class TestValueData : ElementDataBase
        {
            public TestValueData(ILogger<ElementDataBase> logger, IPipeline pipeline)
                : base(logger, pipeline)
            {
            }
        }

        /// <summary>
        /// Test element with a JavaScript property. Returns a snippet that
        /// stores a value on the client, or the value itself once it comes
        /// back as evidence.
        /// </summary>
        private class TestValueElement : FlowElementBase<TestValueData, ElementPropertyMetaData>
        {
            private readonly ILoggerFactory _loggerFactory;

            public TestValueElement(ILoggerFactory loggerFactory)
                : base(loggerFactory.CreateLogger<FlowElementBase<TestValueData, ElementPropertyMetaData>>())
            {
                _loggerFactory = loggerFactory;
            }

            public override string ElementDataKey => "device";

            public override IEvidenceKeyFilter EvidenceKeyFilter =>
                new EvidenceKeyFilterWhitelist(new List<string>() {
                    "query.51D_testvalue",
                    "cookie.51D_testvalue",
                });

            public override IList<ElementPropertyMetaData> Properties =>
                new List<ElementPropertyMetaData>()
                {
                    new ElementPropertyMetaData(this, "testvalue", typeof(string), true),
                    new ElementPropertyMetaData(this, "testvaluejavascript", typeof(Core.Data.Types.JavaScript), true),
                };

            protected override void ProcessInternal(IFlowData data)
            {
                var result = new TestValueData(
                    _loggerFactory.CreateLogger<TestValueData>(), data.Pipeline);
                if (TryGetSavedValue(data, out var saved))
                {
                    result["testvalue"] = saved;
                }
                else
                {
                    result["testvaluejavascript"] = new Core.Data.Types.JavaScript(
                        "document.cookie = \"51D_testvalue=\" + \"purple\"");
                }
                data.GetOrAdd(ElementDataKey, p => result);
            }

            private static bool TryGetSavedValue(IFlowData data, out string value)
            {
                foreach (var key in new[] { "query.51D_testvalue", "cookie.51D_testvalue" })
                {
                    if (data.TryGetEvidence(key, out object obj) &&
                        string.IsNullOrEmpty(obj?.ToString()) == false)
                    {
                        value = obj.ToString();
                        return true;
                    }
                }
                value = null;
                return false;
            }

            protected override void ManagedResourcesCleanup() { }
            protected override void UnmanagedResourcesCleanup() { }
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        [Timeout(300_000)]
        public async Task SessionStorageCache_SecondPageIsServedFromCache(bool enableCookies)
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            int jsonPostCount = 0;

            using var pipeline = BuildPipeline(enableCookies, port);

            var builder = WebApplication.CreateBuilder();
            var app = builder.Build();
            app.Use((ctx, next) =>
            {
                ctx.Response.Headers["Cache-Control"] = "no-store";
                return next();
            });
            app.MapGet("/51dpipeline/js", (HttpContext ctx) =>
                Results.Content(
                    BuildContent(pipeline, ctx,
                        d => d.Get<IJavaScriptBuilderElementData>().JavaScript),
                    "text/javascript"));
            app.MapPost("/51dpipeline/json", async (HttpContext ctx) =>
            {
                Interlocked.Increment(ref jsonPostCount);
                var form = await ctx.Request.ReadFormAsync();
                return Results.Content(
                    BuildContent(pipeline, ctx,
                        d => d.Get<IJsonBuilderElementData>().Json, form),
                    "application/json");
            });
            app.MapGet("/{page}", (string page) => Results.Content(PageHtml, "text/html"));
            app.Urls.Add(url);
            await app.StartAsync();

            ChromeDriver driver = null;
            try
            {
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "first page", () => jsonPostCount);
                var valuePage1 = (string)js.ExecuteScript("return window.fodValue");
                var keysPage1 = GetSessionStorageKeys(js);
                var sessionIdPage1 = (string)js.ExecuteScript("return fod.sessionId");
                Assert.AreEqual("purple", valuePage1,
                    "the first page must produce and see the value");
                Assert.AreEqual(1, jsonPostCount,
                    "the first page must call the json endpoint exactly once");

                jsonPostCount = 0;
                driver.Navigate().GoToUrl(url + "page2");
                WaitForFodDone(js, "second page", () => jsonPostCount);
                var valuePage2 = (string)js.ExecuteScript("return window.fodValue");
                var keysPage2 = GetSessionStorageKeys(js);
                var sessionIdPage2 = (string)js.ExecuteScript("return fod.sessionId");

                Assert.AreNotEqual(sessionIdPage1, sessionIdPage2,
                    "the include must be fetched fresh on the second page, " +
                    "not served from the browser cache");
                Assert.IsFalse(
                    keysPage1.Concat(keysPage2).Any(k =>
                        k.Contains(sessionIdPage1) || k.Contains(sessionIdPage2)),
                    "session storage keys must not embed the per-request session id");
                CollectionAssert.AreEqual(keysPage1, keysPage2,
                    "session storage keys must not change between page views: " +
                    $"[{string.Join(", ", keysPage1)}] -> [{string.Join(", ", keysPage2)}]");
                Assert.AreEqual(0, jsonPostCount,
                    "no json refresh call is expected on the second page");
                Assert.AreEqual("purple", valuePage2,
                    "the second page must still see the value produced on the first page");
            }
            finally
            {
                driver?.Quit();
                await app.DisposeAsync();
            }
        }

        [TestMethod]
        [Timeout(300_000)]
        public async Task SessionStorageCache_BadJsonResponseClearsCache()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";

            using var pipeline = BuildPipeline(enableCookies: false, port);

            var builder = WebApplication.CreateBuilder();
            var app = builder.Build();
            app.Use((ctx, next) =>
            {
                ctx.Response.Headers["Cache-Control"] = "no-store";
                return next();
            });
            app.MapGet("/51dpipeline/js", (HttpContext ctx) =>
                Results.Content(
                    BuildContent(pipeline, ctx,
                        d => d.Get<IJavaScriptBuilderElementData>().JavaScript),
                    "text/javascript"));
            app.MapPost("/51dpipeline/json", () =>
                Results.Content("not json", "application/json"));
            app.MapGet("/{page}", (string page) => Results.Content(PageHtml, "text/html"));
            app.Urls.Add(url);
            await app.StartAsync();

            ChromeDriver driver = null;
            try
            {
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "page with a failing refresh", () => 0);

                var leftover = GetSessionStorageKeys(js)
                    .Where(k => k == "fod" ||
                        k.StartsWith("fod_", StringComparison.Ordinal))
                    .ToList();
                Assert.AreEqual(0, leftover.Count,
                    "a failed refresh must clear every cached entry, left: " +
                    $"[{string.Join(", ", leftover)}]");
            }
            finally
            {
                driver?.Quit();
                await app.DisposeAsync();
            }
        }

        private IPipeline BuildPipeline(bool enableCookies, int port)
        {
            return new PipelineBuilder(_loggerFactory)
                .AddFlowElement(new TestValueElement(_loggerFactory))
                .AddFlowElement(new SequenceElementBuilder(_loggerFactory).Build())
                .AddFlowElement(new JsonBuilderElementBuilder(_loggerFactory).Build())
                .AddFlowElement(new JavaScriptBuilderElementBuilder(_loggerFactory)
                    .SetMinify(false)
                    .SetProtocol("http")
                    .SetHost($"localhost:{port}")
                    .SetEndpoint("/51dpipeline/json")
                    .SetEnableCookies(enableCookies)
                    .Build())
                .Build();
        }

        private static string BuildContent(
            IPipeline pipeline,
            HttpContext ctx,
            Func<IFlowData, string> getResult,
            IFormCollection form = null)
        {
            using var flowData = pipeline.CreateFlowData();
            foreach (var q in ctx.Request.Query)
            {
                flowData.AddEvidence("query." + q.Key, q.Value.ToString());
            }
            foreach (var c in ctx.Request.Cookies)
            {
                flowData.AddEvidence("cookie." + c.Key, c.Value);
            }
            if (form != null)
            {
                foreach (var f in form)
                {
                    flowData.AddEvidence("query." + f.Key, f.Value.ToString());
                }
            }
            flowData.Process();
            return getResult(flowData);
        }

        private static ChromeDriver CreateDriver()
        {
            try
            {
                return JavaScriptBuilderElementTestsBase.CreateConfiguredDriver();
            }
            catch (WebDriverException)
            {
                Assert.Inconclusive("Could not create a ChromeDriver, check " +
                    "that the Chromium driver is installed");
                return null;
            }
        }

        private static void WaitForFodDone(
            IJavaScriptExecutor js, string phase, Func<int> postCount)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
            while (true)
            {
                if (true.Equals(js.ExecuteScript("return window.fodDone === true")))
                {
                    return;
                }
                if (DateTime.UtcNow >= deadline)
                {
                    var fodKeys = js.ExecuteScript(
                        "return typeof fod === 'undefined' ? '' : Object.keys(fod).join(',')");
                    var errors = js.ExecuteScript(
                        "return typeof fod === 'undefined' || !fod.errors ? '' : JSON.stringify(fod.errors)");
                    var storage = js.ExecuteScript(
                        "return Object.keys(sessionStorage).join(',')");
                    Assert.Fail(
                        $"Timed out during {phase}. fodKeys=[{fodKeys}], " +
                        $"errors=[{errors}], sessionStorage=[{storage}], " +
                        $"jsonPosts={postCount()}");
                }
                Thread.Sleep(500);
            }
        }

        private static List<string> GetSessionStorageKeys(IJavaScriptExecutor js)
        {
            var raw = (IReadOnlyCollection<object>)js.ExecuteScript(
                "return Object.keys(sessionStorage)");
            return raw.Select(o => (string)o)
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToList();
        }
    }
}
