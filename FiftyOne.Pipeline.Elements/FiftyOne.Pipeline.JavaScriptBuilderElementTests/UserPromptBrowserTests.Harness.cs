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
using FiftyOne.Pipeline.JavaScriptBuilder.Data;
using FiftyOne.Pipeline.JavaScriptBuilder.FlowElement;
using FiftyOne.Pipeline.JsonBuilder.Data;
using FiftyOne.Pipeline.JsonBuilder.FlowElement;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace FiftyOne.Pipeline.JavaScript.Tests
{
    /// <summary>
    /// The harness the user prompt browser tests are written against. It is
    /// the SessionStorageCacheTests pattern with the additions work package
    /// pipeline-dotnet#414 asks for, being a stub 51Did element so the block
    /// renders, a record of every request body, page variants that place a
    /// preference platform on the page before the include, and a gate the
    /// test releases so a request can be held in flight without abandoning
    /// the page.
    /// </summary>
    public partial class UserPromptBrowserTests
    {
        /// <summary>
        /// Text that exists only inside the template's user prompt section,
        /// so a test can say whether the block itself rendered. The guard
        /// string sits outside the section, so it is not this.
        /// </summary>
        internal const string BlockOnlyName = "51d-pmp-preference";

        private readonly TestLoggerFactory _loggerFactory =
            new TestLoggerFactory();

        #region Elements

        private class TestValueData : ElementDataBase
        {
            public TestValueData(ILogger<ElementDataBase> logger, IPipeline pipeline)
                : base(logger, pipeline)
            {
            }
        }

        /// <summary>
        /// Supplies the two properties the builder reads to decide which
        /// transport the template is rendered with, so these tests run the
        /// fetch and promises render a current browser gets.
        /// </summary>
        private class BrowserCapabilityElement
            : FlowElementBase<TestValueData, ElementPropertyMetaData>
        {
            private readonly ILoggerFactory _loggerFactory;

            public BrowserCapabilityElement(ILoggerFactory loggerFactory)
                : base(loggerFactory.CreateLogger<
                    FlowElementBase<TestValueData, ElementPropertyMetaData>>())
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
                var result = new TestValueData(
                    _loggerFactory.CreateLogger<TestValueData>(), data.Pipeline);
                result["Promise"] = new AspectPropertyValue<string>("Full");
                result["Fetch"] = new AspectPropertyValue<bool>(true);
                data.GetOrAdd(ElementDataKey, p => result);
            }

            protected override void ManagedResourcesCleanup() { }
            protected override void UnmanagedResourcesCleanup() { }
        }

        /// <summary>
        /// One snippet that saves a value on the client and reports it back
        /// once the client has it, which is what makes a request that
        /// carries every snippet result distinguishable from one that does
        /// not.
        /// </summary>
        private class SnippetElement
            : FlowElementBase<TestValueData, ElementPropertyMetaData>
        {
            private readonly ILoggerFactory _loggerFactory;

            public SnippetElement(ILoggerFactory loggerFactory)
                : base(loggerFactory.CreateLogger<
                    FlowElementBase<TestValueData, ElementPropertyMetaData>>())
            {
                _loggerFactory = loggerFactory;
            }

            /// <summary>
            /// Counts how many times the snippet body has been handed out
            /// with something in it, which is not the same as how many
            /// times the browser ran it. The browser side count is kept on
            /// the page in window.snippetRuns.
            /// </summary>
            public int SnippetsIssued;

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
                    new ElementPropertyMetaData(this, "testvaluejavascript",
                        typeof(Core.Data.Types.JavaScript), true),
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
                    Interlocked.Increment(ref SnippetsIssued);
                    // The run is counted on the page as well, so a test can
                    // say whether a refresh ran the snippets again.
                    result["testvaluejavascript"] = new Core.Data.Types.JavaScript(
                        "window.snippetRuns = (window.snippetRuns || 0) + 1; " +
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

        /// <summary>
        /// An element whose only snippet is the hardware profile one, which
        /// the template special cases. TestHardwareProfileElement in
        /// SessionStorageCacheTests also carries testvaluejavascript and so
        /// masks the case this covers.
        /// </summary>
        private class HardwareProfileOnlyElement
            : FlowElementBase<TestValueData, ElementPropertyMetaData>
        {
            private readonly ILoggerFactory _loggerFactory;

            public HardwareProfileOnlyElement(ILoggerFactory loggerFactory)
                : base(loggerFactory.CreateLogger<
                    FlowElementBase<TestValueData, ElementPropertyMetaData>>())
            {
                _loggerFactory = loggerFactory;
            }

            public override string ElementDataKey => "device";

            public override IEvidenceKeyFilter EvidenceKeyFilter =>
                new EvidenceKeyFilterWhitelist(new List<string>());

            public override IList<ElementPropertyMetaData> Properties =>
                new List<ElementPropertyMetaData>()
                {
                    new ElementPropertyMetaData(this, "testvalue", typeof(string), true),
                    new ElementPropertyMetaData(this, "javascripthardwareprofile",
                        typeof(Core.Data.Types.JavaScript), true),
                };

            protected override void ProcessInternal(IFlowData data)
            {
                var result = new TestValueData(
                    _loggerFactory.CreateLogger<TestValueData>(), data.Pipeline);
                result["testvalue"] = "fixed";
                // Records the run and deliberately saves no profile ids,
                // which is the case the template backs out of.
                result["javascripthardwareprofile"] = new Core.Data.Types.JavaScript(
                    "window.hardwareProfileRuns = (window.hardwareProfileRuns || 0) + 1;");
                data.GetOrAdd(ElementDataKey, p => result);
            }

            protected override void ManagedResourcesCleanup() { }
            protected override void UnmanagedResourcesCleanup() { }
        }

        private class DidData : ElementDataBase
        {
            public DidData(ILogger<ElementDataBase> logger, IPipeline pipeline)
                : base(logger, pipeline)
            {
            }
        }

        /// <summary>
        /// Stands in for one of the two 51Did engines. It declares the
        /// element data key both of them return, so the builder's rule sees
        /// a 51Did element and renders the block, and the JSON builder puts
        /// its data in the payload under that key lower cased, which is the
        /// fodid section a page reads. Every response carries a different
        /// identifier, because the real service signs each one with a fresh
        /// random value, so a test can say whether a new identifier was
        /// created.
        /// </summary>
        private class StubDidElement
            : FlowElementBase<DidData, ElementPropertyMetaData>
        {
            private readonly ILoggerFactory _loggerFactory;
            private readonly bool _available;
            private int _issued;

            public StubDidElement(ILoggerFactory loggerFactory, bool available = true)
                : base(loggerFactory.CreateLogger<
                    FlowElementBase<DidData, ElementPropertyMetaData>>())
            {
                _loggerFactory = loggerFactory;
                _available = available;
            }

            /// <summary>
            /// Every request whose body named a usage or a framework string.
            /// Appended under a lock, because Kestrel serves requests on the
            /// thread pool.
            /// </summary>
            public int Issued => _issued;

            public override string ElementDataKey =>
                JavaScriptBuilderElement.FODID_ELEMENT_DATA_KEY;

            public override IEvidenceKeyFilter EvidenceKeyFilter =>
                new EvidenceKeyFilterWhitelist(new List<string>() {
                    "query.id.usage",
                    "query.tcstring",
                });

            public override IList<ElementPropertyMetaData> Properties =>
                new List<ElementPropertyMetaData>()
                {
                    new ElementPropertyMetaData(
                        this, "IdProbGlobal", typeof(string), _available),
                };

            protected override void ProcessInternal(IFlowData data)
            {
                var result = new DidData(
                    _loggerFactory.CreateLogger<DidData>(), data.Pipeline);
                // Nothing is created without a usage signal, which is what
                // the on premise engine does, so a page that nobody was
                // asked on produces no identifier.
                var hasSignal =
                    (data.TryGetEvidence<object>(
                        "query.id.usage", out var usage) &&
                        string.IsNullOrEmpty(usage?.ToString()) == false) ||
                    (data.TryGetEvidence<object>(
                        "query.tcstring", out var tc) &&
                        string.IsNullOrEmpty(tc?.ToString()) == false);
                if (hasSignal)
                {
                    var n = Interlocked.Increment(ref _issued);
                    result["IdProbGlobal"] = new AspectPropertyValue<string>(
                        "51Did-" + n.ToString(
                            System.Globalization.CultureInfo.InvariantCulture));
                }
                data.GetOrAdd(ElementDataKey, p => result);
            }

            protected override void ManagedResourcesCleanup() { }
            protected override void UnmanagedResourcesCleanup() { }
        }

        #endregion

        #region Pipelines

        private IPipeline BuildPipeline(
            int port,
            IFlowElement first,
            StubDidElement didElement)
        {
            var builder = new PipelineBuilder(_loggerFactory)
                .AddFlowElement(first)
                .AddFlowElement(new BrowserCapabilityElement(_loggerFactory));
            if (didElement != null)
            {
                builder = builder.AddFlowElement(didElement);
            }
            return builder
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

        #endregion

        #region The test server

        /// <summary>
        /// Records every request body, in order, so a test can say what the
        /// first and the second request each carried. Also holds the gate a
        /// test releases to keep a request in flight.
        /// </summary>
        internal sealed class Recorder
        {
            private readonly object _lock = new object();
            private readonly List<string> _bodies = new List<string>();

            public TaskCompletionSource<bool> Gate { get; set; }

            public void Add(string body)
            {
                lock (_lock)
                {
                    _bodies.Add(body);
                }
            }

            public int Count
            {
                get { lock (_lock) { return _bodies.Count; } }
            }

            public string At(int index)
            {
                lock (_lock)
                {
                    return index < _bodies.Count ? _bodies[index] : null;
                }
            }

            public IReadOnlyList<string> All
            {
                get { lock (_lock) { return _bodies.ToList(); } }
            }

            public string Describe()
                => "bodies: [" + string.Join(" | ", All) + "]";
        }

        private static WebApplication BuildApp(
            IPipeline pipeline,
            string url,
            Recorder recorder,
            Func<string, string> pageHtml,
            Action<string> onJavaScript = null)
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
                onJavaScript?.Invoke(javaScript);
                return Results.Content(javaScript, "text/javascript");
            });
            app.MapPost("/51dpipeline/json", async (HttpContext ctx) =>
            {
                // The raw body is recorded rather than the parsed form,
                // because a key sent twice arrives as one form entry with
                // both values in it, and telling one key sent twice from
                // one key holding a comma is exactly what a test here has
                // to do.
                ctx.Request.EnableBuffering();
                string raw;
                using (var reader = new StreamReader(
                    ctx.Request.Body,
                    System.Text.Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: false,
                    bufferSize: 4096,
                    leaveOpen: true))
                {
                    raw = await reader.ReadToEndAsync();
                }
                ctx.Request.Body.Position = 0;
                recorder.Add(raw);
                var form = await ctx.Request.ReadFormAsync();
                var gate = recorder.Gate;
                if (gate != null)
                {
                    await gate.Task;
                }
                return Results.Content(
                    BuildContent(pipeline, ctx,
                        d => d.Get<IJsonBuilderElementData>().Json, form),
                    "application/json");
            });
            app.MapGet("/{page}", (string page) =>
                Results.Content(pageHtml(page), "text/html"));
            app.Urls.Add(url);
            return app;
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

        #endregion

        #region Pages

        /// <summary>
        /// A page that loads the script with whatever the caller wants in
        /// front of it, and reports back through a handful of window values
        /// every test reads the same way.
        /// </summary>
        /// <param name="name">
        /// The page title, so a failure says which page it was on.
        /// </param>
        /// <param name="beforeInclude">
        /// Script that runs before the include, which is where a preference
        /// platform's stub has to be, as the framework specification
        /// already requires of a consent platform.
        /// </param>
        /// <param name="query">
        /// Appended to the include's URL, without the leading question
        /// mark.
        /// </param>
        /// <param name="afterInclude">
        /// Script that runs after the include and before the load handler.
        /// </param>
        private static string Page(
            string name,
            string beforeInclude = "",
            string query = "",
            string afterInclude = "")
        {
            var src = "/51dpipeline/js" +
                (string.IsNullOrEmpty(query) ? "" : "?" + query);
            return $@"<!DOCTYPE html>
<html>
  <head>
    <title>{name}</title>
    <script>
      window.fodLog = [];
      window.snippetRuns = 0;
      window.changeCount = 0;
      window.changeIds = [];
      window.completeCount = 0;
      window.fodDone = false;
      window.fodValue = '';
{beforeInclude}
    </script>
    <script src=""{src}""></script>
    <script>
{afterInclude}
      window.addEventListener('load', function () {{
        fod.onChange(function (data) {{
          window.changeCount = window.changeCount + 1;
          window.changeIds.push(
            (data && data.fodid && data.fodid.idprobglobal) || '');
        }});
        fod.complete(function (data) {{
          window.completeCount = window.completeCount + 1;
          window.fodDone = true;
          window.fodValue =
            (data && data.device && data.device.testvalue) || '';
        }});
      }});
    </script>
  </head>
  <body>{name}</body>
</html>";
        }

        /// <summary>
        /// A framework platform's stub. The listener is kept on the window
        /// so a test can fire it when it chooses, and the calls are counted,
        /// so nothing in these tests reads a clock to decide that the block
        /// did not wait.
        /// </summary>
        /// <param name="status">
        /// What the stub reports when the test fires it, being 'loading'
        /// for a platform that has not answered yet.
        /// </param>
        private const string TcfStub = @"
      window.__tcfCalls = 0;
      window.__tcfListener = null;
      window.__tcfapi = function (command, version, callback) {
        window.__tcfCalls = window.__tcfCalls + 1;
        if (command === 'addEventListener') {
          window.__tcfListener = callback;
        }
      };
      window.__tcfDeliver = function (tcString, status) {
        window.__tcfListener(
          { eventStatus: status || 'tcloaded', tcString: tcString }, true);
      };
      window.__tcfClear = function () {
        window.__tcfListener(null, false);
      };
";

        /// <summary>
        /// The Preference Management Platform's own surface. It answers from
        /// memory and announces a change on the window event, which is what
        /// the real one does, and nothing new is written to storage.
        /// </summary>
        private const string PmpStub = @"
      window.__51d_pmp_value = undefined;
      window.__51d_pmp = {
        preference: function () { return window.__51d_pmp_value; }
      };
      window.__51d_pmp_answer = function (value) {
        window.__51d_pmp_value = value;
        window.dispatchEvent(new CustomEvent(
          '51d-pmp-preference', { detail: { preference: value } }));
      };
";

        /// <summary>
        /// A Global Privacy Platform stub and nothing else. Its calls are
        /// counted so a test can say the block never asked it anything.
        /// </summary>
        private const string GppStub = @"
      window.__gppCalls = 0;
      window.__gpp = function () {
        window.__gppCalls = window.__gppCalls + 1;
      };
";

        #endregion

        #region Waiting, always on an observation and never on a clock

        private static void WaitForPostCount(
            Recorder recorder, int expected, string phase)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
            while (true)
            {
                if (recorder.Count >= expected)
                {
                    return;
                }
                if (DateTime.UtcNow >= deadline)
                {
                    Assert.Fail($"Timed out during {phase}. The endpoint was " +
                        $"reached {recorder.Count} times, expected {expected}. " +
                        recorder.Describe());
                }
                Thread.Sleep(100);
            }
        }

        private static void WaitForFodDone(
            IJavaScriptExecutor js, string phase, Recorder recorder)
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
                    var errors = js.ExecuteScript(
                        "return typeof fod === 'undefined' || !fod.errors ? " +
                        "'' : JSON.stringify(fod.errors)");
                    var storage = js.ExecuteScript(
                        "return Object.keys(sessionStorage).join(',')");
                    Assert.Fail($"Timed out during {phase}. errors=[{errors}], " +
                        $"sessionStorage=[{storage}], " + recorder.Describe());
                }
                Thread.Sleep(200);
            }
        }

        private static void WaitForScript(
            IJavaScriptExecutor js, string script, string phase,
            Recorder recorder)
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
                    Assert.Fail($"Timed out during {phase}, waiting for " +
                        $"\"{script}\". " + recorder.Describe());
                }
                Thread.Sleep(100);
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

        private static List<string> GetConsoleMessages(ChromeDriver driver)
            => driver.Manage().Logs.GetLog(LogType.Browser)
                .Select(e => e.Message)
                .ToList();

        private static ChromeDriver CreateDriver()
        {
            try
            {
                return JavaScriptBuilderElementTestsBase.CreateConfiguredDriver();
            }
            catch (WebDriverException ex)
            {
                // Not Inconclusive, because skipping here reports green for
                // a run in which none of these tests executed at all.
                Assert.Fail("Could not create a ChromeDriver, check that the " +
                    $"Chromium driver is installed: {ex.Message}");
                return null;
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

        #endregion
    }
}
