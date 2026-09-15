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

using FiftyOne.Pipeline.Engines.TestHelpers;
using FiftyOne.Pipeline.JavaScriptBuilder.Data;
using FiftyOne.Pipeline.JsonBuilder.Data;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace FiftyOne.Pipeline.JavaScript.Tests
{
    /// <summary>
    /// What refresh() does and does not do, what the record holds, and the
    /// two changes of answer James Rosewell asked to see. Tests 3, 11 to
    /// 13, 15 to 18 and 22 to 23 of pipeline-dotnet#414.
    /// </summary>
    public partial class UserPromptBrowserTests
    {
        /// <summary>
        /// Reads a key's value out of a recorded body.
        /// </summary>
        private static string ValueOf(string body, string key)
        {
            if (body == null)
            {
                return null;
            }
            foreach (var pair in body.Split('&'))
            {
                var mark = pair.IndexOf('=');
                if (mark > 0 &&
                    pair.Substring(0, mark).Equals(key, StringComparison.Ordinal))
                {
                    return pair.Substring(mark + 1);
                }
            }
            return null;
        }

        /// <summary>
        /// Test 22, which is James Rosewell's check. A change of answer on
        /// one page instance produces a new identifier, the sequence
        /// increases and onChange is called, with every other input the
        /// same.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task AChangeOfAnswerOnOnePageProducesANewIdentifier()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            var recorder = new Recorder();

            using var pipeline = BuildPipeline(port,
                new SnippetElement(_loggerFactory),
                new StubDidElement(_loggerFactory));
            var app = BuildApp(pipeline, url, recorder,
                page => Page("one page, two answers", PmpStub));

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "the first round", recorder);

                js.ExecuteScript("window.__51d_pmp_answer('standard')");
                WaitForScript(js,
                    "return !!(fod.fodid && fod.fodid.idprobglobal)",
                    "the first identifier", recorder);

                var postsAfterA = recorder.Count;
                var firstId = (string)js.ExecuteScript(
                    "return fod.fodid.idprobglobal");
                var changesAfterA = Convert.ToInt64(
                    js.ExecuteScript("return window.changeCount"),
                    System.Globalization.CultureInfo.InvariantCulture);
                var bodyA = recorder.At(postsAfterA - 1);
                StringAssert.Contains(bodyA, "id.usage=standard",
                    "the request that created the first identifier must " +
                    "carry the first answer");
                Assert.IsTrue(changesAfterA >= 1,
                    "onChange must have fired for the first identifier");

                // The visitor reopens the dialog and answers differently.
                // Nothing else about the page has changed.
                js.ExecuteScript("window.__51d_pmp_answer('personalized')");
                WaitForPostCount(recorder, postsAfterA + 1,
                    "the request for the second answer");
                WaitForScript(js,
                    "return fod.fodid.idprobglobal !== " +
                    $"'{firstId}'",
                    "the second identifier", recorder);

                var bodyB = recorder.At(postsAfterA);
                Assert.AreEqual(postsAfterA + 1, recorder.Count,
                    "exactly one further request. " + recorder.Describe());
                StringAssert.Contains(bodyB, "id.usage=personalized");
                Assert.AreEqual(
                    int.Parse(ValueOf(bodyA, "sequence"),
                        System.Globalization.CultureInfo.InvariantCulture) + 1,
                    int.Parse(ValueOf(bodyB, "sequence"),
                        System.Globalization.CultureInfo.InvariantCulture),
                    "the sequence must be one higher than the previous " +
                    "request's. " + recorder.Describe());

                var secondId = (string)js.ExecuteScript(
                    "return fod.fodid.idprobglobal");
                Assert.AreNotEqual(firstId, secondId,
                    "a change of answer must produce a new identifier");
                Assert.IsTrue(
                    Convert.ToInt64(js.ExecuteScript("return window.changeCount"),
                        System.Globalization.CultureInfo.InvariantCulture) >
                    changesAfterA,
                    "onChange must fire again with the new value");
                var lastChangeId = (string)js.ExecuteScript(
                    "return window.changeIds[window.changeIds.length - 1]");
                Assert.AreEqual(secondId, lastChangeId,
                    "and the value onChange carried must be the one the " +
                    "object now holds");
            }
            finally
            {
                QuitDriver(driver);
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// Test 23. The same across two page views in one tab. The
        /// sequence observed on the wire is recorded as it is, because it
        /// restarts at 1 with each script load and the server adds one per
        /// request, so the behaviour is pinned rather than assumed.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task AChangeOfAnswerAcrossTwoPageViewsInOneTab()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            var recorder = new Recorder();

            using var pipeline = BuildPipeline(port,
                new SnippetElement(_loggerFactory),
                new StubDidElement(_loggerFactory));
            // Page one holds answer A when the script constructs and page
            // two holds answer B, which is the visitor answering again
            // between the two page views.
            var app = BuildApp(pipeline, url, recorder,
                page => page == "page1"
                    ? Page("page one",
                        PmpStub + "window.__51d_pmp_value = 'standard';")
                    : Page("page two",
                        PmpStub + "window.__51d_pmp_value = 'personalized';"));

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "page one", recorder);
                WaitForScript(js,
                    "return !!(fod.fodid && fod.fodid.idprobglobal)",
                    "page one's identifier", recorder);

                var firstId = (string)js.ExecuteScript(
                    "return fod.fodid.idprobglobal");
                var postsOnPageOne = recorder.Count;
                var bodyOne = recorder.At(postsOnPageOne - 1);
                StringAssert.Contains(bodyOne, "id.usage=standard");
                CollectionAssert.Contains(GetSessionStorageKeys(js), "fod_inputs",
                    "page one must leave the record of what it sent behind");

                driver.Navigate().GoToUrl(url + "page2");
                WaitForFodDone(js, "page two", recorder);
                WaitForScript(js,
                    "return !!(fod.fodid && fod.fodid.idprobglobal)",
                    "page two's identifier", recorder);

                Assert.IsTrue(recorder.Count > postsOnPageOne,
                    "a different answer is a different input, so page two " +
                    "must make a fresh request. " + recorder.Describe());
                var bodyTwo = recorder.At(recorder.Count - 1);
                StringAssert.Contains(bodyTwo, "id.usage=personalized",
                    "and it must carry page two's answer");
                StringAssert.Contains(bodyTwo, "51D_testvalue=purple",
                    "and the snippet values, because the identifier is " +
                    "created only after every other piece of data");
                Assert.AreEqual(0, KeyCount(bodyTwo, "id.usage=standard"),
                    "and none of page one's answer");

                var secondId = (string)js.ExecuteScript(
                    "return fod.fodid.idprobglobal");
                Assert.AreNotEqual(firstId, secondId,
                    "a change of answer across two page views must produce " +
                    "a new identifier");
                Assert.IsTrue(
                    Convert.ToInt64(js.ExecuteScript("return window.changeCount"),
                        System.Globalization.CultureInfo.InvariantCulture) >= 1,
                    "onChange must fire on the new instance");

                // Recorded as observed rather than asserted to a designed
                // value. The sequence restarts with each script load
                // because the builder renders it, and the server adds one
                // per request.
                Assert.AreEqual("1", ValueOf(bodyTwo, "sequence"),
                    "the sequence observed on page two's request. It " +
                    "restarts at 1 with each script load, so it is not a " +
                    "running count across the tab. Page one's request " +
                    $"carried sequence={ValueOf(bodyOne, "sequence")}.");
            }
            finally
            {
                QuitDriver(driver);
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// Test 12. refresh() with nothing changed asks for nothing,
        /// because the record of what was last sent is what it compares
        /// with.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task RefreshWithUnchangedInputsMakesNoRequest()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            var recorder = new Recorder();

            using var pipeline = BuildPipeline(port,
                new SnippetElement(_loggerFactory),
                new StubDidElement(_loggerFactory));
            var app = BuildApp(pipeline, url, recorder,
                page => Page("idle refresh",
                    PmpStub + "window.__51d_pmp_value = 'standard';"));

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "the first round", recorder);
                WaitForScript(js,
                    "return !!(fod.fodid && fod.fodid.idprobglobal)",
                    "the identifier", recorder);

                var before = recorder.Count;
                js.ExecuteScript("fod.refresh(); fod.refresh(); fod.refresh()");
                // A positive observation that nothing was sent: the page is
                // still reporting the identifier it already had, and three
                // calls have been made and returned.
                WaitForScript(js,
                    "return !!(fod.fodid && fod.fodid.idprobglobal)",
                    "the identifier still being there", recorder);
                Assert.AreEqual(before, recorder.Count,
                    "nothing changed, so there is nothing to ask for. " +
                    recorder.Describe());
            }
            finally
            {
                QuitDriver(driver);
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// Test 11. refresh() on an idle page whose answer has changed
        /// makes one request carrying the stored snippet values and the
        /// new answer, runs no snippet again, and a complete() registered
        /// after the call fires when that request's response has loaded.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task RefreshSendsTheStoredValuesAndRunsNoSnippetAgain()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            var recorder = new Recorder();

            using var pipeline = BuildPipeline(port,
                new SnippetElement(_loggerFactory),
                new StubDidElement(_loggerFactory));
            var app = BuildApp(pipeline, url, recorder,
                page => Page("refresh on an idle page", PmpStub));

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "the first round", recorder);

                var runsBefore = Convert.ToInt64(
                    js.ExecuteScript("return window.snippetRuns"),
                    System.Globalization.CultureInfo.InvariantCulture);
                Assert.AreEqual(1L, runsBefore,
                    "the snippet must have run once on the first round");

                // The count is taken before the answer is announced,
                // because announcing it starts the request and reading the
                // count afterwards would already include it.
                var before = recorder.Count;
                js.ExecuteScript(
                    "window.lateComplete = ''; " +
                    "window.__51d_pmp_answer('standard'); " +
                    "fod.complete(function (data) { " +
                    "  window.lateComplete = " +
                    "    (data && data.fodid && data.fodid.idprobglobal) || ''; " +
                    "});");
                WaitForPostCount(recorder, before + 1, "the refresh request");
                WaitForScript(js, "return window.lateComplete !== ''",
                    "a complete() registered after the refresh", recorder);

                var body = recorder.At(recorder.Count - 1);
                StringAssert.Contains(body, "id.usage=standard",
                    "the refresh must carry the new answer");
                StringAssert.Contains(body, "51D_testvalue=purple",
                    "and the values the snippets already produced");
                Assert.AreEqual(runsBefore,
                    Convert.ToInt64(js.ExecuteScript("return window.snippetRuns"),
                        System.Globalization.CultureInfo.InvariantCulture),
                    "the snippets must not run again, because they read the " +
                    "device and not the consent state");
                Assert.AreEqual(
                    (string)js.ExecuteScript("return fod.fodid.idprobglobal"),
                    (string)js.ExecuteScript("return window.lateComplete"),
                    "complete() registered after the call must fire when " +
                    "that request's response has loaded");
            }
            finally
            {
                QuitDriver(driver);
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// Test 13, and test 8 with it. An answer that arrives while a
        /// round is in progress is carried by that round's own next
        /// request, and no extra request follows. The endpoint is held by
        /// a gate the test releases, so nothing here reads a clock.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task AnAnswerArrivingDuringARoundIsCarriedByThatRound()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            var recorder = new Recorder
            {
                Gate = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously),
            };

            using var pipeline = BuildPipeline(port,
                new SnippetElement(_loggerFactory),
                new StubDidElement(_loggerFactory));
            var app = BuildApp(pipeline, url, recorder,
                page => Page("answer during a round", PmpStub));

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                // The first request is in flight and held by the gate.
                WaitForPostCount(recorder, 1, "the held first request");
                AssertNoAnswerKeys(recorder.At(0),
                    "the first request, sent before any answer");

                // The visitor answers whilst that request is still open.
                js.ExecuteScript("window.__51d_pmp_answer('standard')");

                // Release it. The round then ends and asks again, once,
                // with the answer.
                recorder.Gate.SetResult(true);
                recorder.Gate = null;

                WaitForPostCount(recorder, 2, "the round's own next request");
                WaitForScript(js,
                    "return !!(fod.fodid && fod.fodid.idprobglobal)",
                    "the identifier", recorder);

                StringAssert.Contains(recorder.At(1), "id.usage=standard",
                    "the answer that arrived mid round must be carried by " +
                    "that round's own next request");
                StringAssert.Contains(recorder.At(1), "51D_testvalue=purple",
                    "along with every snippet result");
                Assert.AreEqual(2, recorder.Count,
                    "and no extra request follows. " + recorder.Describe());
            }
            finally
            {
                if (recorder.Gate != null)
                {
                    recorder.Gate.TrySetResult(true);
                }
                QuitDriver(driver);
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// Test 15. The page changes what it puts in the evidence object
        /// and asks for a refresh, and the next request carries the new
        /// value.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task ChangingTheEvidenceObjectAndRefreshingSendsTheNewValue()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            var recorder = new Recorder();

            using var pipeline = BuildPipeline(port,
                new SnippetElement(_loggerFactory),
                new StubDidElement(_loggerFactory));
            var app = BuildApp(pipeline, url, recorder,
                page => Page("evidence object",
                    PmpStub + "window.__51d_pmp_value = 'standard'; " +
                    "window.fodEvidence = { 'query.mark': 'first' };"));

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "the first round", recorder);
                StringAssert.Contains(recorder.At(0), "query.mark=first",
                    "the evidence object must reach the first request");

                var before = recorder.Count;
                js.ExecuteScript(
                    "window.fodEvidence['query.mark'] = 'second'; fod.refresh()");
                WaitForPostCount(recorder, before + 1, "the refresh");
                StringAssert.Contains(recorder.At(before), "query.mark=second",
                    "the evidence object is read at the moment the body is " +
                    "built, so a change reaches the next request");
                Assert.AreEqual(0, KeyCount(recorder.At(before), "query.mark=first"),
                    "and the old value does not go with it");

                // Reassigning the object rather than changing it in place
                // has to work the same way.
                js.ExecuteScript(
                    "window.fodEvidence = { 'query.mark': 'third' }; fod.refresh()");
                WaitForPostCount(recorder, before + 2, "the second refresh");
                StringAssert.Contains(recorder.At(before + 1), "query.mark=third",
                    "a reassigned evidence object must be read as well");
            }
            finally
            {
                QuitDriver(driver);
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// Test 18. At the server's iteration cap refresh() asks for
        /// nothing and says so. The number it names is the JSON builder's
        /// own constant, which UserPromptRenderTests pins against the
        /// template text.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task RefreshAtTheIterationCapSendsNothingAndSaysSo()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            var recorder = new Recorder();

            using var pipeline = BuildPipeline(port,
                new SnippetElement(_loggerFactory),
                new StubDidElement(_loggerFactory));
            // The sequence element adds one to what the request carried,
            // so nine renders a script that starts at the cap of ten.
            var app = BuildApp(pipeline, url, recorder,
                page => Page("at the cap", PmpStub, query: "sequence=9"));

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "the page at the cap", recorder);

                var before = recorder.Count;
                js.ExecuteScript("window.__51d_pmp_answer('standard')");
                js.ExecuteScript("fod.refresh()");
                WaitForScript(js,
                    "return window.fodDone === true",
                    "the page settling", recorder);

                Assert.AreEqual(before, recorder.Count,
                    "the iterations of this page view are finished, so " +
                    "nothing may be asked for. " + recorder.Describe());

                var expected = JsonBuilder.Constants.MAX_JAVASCRIPT_ITERATIONS
                    .ToString(System.Globalization.CultureInfo.InvariantCulture);
                var logged = GetConsoleMessages(driver)
                    .Where(m => m.Contains("51Degrees: the maximum of",
                        StringComparison.Ordinal))
                    .ToList();
                Assert.IsTrue(logged.Count >= 1,
                    "refresh() at the cap must say so in the console, saw: " +
                    string.Join(" | ", GetConsoleMessages(driver)));
                StringAssert.Contains(logged[0], $"maximum of {expected} iterations",
                    "and name the number the server stops at");
                StringAssert.Contains(logged[0], "refresh() does nothing");
            }
            finally
            {
                QuitDriver(driver);
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// Test 16, and test 26's second half. The script loaded twice on
        /// one page says so once, on the second load, and the message
        /// names the object and prints no payload.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task LoadingTheScriptTwiceWarnsOnceOnTheSecondLoad()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            var recorder = new Recorder();

            using var pipeline = BuildPipeline(port,
                new SnippetElement(_loggerFactory),
                new StubDidElement(_loggerFactory));
            var app = BuildApp(pipeline, url, recorder,
                page => Page("one instance",
                    PmpStub + "window.__51d_pmp_value = 'standard';"));

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "the first load", recorder);

                var afterFirst = GetConsoleMessages(driver)
                    .Where(m => m.Contains("already exists on this page",
                        StringComparison.Ordinal))
                    .ToList();
                Assert.AreEqual(0, afterFirst.Count,
                    "the first load must say nothing, saw: " +
                    string.Join(" | ", afterFirst));

                // Something else on the page adds the script again, which
                // is what the Preference Management Platform must not do
                // when the object is already there.
                js.ExecuteScript(
                    "var s = document.createElement('script'); " +
                    "s.src = '/51dpipeline/js?again=1'; " +
                    "s.onload = function () { window.secondLoaded = true; }; " +
                    "document.head.appendChild(s);");
                WaitForScript(js, "return window.secondLoaded === true",
                    "the second script load", recorder);

                var afterSecond = GetConsoleMessages(driver)
                    .Where(m => m.Contains("already exists on this page",
                        StringComparison.Ordinal))
                    .ToList();
                Assert.AreEqual(1, afterSecond.Count,
                    "the second load must say so exactly once, saw: " +
                    string.Join(" | ", afterSecond));
                StringAssert.Contains(afterSecond[0], "fod already exists",
                    "and name the object");
                Assert.IsFalse(
                    afterSecond[0].Contains("51Did-", StringComparison.Ordinal),
                    "and print no payload, message was: " + afterSecond[0]);
            }
            finally
            {
                QuitDriver(driver);
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// Test 26's first half. The ordinary case, being a page with the
        /// client script and no preference platform tag of its own, still
        /// works. The Preference Management Platform adds this script to a
        /// page that carries no such object, so the one instance warning
        /// must not fire for that.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task TheOrdinaryPageWithOneScriptAndNoPlatformStillWorks()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            var recorder = new Recorder();

            using var pipeline = BuildPipeline(port,
                new SnippetElement(_loggerFactory),
                new StubDidElement(_loggerFactory));
            var app = BuildApp(pipeline, url, recorder,
                page => Page("ordinary page"));

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "the ordinary page", recorder);

                Assert.AreEqual("purple",
                    (string)js.ExecuteScript("return window.fodValue"),
                    "the page must get its values as it always did");
                Assert.AreEqual(1, recorder.Count,
                    "one request and no more. " + recorder.Describe());
                Assert.AreEqual(0, GetConsoleMessages(driver)
                    .Count(m => m.Contains("already exists on this page",
                        StringComparison.Ordinal)),
                    "one script tag must never trip the one instance warning");
            }
            finally
            {
                QuitDriver(driver);
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// Test 3. The record's key is the storage key with an underscore
        /// and the word inputs, a failed refresh removes it, and nothing is
        /// stored under a key ending in the old parameters name.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task TheRecordKeyIsFodInputsAndAFailedRefreshRemovesIt()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            var recorder = new Recorder();
            var fail = false;

            using var pipeline = BuildPipeline(port,
                new SnippetElement(_loggerFactory),
                new StubDidElement(_loggerFactory));

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
                recorder.Add(string.Join("&",
                    form.Select(f => $"{f.Key}={f.Value}")));
                if (fail)
                {
                    return Results.Content("not json", "application/json");
                }
                return Results.Content(
                    BuildContent(pipeline, ctx,
                        d => d.Get<IJsonBuilderElementData>().Json, form),
                    "application/json");
            });
            app.MapGet("/{page}", (string page) => Results.Content(
                Page("record key", PmpStub), "text/html"));
            app.Urls.Add(url);

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "the first round", recorder);

                var keys = GetSessionStorageKeys(js);
                CollectionAssert.Contains(keys, "fod_inputs",
                    "the record of the request's inputs is kept under the " +
                    "storage key and the word inputs, keys were: " +
                    string.Join(", ", keys));
                Assert.IsFalse(
                    keys.Any(k => k.EndsWith("_parameters",
                        StringComparison.Ordinal)),
                    "the parameters object belongs to one page view and must " +
                    "not be stored at all, keys were: " +
                    string.Join(", ", keys));

                fail = true;
                var before = recorder.Count;
                js.ExecuteScript("window.__51d_pmp_answer('standard')");
                WaitForPostCount(recorder, before + 1, "the failing refresh");
                WaitForScript(js,
                    "return Object.keys(sessionStorage).indexOf('fod_inputs') === -1",
                    "the record being removed by the failed refresh", recorder);

                var leftover = GetSessionStorageKeys(js)
                    .Where(k => k == "fod" ||
                        k.StartsWith("fod_", StringComparison.Ordinal))
                    .ToList();
                Assert.AreEqual(0, leftover.Count,
                    "a failed refresh must clear every cached entry, the " +
                    "record included, left: " + string.Join(", ", leftover));
            }
            finally
            {
                QuitDriver(driver);
                await app.DisposeAsync();
            }
        }
    }
}
