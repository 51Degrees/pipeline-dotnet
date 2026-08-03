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
using FiftyOne.Pipeline.Engines.Data;
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

        private class BrowserCapabilityData : ElementDataBase
        {
            public BrowserCapabilityData(ILogger<ElementDataBase> logger, IPipeline pipeline)
                : base(logger, pipeline)
            {
            }
        }

        /// <summary>
        /// Supplies the two properties the JavaScript builder reads to decide
        /// which transport the template is rendered with. Device detection
        /// provides them in production; without an element that does, the
        /// builder falls back to XHR and no promises, which is not what any
        /// current browser gets.
        /// </summary>
        private class BrowserCapabilityElement : FlowElementBase<BrowserCapabilityData, ElementPropertyMetaData>
        {
            private readonly ILoggerFactory _loggerFactory;

            public BrowserCapabilityElement(ILoggerFactory loggerFactory)
                : base(loggerFactory.CreateLogger<FlowElementBase<BrowserCapabilityData, ElementPropertyMetaData>>())
            {
                _loggerFactory = loggerFactory;
            }

            public override string ElementDataKey => "browser";

            public override IEvidenceKeyFilter EvidenceKeyFilter =>
                new EvidenceKeyFilterWhitelist(new List<string>());

            public override IList<ElementPropertyMetaData> Properties =>
                new List<ElementPropertyMetaData>()
                {
                    new ElementPropertyMetaData(this, "Promise", typeof(string), true),
                    new ElementPropertyMetaData(this, "Fetch", typeof(bool), true),
                };

            protected override void ProcessInternal(IFlowData data)
            {
                var result = new BrowserCapabilityData(
                    _loggerFactory.CreateLogger<BrowserCapabilityData>(), data.Pipeline);
                result["Promise"] = new AspectPropertyValue<string>("Full");
                result["Fetch"] = new AspectPropertyValue<bool>(true);
                data.GetOrAdd(ElementDataKey, p => result);
            }

            protected override void ManagedResourcesCleanup() { }
            protected override void UnmanagedResourcesCleanup() { }
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

        private const string MergePageHtml = """
            <!DOCTYPE html>
            <html>
              <head>
                <title>Cache merge test</title>
                <script src="/51dpipeline/js"></script>
                <script>
                  window.fodDone = false;
                  window.fodValue = '';
                  window.fodFreshBody = false;
                  window.addEventListener('load', function () {
                    fod.complete(function (data) {
                      window.fodDone = true;
                      window.fodValue =
                        (data && data.device && data.device.testvalue) || '';
                      window.fodFreshBody =
                        !!(data && data.device &&
                           typeof data.device.testvaluejavascript === 'string');
                    });
                  });
                </script>
              </head>
              <body>
                Cache merge test page
              </body>
            </html>
            """;

        private const string ParamsFirstPageHtml = """
            <!DOCTYPE html>
            <html>
              <head>
                <title>Parameters test, first page</title>
                <script src="/51dpipeline/js?mark=first&amp;campaign=spring"></script>
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
                Parameters test, first page
              </body>
            </html>
            """;

        private const string ParamsSecondPageHtml = """
            <!DOCTYPE html>
            <html>
              <head>
                <title>Parameters test, second page</title>
                <script src="/51dpipeline/js?mark=second"></script>
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
                Parameters test, second page
              </body>
            </html>
            """;

        private const string MultiCallbackPageHtml = """
            <!DOCTYPE html>
            <html>
              <head>
                <title>Multiple complete callbacks test</title>
                <script src="/51dpipeline/js"></script>
                <script>
                  window.firstValue = '';
                  window.secondValue = '';
                  window.addEventListener('load', function () {
                    // The page registers its own callback first, a later
                    // consumer registers another one. Both must fire.
                    fod.complete(function (data) {
                      window.firstValue =
                        (data && data.device && data.device.testvalue) || '';
                    });
                    fod.complete(function (data) {
                      window.secondValue =
                        (data && data.device && data.device.testvalue) || '';
                    });
                  });
                </script>
              </head>
              <body>
                Multiple complete callbacks test page
              </body>
            </html>
            """;

        private const string DelayedPageHtml = """
            <!DOCTYPE html>
            <html>
              <head>
                <title>Delayed evidence test</title>
                <script src="/51dpipeline/js"></script>
                <script>
                  window.fodError = '';
                  window.addEventListener('error', function (e) {
                    window.fodError = e.message || 'unknown error';
                  });
                  window.addEventListener('load', function () {
                    fod.complete(function (data) {}, 'loc');
                  });
                </script>
              </head>
              <body>
                Delayed evidence test page
              </body>
            </html>
            """;

        private const string OnChangePageHtml = """
            <!DOCTYPE html>
            <html>
              <head>
                <title>onChange test</title>
                <script src="/51dpipeline/js"></script>
                <script>
                  window.changeCount = 0;
                  window.changeValue = '';
                  window.addEventListener('load', function () {
                    // Registered the way page code has to register it: after
                    // the include has run and built the object.
                    fod.onChange(function (data) {
                      window.changeCount = window.changeCount + 1;
                      window.changeValue =
                        (data && data.device && data.device.testvalue) || '';
                    });
                  });
                </script>
              </head>
              <body>
                onChange test page
              </body>
            </html>
            """;

        private const string PayloadPageHtml = """
            <!DOCTYPE html>
            <html>
              <head>
                <title>Payload shape test</title>
                <script src="/51dpipeline/js"></script>
                <script>
                  window.fodDone = false;
                  window.fodValue = '';
                  window.fodErrorCount = -1;
                  window.fodJsProperties = '';
                  window.addEventListener('load', function () {
                    fod.complete(function (data) {
                      window.fodValue =
                        (data && data.device && data.device.testvalue) || '';
                      window.fodErrorCount =
                        (data && data.errors && data.errors.length) || 0;
                      window.fodJsProperties =
                        (data && data.javascriptProperties || []).join(',');
                      window.fodDone = true;
                    });
                  });
                </script>
              </head>
              <body>
                Payload shape test page
              </body>
            </html>
            """;

        /// <summary>
        /// Test element with a second JavaScript property named the way the
        /// template special cases it. Its snippet records that it ran and
        /// deliberately saves no profile ids, which is the case the template
        /// backs out of.
        /// </summary>
        private class TestHardwareProfileElement : FlowElementBase<TestValueData, ElementPropertyMetaData>
        {
            private readonly ILoggerFactory _loggerFactory;

            public TestHardwareProfileElement(ILoggerFactory loggerFactory)
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
                    new ElementPropertyMetaData(this, "javascripthardwareprofile", typeof(Core.Data.Types.JavaScript), true),
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
                // Records the run and saves nothing, so the template's
                // empty-profile back-out is what decides whether it is ever
                // attempted again.
                result["javascripthardwareprofile"] = new Core.Data.Types.JavaScript(
                    "window.hardwareProfileRuns = (window.hardwareProfileRuns || 0) + 1;");
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

        private class TestDelayedData : ElementDataBase
        {
            public TestDelayedData(ILogger<ElementDataBase> logger, IPipeline pipeline)
                : base(logger, pipeline)
            {
            }
        }

        /// <summary>
        /// Test element with two delayed JavaScript properties: one referenced
        /// by an evidenceproperties entry at the top level of the aspect and
        /// one by an entry inside a nested object.
        /// </summary>
        private class TestDelayedElement : FlowElementBase<TestDelayedData, ElementPropertyMetaData>
        {
            private readonly ILoggerFactory _loggerFactory;

            public TestDelayedElement(ILoggerFactory loggerFactory)
                : base(loggerFactory.CreateLogger<FlowElementBase<TestDelayedData, ElementPropertyMetaData>>())
            {
                _loggerFactory = loggerFactory;
            }

            public override string ElementDataKey => "loc";

            public override IEvidenceKeyFilter EvidenceKeyFilter =>
                new EvidenceKeyFilterWhitelist(new List<string>() {
                    "query.51D_a",
                    "query.51D_b",
                });

            public override IList<ElementPropertyMetaData> Properties =>
                new List<ElementPropertyMetaData>()
                {
                    new ElementPropertyMetaData(this, "ajs", typeof(Core.Data.Types.JavaScript), true),
                    new ElementPropertyMetaData(this, "bjs", typeof(Core.Data.Types.JavaScript), true),
                };

            protected override void ProcessInternal(IFlowData data)
            {
                var result = new TestDelayedData(
                    _loggerFactory.CreateLogger<TestDelayedData>(), data.Pipeline);
                // The top level entry must come before the nested object so
                // the recursion walks both.
                result["aevidenceproperties"] = new List<string> { "loc.ajs" };
                result["nested"] = new Dictionary<string, object>
                {
                    ["bevidenceproperties"] = new List<string> { "loc.bjs" },
                };
                result["ajs"] = new Core.Data.Types.JavaScript(
                    "document.cookie = \"51D_a=\" + \"one\"");
                result["bjs"] = new Core.Data.Types.JavaScript(
                    "document.cookie = \"51D_b=\" + \"two\"");
                result["ajsdelayexecution"] = true;
                result["bjsdelayexecution"] = true;
                data.GetOrAdd(ElementDataKey, p => result);
            }

            protected override void ManagedResourcesCleanup() { }
            protected override void UnmanagedResourcesCleanup() { }
        }

        [DataTestMethod]
        [DataRow(true, true)]
        [DataRow(false, true)]
        // The XHR and no-promises render is what a browser without the Promise
        // and Fetch properties gets, so it keeps one pass through the suite's
        // broadest case.
        [DataRow(true, false)]
        [DataRow(false, false)]
        [Timeout(300_000)]
        public async Task SessionStorageCache_SecondPageIsServedFromCache(
            bool enableCookies, bool modernBrowser)
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            int jsonPostCount = 0;

            using var pipeline = BuildPipeline(enableCookies, port, modernBrowser);

            string servedJavaScript = null;

            var app = BuildTestApp(pipeline, url, async (ctx) =>
            {
                Interlocked.Increment(ref jsonPostCount);
                var form = await ctx.Request.ReadFormAsync();
                return Results.Content(
                    BuildContent(pipeline, ctx,
                        d => d.Get<IJsonBuilderElementData>().Json, form),
                    "application/json");
            }, _ => PageHtml,
            javaScript =>
            {
                servedJavaScript = javaScript;
                return javaScript;
            });
            await app.StartAsync();

            ChromeDriver driver = null;
            try
            {
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "first page", () => jsonPostCount);

                // The transport is chosen from the Promise and Fetch
                // properties. If nothing supplies them the builder quietly
                // falls back to XHR, so the mode under test has to be pinned
                // or this dimension tests nothing.
                Assert.AreEqual(modernBrowser,
                    servedJavaScript.Contains("fetch(", StringComparison.Ordinal),
                    "the template must be rendered with the transport this " +
                    "case is for");

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

        /// <summary>
        /// Pins the clearCache iteration: a failed refresh must remove every
        /// cached entry, not every other one. The session id key shape is
        /// pinned by SessionStorageCache_SecondPageIsServedFromCache.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task SessionStorageCache_BadJsonResponseClearsCache()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            int jsonPostCount = 0;

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
            {
                Interlocked.Increment(ref jsonPostCount);
                return Results.Content("not json", "application/json");
            });
            app.MapGet("/{page}", (string page) => Results.Content(PageHtml, "text/html"));
            app.Urls.Add(url);

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "page with a failing refresh", () => jsonPostCount);

                Assert.AreEqual(1, jsonPostCount,
                    "the refresh must actually have been attempted, otherwise the " +
                    "clear-on-error path was never exercised");
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
                try
                {
                    driver?.Quit();
                }
                catch (WebDriverException)
                {
                    // A dead session must not mask the real failure or stop the
                    // web application being disposed.
                }
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// Both evidence snippets behind a delayed property must run when the
        /// page asks for the aspect, whether their evidenceproperties entry
        /// sits at the top level or inside a nested object.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task SessionStorageCache_DelayedEvidenceReachesTheEndpoint()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            string postBody = null;

            using var pipeline = BuildDelayedPipeline(port);

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
                var form = await ctx.Request.ReadFormAsync();
                postBody = string.Join("&",
                    form.Select(f => $"{f.Key}={f.Value}"));
                return Results.Content(
                    BuildContent(pipeline, ctx,
                        d => d.Get<IJsonBuilderElementData>().Json, form),
                    "application/json");
            });
            app.MapGet("/{page}", (string page) => Results.Content(DelayedPageHtml, "text/html"));
            app.Urls.Add(url);

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");

                var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
                while (postBody == null)
                {
                    if (DateTime.UtcNow >= deadline)
                    {
                        var pageError = js.ExecuteScript("return window.fodError || ''");
                        Assert.Fail(
                            "Timed out waiting for the delayed evidence refresh. " +
                            $"pageError=[{pageError}]");
                    }
                    Thread.Sleep(500);
                }

                StringAssert.Contains(postBody, "51D_a=one",
                    "evidence from the top level entry must reach the endpoint");
                StringAssert.Contains(postBody, "51D_b=two",
                    "evidence from the nested entry must reach the endpoint");
            }
            finally
            {
                try
                {
                    driver?.Quit();
                }
                catch (WebDriverException)
                {
                    // A dead session must not mask the real failure or stop the
                    // web application being disposed.
                }
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// A complete callback must not replace one registered earlier:
        /// every callback registered before the data is ready fires when it
        /// arrives, and one registered afterwards fires immediately.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task Complete_DoesNotReplaceEarlierCallbacks()
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
            app.MapPost("/51dpipeline/json", async (HttpContext ctx) =>
            {
                var form = await ctx.Request.ReadFormAsync();
                return Results.Content(
                    BuildContent(pipeline, ctx,
                        d => d.Get<IJsonBuilderElementData>().Json, form),
                    "application/json");
            });
            app.MapGet("/{page}", (string page) => Results.Content(MultiCallbackPageHtml, "text/html"));
            app.Urls.Add(url);

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");

                var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
                while (true)
                {
                    var done = js.ExecuteScript(
                        "return window.firstValue !== '' && window.secondValue !== ''");
                    if (true.Equals(done))
                    {
                        break;
                    }
                    if (DateTime.UtcNow >= deadline)
                    {
                        var first = js.ExecuteScript("return window.firstValue");
                        var second = js.ExecuteScript("return window.secondValue");
                        Assert.Fail(
                            "Timed out waiting for both callbacks. " +
                            $"first=[{first}], second=[{second}]");
                    }
                    Thread.Sleep(500);
                }

                Assert.AreEqual("purple",
                    (string)js.ExecuteScript("return window.firstValue"),
                    "the callback registered first must still fire");
                Assert.AreEqual("purple",
                    (string)js.ExecuteScript("return window.secondValue"),
                    "the callback registered second must fire as well");

                var lateValue = (string)js.ExecuteScript(
                    "var v = ''; fod.complete(function (d) { " +
                    "v = (d && d.device && d.device.testvalue) || ''; }); return v;");
                Assert.AreEqual("purple", lateValue,
                    "a callback registered after completion must fire immediately");
            }
            finally
            {
                try
                {
                    driver?.Quit();
                }
                catch (WebDriverException)
                {
                    // A dead session must not mask the real failure or stop the
                    // web application being disposed.
                }
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// An error status from the refresh endpoint must not be cached: the
        /// next page view retries instead of replaying the error body.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task SessionStorageCache_ErrorStatusIsNotCached()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            var failing = true;
            int healthyPosts = 0;

            using var pipeline = BuildPipeline(enableCookies: false, port);

            var app = BuildTestApp(pipeline, url, async (ctx) =>
            {
                if (failing)
                {
                    return Results.Content("{\"errors\":[\"boom\"]}",
                        "application/json", null, 500);
                }
                Interlocked.Increment(ref healthyPosts);
                var form = await ctx.Request.ReadFormAsync();
                return Results.Content(
                    BuildContent(pipeline, ctx,
                        d => d.Get<IJsonBuilderElementData>().Json, form),
                    "application/json");
            }, _ => PageHtml);

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "page with a failing endpoint", () => healthyPosts);
                CollectionAssert.DoesNotContain(GetSessionStorageKeys(js), "fod",
                    "an error response must not be cached");

                failing = false;
                driver.Navigate().GoToUrl(url + "page2");
                WaitForFodDone(js, "page after the endpoint recovered", () => healthyPosts);

                Assert.AreEqual(1, healthyPosts,
                    "the second page must retry the refresh");
                Assert.AreEqual("purple",
                    (string)js.ExecuteScript("return window.fodValue"),
                    "the second page must get the real value");
            }
            finally
            {
                try
                {
                    driver?.Quit();
                }
                catch (WebDriverException)
                {
                    // A dead session must not mask the real failure or stop the
                    // web application being disposed.
                }
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// A property flag left behind by a refresh that never returned must
        /// not count on the next page view: without the response next to it
        /// the snippet runs again.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task SessionStorageCache_FlagWithoutResponseSelfHeals()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            var hang = true;
            int posts = 0;

            using var pipeline = BuildPipeline(enableCookies: false, port);

            var app = BuildTestApp(pipeline, url, async (ctx) =>
            {
                if (hang)
                {
                    try
                    {
                        await Task.Delay(Timeout.Infinite, ctx.RequestAborted);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    return Results.StatusCode(500);
                }
                Interlocked.Increment(ref posts);
                var form = await ctx.Request.ReadFormAsync();
                return Results.Content(
                    BuildContent(pipeline, ctx,
                        d => d.Get<IJsonBuilderElementData>().Json, form),
                    "application/json");
            }, _ => PageHtml);

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                // The refresh hangs, so completion never fires: wait for the
                // flag write instead.
                WaitForStorageKey(js, "fod_property_");
                CollectionAssert.DoesNotContain(GetSessionStorageKeys(js), "fod",
                    "no response was received, so nothing must be cached");

                hang = false;
                driver.Navigate().GoToUrl(url + "page2");
                WaitForFodDone(js, "page after the hung refresh", () => posts);

                Assert.AreEqual(1, posts,
                    "the snippet must run again when the flag has no response");
                Assert.AreEqual("purple",
                    (string)js.ExecuteScript("return window.fodValue"),
                    "the second page must get the real value");
            }
            finally
            {
                try
                {
                    driver?.Quit();
                }
                catch (WebDriverException)
                {
                    // A dead session must not mask the real failure or stop the
                    // web application being disposed.
                }
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// On a later page view the cached response fills the gaps in the
        /// fresh payload instead of hiding what the server just rendered.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task SessionStorageCache_CachedResponseDoesNotHideFreshPayload()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";

            using var pipeline = BuildPipeline(enableCookies: false, port);

            var app = BuildTestApp(pipeline, url, async (ctx) =>
            {
                var form = await ctx.Request.ReadFormAsync();
                return Results.Content(
                    BuildContent(pipeline, ctx,
                        d => d.Get<IJsonBuilderElementData>().Json, form),
                    "application/json");
            }, _ => MergePageHtml);

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "first page", () => 0);

                driver.Navigate().GoToUrl(url + "page2");
                WaitForFodDone(js, "second page", () => 0);

                Assert.AreEqual("purple",
                    (string)js.ExecuteScript("return window.fodValue"),
                    "the cached value must fill the gap in the fresh payload");
                Assert.IsTrue(
                    true.Equals(js.ExecuteScript("return window.fodFreshBody")),
                    "the fresh payload must not be hidden by the cached response");
            }
            finally
            {
                try
                {
                    driver?.Quit();
                }
                catch (WebDriverException)
                {
                    // A dead session must not mask the real failure or stop the
                    // web application being disposed.
                }
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// A refresh reports the query evidence of the page view making it and
        /// nothing else. The parameters object is rendered from that page
        /// view's own query string, so carrying any of it into a later page
        /// view misreports that page. The values the snippets produce do carry
        /// over, from their own storage.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task SessionStorageCache_RefreshSendsOnlyThisPageViewsQueryEvidence()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            var hang = true;
            string postBody = null;

            using var pipeline = BuildPipeline(enableCookies: false, port);

            var app = BuildTestApp(pipeline, url, async (ctx) =>
            {
                if (hang)
                {
                    try
                    {
                        await Task.Delay(Timeout.Infinite, ctx.RequestAborted);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    return Results.StatusCode(500);
                }
                var form = await ctx.Request.ReadFormAsync();
                postBody = string.Join("&",
                    form.Select(f => $"{f.Key}={f.Value}"));
                return Results.Content(
                    BuildContent(pipeline, ctx,
                        d => d.Get<IJsonBuilderElementData>().Json, form),
                    "application/json");
            }, page => page == "page1" ? ParamsFirstPageHtml : ParamsSecondPageHtml);

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                // The refresh hangs, so the first page runs its snippet and
                // saves the value it produces but never caches a response.
                WaitForStorageKey(js, "fod_data_");

                hang = false;
                driver.Navigate().GoToUrl(url + "page2");
                WaitForFodDone(js, "page with its own parameters", () => postBody == null ? 0 : 1);

                Assert.IsNotNull(postBody, "the second page must refresh");
                StringAssert.Contains(postBody, "mark=second",
                    "the current page's own query evidence must be sent");
                Assert.IsFalse(postBody.Contains("mark=first"),
                    "an earlier page view's value must not override this one");
                Assert.IsFalse(postBody.Contains("campaign=spring"),
                    "a parameter this page view does not have must not be " +
                    "resurrected from an earlier one");
                StringAssert.Contains(postBody, "51D_testvalue=purple",
                    "the values the snippets produce must still carry over");
                Assert.IsFalse(
                    GetSessionStorageKeys(js).Any(k => k.EndsWith(
                        "_parameters", StringComparison.Ordinal)),
                    "the parameters object belongs to one page view and must " +
                    "not be stored at all");
                Assert.AreEqual("purple",
                    (string)js.ExecuteScript("return window.fodValue"),
                    "the second page must get the real value");
            }
            finally
            {
                QuitDriver(driver);
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// A page view served from the cache must still announce the change.
        /// The cached branch resolves inside the constructor, before page code
        /// has had a chance to call onChange, so nothing but a deferral gets
        /// the handler registered in time.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task SessionStorageCache_CacheHitFiresOnChange()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            int posts = 0;

            using var pipeline = BuildPipeline(enableCookies: false, port);

            var app = BuildTestApp(pipeline, url, async (ctx) =>
            {
                Interlocked.Increment(ref posts);
                var form = await ctx.Request.ReadFormAsync();
                return Results.Content(
                    BuildContent(pipeline, ctx,
                        d => d.Get<IJsonBuilderElementData>().Json, form),
                    "application/json");
            }, _ => OnChangePageHtml);

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForScript(js, "return window.changeCount > 0",
                    "first page onChange", () => $"posts={posts}");
                Assert.AreEqual(1, posts, "the first page must refresh");

                driver.Navigate().GoToUrl(url + "page2");
                WaitForScript(js, "return window.changeCount > 0",
                    "cache hit onChange",
                    () => "the handler registered after the include ran was " +
                        "never called on a page view served from the cache");

                Assert.AreEqual("purple",
                    (string)js.ExecuteScript("return window.changeValue"),
                    "the handler must be given the merged payload");
                Assert.AreEqual(1, posts,
                    "the second page must still be served from the cache");
            }
            finally
            {
                QuitDriver(driver);
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// The refresh endpoint's payload is not under the template's control.
        /// The arrays in it describe the request that produced them, so a
        /// cached one must not reach a later page view, and an aspect that
        /// comes back null must not break the merge.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task SessionStorageCache_CachedPayloadShapeDoesNotLeak()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";

            using var pipeline = BuildPipeline(enableCookies: false, port);

            // A success response that still reports an error and lists a
            // snippet the next page view is not given.
            const string ResponseJson =
                "{\"device\":{\"testvalue\":\"purple\"}," +
                "\"javascriptProperties\":[\"device.testvaluejavascript\"," +
                "\"device.ghostjavascript\"]," +
                "\"errors\":[\"transient upstream problem\"]}";

            var app = BuildTestApp(pipeline, url,
                _ => Task.FromResult(Results.Content(ResponseJson, "application/json")),
                _ => PayloadPageHtml,
                // The second page renders with a null errors entry, which the
                // old merge read straight through.
                javaScript => javaScript.Replace(
                    "var json = {", "var json = {\"errors\":null,",
                    StringComparison.Ordinal));

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "first page", () => 0);

                driver.Navigate().GoToUrl(url + "page2");
                WaitForFodDone(js, "second page", () => 0);

                Assert.AreEqual("purple",
                    (string)js.ExecuteScript("return window.fodValue"),
                    "a null aspect must not stop the cached value being merged");
                Assert.AreEqual(0L,
                    js.ExecuteScript("return window.fodErrorCount"),
                    "an error from an earlier page view must not be reported " +
                    "on this one");
                Assert.AreEqual("device.testvaluejavascript",
                    (string)js.ExecuteScript("return window.fodJsProperties"),
                    "the snippet list must be the one the server rendered for " +
                    "this page view");
            }
            finally
            {
                QuitDriver(driver);
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// A cached entry that cannot be used must send the page view down the
        /// request path rather than leaving it with neither the cached values
        /// nor a refresh. 'null' is included because it parses.
        /// </summary>
        [DataTestMethod]
        [DataRow("{not json")]
        [DataRow("null")]
        [Timeout(300_000)]
        public async Task SessionStorageCache_UnusableEntryFallsBackToRequest(
            string cachedText)
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            int posts = 0;

            using var pipeline = BuildPipeline(enableCookies: false, port);

            var app = BuildTestApp(pipeline, url, async (ctx) =>
            {
                Interlocked.Increment(ref posts);
                var form = await ctx.Request.ReadFormAsync();
                return Results.Content(
                    BuildContent(pipeline, ctx,
                        d => d.Get<IJsonBuilderElementData>().Json, form),
                    "application/json");
            }, _ => PageHtml);

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "first page", () => posts);
                Assert.AreEqual(1, posts, "the first page must refresh");

                // Leave the property flags in place and damage only the
                // response, which is the state a truncated or externally
                // modified entry produces.
                js.ExecuteScript(
                    "sessionStorage.setItem('fod', arguments[0])", cachedText);

                driver.Navigate().GoToUrl(url + "page2");
                WaitForFodDone(js, "page with an unusable cached entry", () => posts);

                Assert.AreEqual(2, posts,
                    "the page view must fall back to a refresh");
                Assert.AreEqual("purple",
                    (string)js.ExecuteScript("return window.fodValue"),
                    "the page view must still end up with the value");
                Assert.AreNotEqual(cachedText,
                    (string)js.ExecuteScript("return sessionStorage.getItem('fod')"),
                    "the unusable entry must not be left in place to be " +
                    "rejected again on every later page view");
            }
            finally
            {
                QuitDriver(driver);
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// A snippet that saves nothing must be attempted again on the next
        /// page view. The template backs out of the empty
        /// javascripthardwareprofile case, and with the key stable the flag it
        /// wrote before backing out would otherwise retire the property for
        /// the whole tab session.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task SessionStorageCache_SnippetThatSavesNothingIsRetried()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";

            using var pipeline = new PipelineBuilder(_loggerFactory)
                .AddFlowElement(new TestHardwareProfileElement(_loggerFactory))
                .AddFlowElement(new BrowserCapabilityElement(_loggerFactory))
                .AddFlowElement(new SequenceElementBuilder(_loggerFactory).Build())
                .AddFlowElement(new JsonBuilderElementBuilder(_loggerFactory).Build())
                .AddFlowElement(new JavaScriptBuilderElementBuilder(_loggerFactory)
                    .SetMinify(false)
                    .SetProtocol("http")
                    .SetHost($"localhost:{port}")
                    .SetEndpoint("/51dpipeline/json")
                    .SetEnableCookies(false)
                    .Build())
                .Build();

            var app = BuildTestApp(pipeline, url, async (ctx) =>
            {
                var form = await ctx.Request.ReadFormAsync();
                return Results.Content(
                    BuildContent(pipeline, ctx,
                        d => d.Get<IJsonBuilderElementData>().Json, form),
                    "application/json");
            }, _ => PageHtml);

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "first page", () => 0);
                Assert.AreEqual(1L,
                    js.ExecuteScript("return window.hardwareProfileRuns || 0"),
                    "the snippet must run on the first page");

                driver.Navigate().GoToUrl(url + "page2");
                WaitForFodDone(js, "second page", () => 0);

                Assert.AreEqual(1L,
                    js.ExecuteScript("return window.hardwareProfileRuns || 0"),
                    "the snippet saved nothing, so the next page view must " +
                    "attempt it again instead of treating it as done");
            }
            finally
            {
                QuitDriver(driver);
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// A refresh that brings back a snippet the page has not run yet must
        /// execute it and refresh again, and must then stop.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task SessionStorageCache_SnippetArrivingInARefreshIsExecuted()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            int posts = 0;

            using var pipeline = BuildPipeline(enableCookies: false, port);

            const string WithFollowUpJson =
                "{\"device\":{\"testvalue\":\"purple\"," +
                "\"followupjavascript\":\"window.followUpRuns = " +
                "(window.followUpRuns || 0) + 1;\"}," +
                "\"javascriptProperties\":[\"device.followupjavascript\"]}";
            const string SettledJson =
                "{\"device\":{\"testvalue\":\"purple\"}," +
                "\"javascriptProperties\":[]}";

            var app = BuildTestApp(pipeline, url,
                _ => Task.FromResult(Results.Content(
                    Interlocked.Increment(ref posts) == 1
                        ? WithFollowUpJson
                        : SettledJson,
                    "application/json")),
                _ => PageHtml);

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "page with a follow-up snippet", () => posts);

                Assert.AreEqual(1L,
                    js.ExecuteScript("return window.followUpRuns || 0"),
                    "the snippet that arrived in the refresh must run exactly " +
                    "once");
                Assert.AreEqual(2, posts,
                    "the follow-up snippet must produce one further refresh " +
                    "and the flow must then settle");
            }
            finally
            {
                QuitDriver(driver);
                await app.DisposeAsync();
            }
        }

        private IPipeline BuildDelayedPipeline(int port)
        {
            return new PipelineBuilder(_loggerFactory)
                .AddFlowElement(new TestDelayedElement(_loggerFactory))
                .AddFlowElement(new BrowserCapabilityElement(_loggerFactory))
                .AddFlowElement(new SequenceElementBuilder(_loggerFactory).Build())
                .AddFlowElement(new JsonBuilderElementBuilder(_loggerFactory).Build())
                .AddFlowElement(new JavaScriptBuilderElementBuilder(_loggerFactory)
                    .SetMinify(false)
                    .SetProtocol("http")
                    .SetHost($"localhost:{port}")
                    .SetEndpoint("/51dpipeline/json")
                    .SetEnableCookies(false)
                    .Build())
                .Build();
        }

        private IPipeline BuildPipeline(bool enableCookies, int port,
            bool modernBrowser = true)
        {
            var builder = new PipelineBuilder(_loggerFactory)
                .AddFlowElement(new TestValueElement(_loggerFactory));
            if (modernBrowser)
            {
                builder = builder.AddFlowElement(
                    new BrowserCapabilityElement(_loggerFactory));
            }
            return builder
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

        private static WebApplication BuildTestApp(
            IPipeline pipeline,
            string url,
            Func<HttpContext, Task<IResult>> onJson,
            Func<string, string> pageHtml,
            Func<string, string> transformJavaScript = null)
        {
            var builder = WebApplication.CreateBuilder();
            var app = builder.Build();
            app.Use((ctx, next) =>
            {
                ctx.Response.Headers["Cache-Control"] = "no-store";
                return next();
            });
            app.MapGet("/51dpipeline/js", (HttpContext ctx) =>
            {
                var javaScript = BuildContent(pipeline, ctx,
                    d => d.Get<IJavaScriptBuilderElementData>().JavaScript);
                return Results.Content(
                    transformJavaScript == null
                        ? javaScript
                        : transformJavaScript(javaScript),
                    "text/javascript");
            });
            app.MapPost("/51dpipeline/json", onJson);
            app.MapGet("/{page}", (string page) =>
                Results.Content(pageHtml(page), "text/html"));
            app.Urls.Add(url);
            return app;
        }

        /// <summary>
        /// Waits for a script that returns a boolean to become true, failing
        /// with whatever context the caller can add.
        /// </summary>
        private static void WaitForScript(
            IJavaScriptExecutor js, string script, string phase, Func<string> detail)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
            while (true)
            {
                if (true.Equals(js.ExecuteScript(script)))
                {
                    return;
                }
                if (DateTime.UtcNow >= deadline)
                {
                    Assert.Fail($"Timed out during {phase}. {detail()}");
                }
                Thread.Sleep(200);
            }
        }

        private static void QuitDriver(ChromeDriver driver)
        {
            try
            {
                driver?.Quit();
            }
            catch (WebDriverException)
            {
                // A dead session must not mask the real failure or stop the
                // web application being disposed.
            }
        }

        private static void WaitForStorageKey(IJavaScriptExecutor js, string prefix)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
            while (true)
            {
                var keys = GetSessionStorageKeys(js);
                if (keys.Any(k => k.StartsWith(prefix, StringComparison.Ordinal)))
                {
                    return;
                }
                if (DateTime.UtcNow >= deadline)
                {
                    Assert.Fail(
                        $"Timed out waiting for a session storage key starting " +
                        $"with {prefix}, have: [{string.Join(", ", keys)}]");
                }
                Thread.Sleep(200);
            }
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
            catch (WebDriverException ex)
            {
                // Not Inconclusive: skipping here reports green for a run in
                // which none of these tests executed at all.
                Assert.Fail("Could not create a ChromeDriver, check that the " +
                    $"Chromium driver is installed: {ex.Message}");
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
