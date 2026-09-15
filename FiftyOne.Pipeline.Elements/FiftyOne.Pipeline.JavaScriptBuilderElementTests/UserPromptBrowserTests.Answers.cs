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
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace FiftyOne.Pipeline.JavaScript.Tests
{
    /// <summary>
    /// Where the visitor's answer comes from, what reaches the wire and what
    /// does not. Tests 4 to 10 and 24 to 26 of pipeline-dotnet#414.
    /// </summary>
    [TestClass]
    public partial class UserPromptBrowserTests
    {
        /// <summary>
        /// Counts how many times a key appears in a recorded request body.
        /// A key sent twice is what the server keeps as a repeated form key
        /// and every reader then sees as absent, so counting matters.
        /// </summary>
        private static int KeyCount(string body, string key)
            => body == null
                ? 0
                : ("&" + body).Split(new[] { "&" + key + "=" },
                    StringSplitOptions.None).Length - 1;

        private static void AssertNoAnswerKeys(string body, string phase)
        {
            foreach (var key in new[] { "id.usage", "tcstring", "gpp", "gppstring" })
            {
                Assert.AreEqual(0, KeyCount(body, key),
                    $"{phase} must carry no {key}, body was: {body}");
            }
        }

        /// <summary>
        /// Test 24. A page with the block rendered and no preference
        /// platform at all warns exactly once, prints no value, and still
        /// makes its first request.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task NoPlatformWarnsOnceAndTheFirstRequestStillGoes()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            var recorder = new Recorder();
            string served = null;

            using var pipeline = BuildPipeline(port,
                new SnippetElement(_loggerFactory),
                new StubDidElement(_loggerFactory));
            var app = BuildApp(pipeline, url, recorder,
                page => Page("no platform"), js => served = js);

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "the page with no platform", recorder);

                StringAssert.Contains(served, BlockOnlyName,
                    "the block must have rendered, otherwise this test is " +
                    "not testing what it says");
                Assert.AreEqual(1, recorder.Count,
                    "the first request must still go. " + recorder.Describe());
                AssertNoAnswerKeys(recorder.At(0), "a page with no platform");

                var warnings = GetConsoleMessages(driver)
                    .Where(m => m.Contains("no preference platform was found",
                        StringComparison.Ordinal))
                    .ToList();
                Assert.AreEqual(1, warnings.Count,
                    "the warning must fire exactly once, saw: " +
                    string.Join(" | ", warnings));
                Assert.IsFalse(
                    warnings[0].Contains("standard", StringComparison.Ordinal) ||
                    warnings[0].Contains("personalized", StringComparison.Ordinal),
                    "no value may be printed by this block, warning was: " +
                    warnings[0]);
            }
            finally
            {
                QuitDriver(driver);
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// Test 9. A page carrying a Global Privacy Platform stub and
        /// nothing else sends no consent key, and the block never calls it,
        /// because the Model Terms for Marketing do not map that platform.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task AGlobalPrivacyPlatformStubIsNeverCalledAndSendsNothing()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            var recorder = new Recorder();

            using var pipeline = BuildPipeline(port,
                new SnippetElement(_loggerFactory),
                new StubDidElement(_loggerFactory));
            var app = BuildApp(pipeline, url, recorder,
                page => Page("gpp only", GppStub));

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "the page with a privacy platform stub",
                    recorder);

                Assert.AreEqual(1, recorder.Count,
                    "one request and no more. " + recorder.Describe());
                AssertNoAnswerKeys(recorder.At(0),
                    "a page with only a privacy platform stub");
                Assert.AreEqual(0L,
                    Convert.ToInt64(js.ExecuteScript("return window.__gppCalls"),
                        System.Globalization.CultureInfo.InvariantCulture),
                    "the block must never call the Global Privacy Platform");
            }
            finally
            {
                QuitDriver(driver);
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// Test 7. The late answer, which is the common path. Nothing at
        /// load, then a delivery on the framework listener, then a second
        /// request carrying the string and an identifier in the response,
        /// all in one page view.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task ALateFrameworkAnswerProducesASecondRequestAndAnIdentifier()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            var recorder = new Recorder();

            using var pipeline = BuildPipeline(port,
                new SnippetElement(_loggerFactory),
                new StubDidElement(_loggerFactory));
            var app = BuildApp(pipeline, url, recorder,
                page => Page("late answer", TcfStub));

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "the first round", recorder);

                Assert.AreEqual(1, recorder.Count,
                    "the first request must not wait for the platform. " +
                    recorder.Describe());
                AssertNoAnswerKeys(recorder.At(0),
                    "the first request, before the platform answered");
                Assert.AreEqual("", (string)js.ExecuteScript(
                    "return (fod.fodid && fod.fodid.idprobglobal) || ''"),
                    "no identifier is created before an answer");

                js.ExecuteScript("window.__tcfDeliver('CPtestString')");
                WaitForPostCount(recorder, 2, "the request after the answer");
                WaitForScript(js,
                    "return !!(fod.fodid && fod.fodid.idprobglobal)",
                    "the identifier arriving", recorder);

                StringAssert.Contains(recorder.At(1), "tcstring=CPtestString",
                    "the second request must carry the framework string");
                StringAssert.Contains(recorder.At(1), "51D_testvalue=purple",
                    "and every snippet result, because the identifier is " +
                    "created only after every other piece of data");
                Assert.AreEqual(2, recorder.Count,
                    "exactly one further request. " + recorder.Describe());
            }
            finally
            {
                QuitDriver(driver);
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// Test 5. On a Preference Management Platform page the framework
        /// surface is the platform's own. The platform's answer is what is
        /// sent, the framework string beside it is not, and the block still
        /// registers on the framework surface.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task ThePlatformsOwnAnswerWinsOverItsFrameworkSurface()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            var recorder = new Recorder();

            using var pipeline = BuildPipeline(port,
                new SnippetElement(_loggerFactory),
                new StubDidElement(_loggerFactory));
            // The platform has already answered when the script constructs,
            // which is a returning visitor whose answer is in the shared
            // store. The framework surface is the platform's own, as the
            // rule that the two never share a page requires.
            var app = BuildApp(pipeline, url, recorder,
                page => Page("platform page",
                    PmpStub + TcfStub + "\n      window.__51d_pmp_value = 'standard';"));

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "the platform page", recorder);

                StringAssert.Contains(recorder.At(0), "id.usage=standard",
                    "the platform's answer is what reaches the wire");
                Assert.AreEqual(0, KeyCount(recorder.At(0), "tcstring"),
                    "the first source with an answer wins and the rest are " +
                    "ignored, body was: " + recorder.At(0));
                Assert.IsTrue(
                    Convert.ToInt64(js.ExecuteScript("return window.__tcfCalls"),
                        System.Globalization.CultureInfo.InvariantCulture) >= 1,
                    "the block must still register on the framework surface");

                // The framework surface then delivers a string, which must
                // not displace the platform's answer.
                js.ExecuteScript("window.__tcfDeliver('CPshouldNotBeSent')");
                Thread.Sleep(500);
                foreach (var body in recorder.All)
                {
                    Assert.AreEqual(0, KeyCount(body, "tcstring"),
                        "a framework string must never displace the " +
                        "platform's own answer, body was: " + body);
                }
            }
            finally
            {
                QuitDriver(driver);
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// Test 6. The alternative button stores non-marketing, which is an
        /// answer under the Model Terms and not a refusal, so it is sent as
        /// a usage like the other two.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        [DataRow("non-marketing")]
        [DataRow("standard")]
        [DataRow("personalized")]
        public async Task AnAnswerHeldInTheKeyIsSentAsAUsage(string answer)
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            var recorder = new Recorder();

            using var pipeline = BuildPipeline(port,
                new SnippetElement(_loggerFactory),
                new StubDidElement(_loggerFactory));
            // The script constructs before the platform's bundle has
            // loaded, on a site where the visitor has already answered.
            var seed = $@"
      try {{
        localStorage.setItem('__51d_pmp_pref', JSON.stringify(
          {{ v: 1, p: '{answer}', t: Date.now() }}));
      }} catch (e) {{ }}
";
            var app = BuildApp(pipeline, url, recorder,
                page => Page("stored answer", seed));

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                // The seed has to be in place before the include runs, and
                // a page cannot write another origin's storage, so the
                // first navigation puts the entry there and the second is
                // the page under test.
                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "the page with a stored answer", recorder);

                StringAssert.Contains(recorder.At(0), $"id.usage={answer}",
                    "the stored answer must reach the first request");
                WaitForScript(js,
                    "return !!(fod.fodid && fod.fodid.idprobglobal)",
                    "the identifier arriving", recorder);
            }
            finally
            {
                QuitDriver(driver);
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// Test 4. The duplicate key case, which is what would have
        /// produced no identifier at all on the documented platform page
        /// and on every Prebid page. The script URL states a usage and the
        /// platform holds the same one, and the key still goes once.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task AUsageInTheUrlAndInTheKeyIsSentOnce()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            var recorder = new Recorder();

            using var pipeline = BuildPipeline(port,
                new SnippetElement(_loggerFactory),
                new StubDidElement(_loggerFactory));
            var app = BuildApp(pipeline, url, recorder,
                page => Page("duplicate usage",
                    PmpStub + "\n      window.__51d_pmp_value = 'standard';",
                    query: "id.usage=standard"));

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "the duplicate usage page", recorder);

                Assert.AreEqual(1, KeyCount(recorder.At(0), "id.usage"),
                    "the key must be sent exactly once, or the server keeps " +
                    "it as a repeated form key and every reader sees it as " +
                    "absent. Body was: " + recorder.At(0));
                StringAssert.Contains(recorder.At(0), "id.usage=standard");
            }
            finally
            {
                QuitDriver(driver);
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// Test 4, the framework half. A string in the script URL and a
        /// platform delivering one goes once.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task AFrameworkStringInTheUrlAndFromThePlatformIsSentOnce()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            var recorder = new Recorder();

            using var pipeline = BuildPipeline(port,
                new SnippetElement(_loggerFactory),
                new StubDidElement(_loggerFactory));
            var app = BuildApp(pipeline, url, recorder,
                page => Page("duplicate string", TcfStub,
                    query: "tcstring=CPfromTheUrl"));

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "the first round", recorder);

                js.ExecuteScript("window.__tcfDeliver('CPfromThePlatform')");
                WaitForPostCount(recorder, 2, "the request after the answer");

                Assert.AreEqual(1, KeyCount(recorder.At(1), "tcstring"),
                    "the key must be sent exactly once. Body was: " +
                    recorder.At(1));
                StringAssert.Contains(recorder.At(1), "tcstring=CPfromThePlatform",
                    "the platform's own delivery replaces what the URL " +
                    "carried, because one key holds one value");
            }
            finally
            {
                QuitDriver(driver);
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// Test 14. Standard and then the alternative in one page view. A
        /// delivery of (null, false) is the framework's view of a usage
        /// granting no purposes, and on its own it is never read as an
        /// answer.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task StandardThenTheAlternativeInOnePageView()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            var recorder = new Recorder();

            using var pipeline = BuildPipeline(port,
                new SnippetElement(_loggerFactory),
                new StubDidElement(_loggerFactory));
            var app = BuildApp(pipeline, url, recorder,
                page => Page("change of mind", PmpStub + TcfStub));

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "the first round", recorder);
                AssertNoAnswerKeys(recorder.At(0), "the first request");

                js.ExecuteScript("window.__tcfDeliver('CPfirstAnswer')");
                WaitForPostCount(recorder, 2, "the framework answer");
                StringAssert.Contains(recorder.At(1), "tcstring=CPfirstAnswer");

                // The alternative closes the dialog, and the framework
                // surface answers (null, false) because the usage grants no
                // purposes. On its own that is not an answer.
                js.ExecuteScript("window.__tcfClear()");
                WaitForPostCount(recorder, 3, "the cleared framework string");
                AssertNoAnswerKeys(recorder.At(2),
                    "a delivery of (null, false) on its own");

                // The platform then announces the real answer.
                js.ExecuteScript("window.__51d_pmp_answer('non-marketing')");
                WaitForPostCount(recorder, 4, "the alternative answer");
                StringAssert.Contains(recorder.At(3), "id.usage=non-marketing",
                    "the alternative is an answer under the Model Terms and " +
                    "creates an identifier like the other two");
                WaitForScript(js,
                    "return !!(fod.fodid && fod.fodid.idprobglobal)",
                    "the identifier for the alternative", recorder);
            }
            finally
            {
                QuitDriver(driver);
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// Test 25. A framework stub whose registration throws, and one
        /// that never calls back, each say so once, print no value and do
        /// not stop the first request.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task AFrameworkStubThatThrowsIsNamedOnceAndStopsNothing()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            var recorder = new Recorder();

            using var pipeline = BuildPipeline(port,
                new SnippetElement(_loggerFactory),
                new StubDidElement(_loggerFactory));
            const string throwingStub = @"
      window.__tcfapi = function () { throw new Error('stub failure'); };
";
            var app = BuildApp(pipeline, url, recorder,
                page => Page("throwing stub", throwingStub));

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "the page with a throwing stub", recorder);

                Assert.AreEqual(1, recorder.Count,
                    "the first request must still go. " + recorder.Describe());
                AssertNoAnswerKeys(recorder.At(0),
                    "a page whose platform stub throws");

                var named = GetConsoleMessages(driver)
                    .Where(m => m.Contains("__tcfapi('addEventListener') threw",
                        StringComparison.Ordinal))
                    .ToList();
                Assert.AreEqual(1, named.Count,
                    "the call that threw must be named once, saw: " +
                    string.Join(" | ", named));
                Assert.IsFalse(
                    named[0].Contains("stub failure", StringComparison.Ordinal),
                    "no value from the platform may be printed, message was: " +
                    named[0]);
            }
            finally
            {
                QuitDriver(driver);
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// Test 25, the second half. A stub that takes the registration and
        /// never calls back says so once, after the ten seconds the
        /// template waits before logging, and that log is not a wait,
        /// because the first request has already gone.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task AFrameworkStubThatNeverCallsBackIsNamedOnce()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            var recorder = new Recorder();

            using var pipeline = BuildPipeline(port,
                new SnippetElement(_loggerFactory),
                new StubDidElement(_loggerFactory));
            var app = BuildApp(pipeline, url, recorder,
                page => Page("silent stub", TcfStub));

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "the page with a silent stub", recorder);

                Assert.AreEqual(1, recorder.Count,
                    "the first request goes before any of this. " +
                    recorder.Describe());

                // The template logs after ten seconds. The wait here is for
                // that log and not for the request, which has already been
                // observed above.
                var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(40);
                List<string> named;
                while (true)
                {
                    named = GetConsoleMessages(driver)
                        .Where(m => m.Contains(
                            "has not called back within ten seconds",
                            StringComparison.Ordinal))
                        .ToList();
                    if (named.Count > 0 || DateTime.UtcNow >= deadline)
                    {
                        break;
                    }
                    Thread.Sleep(500);
                }

                Assert.AreEqual(1, named.Count,
                    "a stub that never calls back must be named once, saw: " +
                    string.Join(" | ", named));
                Assert.AreEqual(1, recorder.Count,
                    "and nothing further must have been sent. " +
                    recorder.Describe());
            }
            finally
            {
                QuitDriver(driver);
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// Test 10. The hardware profile snippet is the one the template
        /// special cases, and an element whose only snippet is that one is
        /// what proves a page with an answer still gets its request when
        /// the snippet saves nothing.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task TheHardwareProfileSnippetFindingNothingStillSends()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            var recorder = new Recorder();

            using var pipeline = BuildPipeline(port,
                new HardwareProfileOnlyElement(_loggerFactory),
                new StubDidElement(_loggerFactory));
            var app = BuildApp(pipeline, url, recorder,
                page => Page("hardware profile",
                    PmpStub + "\n      window.__51d_pmp_value = 'standard';"));

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "the hardware profile page", recorder);

                Assert.IsTrue(
                    Convert.ToInt64(
                        js.ExecuteScript("return window.hardwareProfileRuns || 0"),
                        System.Globalization.CultureInfo.InvariantCulture) >= 1,
                    "the snippet must actually have run, otherwise the case " +
                    "this test is for was never reached");
                Assert.IsTrue(recorder.Count >= 1,
                    "a request must still be produced, because the visitor " +
                    "answered and the identifier is created from a request " +
                    "that carries the answer. " + recorder.Describe());
                StringAssert.Contains(recorder.At(0), "id.usage=standard",
                    "and it carries the answer");
            }
            finally
            {
                QuitDriver(driver);
                await app.DisposeAsync();
            }
        }
        /// <summary>
        /// The general form of test 10. An answer known when the script
        /// constructs has to reach the server on that page view, whatever
        /// the resource key's properties happen to need, because a page
        /// view that ends without sending it creates no identifier at all.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task AnAnswerKnownAtConstructionWithNoSnippetIsStillSent()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            var recorder = new Recorder();

            // A pipeline whose properties need no snippet at all, which is
            // an ordinary resource key and not a corner.
            using var pipeline = BuildPipeline(port,
                new BrowserCapabilityElement(_loggerFactory),
                new StubDidElement(_loggerFactory));
            var app = BuildApp(pipeline, url, recorder,
                page => Page("no snippet",
                    PmpStub + "window.__51d_pmp_value = 'standard';"));

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "the page with no snippet", recorder);

                Assert.IsTrue(recorder.Count >= 1,
                    "the visitor has answered and the page is entitled, so " +
                    "the answer has to reach the server on this page view. " +
                    recorder.Describe());
                StringAssert.Contains(recorder.At(0), "id.usage=standard");
                WaitForScript(js,
                    "return !!(fod.fodid && fod.fodid.idprobglobal)",
                    "the identifier arriving", recorder);
            }
            finally
            {
                QuitDriver(driver);
                await app.DisposeAsync();
            }
        }
    }
}
