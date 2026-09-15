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
    /// A stored answer may be reused only for the same inputs, and no
    /// answer yet on a page where a platform is present means unknown
    /// rather than different. Tests 2 and 17 of pipeline-dotnet#414.
    /// </summary>
    public partial class UserPromptBrowserTests
    {
        /// <summary>
        /// Test 2. Each input that influences the answer, moved on its own
        /// between two page views, produces a fresh request carrying page
        /// two's value and none of page one's.
        /// </summary>
        /// <param name="what">
        /// Which input moves, which is also what the failure says.
        /// </param>
        /// <param name="one">The value page one uses.</param>
        /// <param name="two">The value page two uses.</param>
        [TestMethod]
        [Timeout(300_000)]
        [DataRow("a rendered parameter", "one", "two")]
        [DataRow("the evidence object", "one", "two")]
        [DataRow("a usage in the evidence object", "standard", "personalized")]
        [DataRow("a consent string", "CPone", "CPtwo")]
        [DataRow("a salt", "saltone", "salttwo")]
        public async Task OneInputMovingOnItsOwnProducesAFreshRequest(
            string what, string one, string two)
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            var recorder = new Recorder();

            // The key the value travels under, and whether it rides in the
            // script URL or in the evidence object.
            var inUrl = what == "a rendered parameter";
            var key = what switch
            {
                "a rendered parameter" => "mark",
                "the evidence object" => "query.mark",
                "a usage in the evidence object" => "id.usage",
                "a consent string" => "tcstring",
                _ => "id.salt",
            };

            string PageFor(string value) => inUrl
                ? Page("input page", query: $"{key}={value}")
                : Page("input page",
                    $"window.fodEvidence = {{ '{key}': '{value}' }};");

            using var pipeline = BuildPipeline(port,
                new SnippetElement(_loggerFactory),
                new StubDidElement(_loggerFactory));
            var app = BuildApp(pipeline, url, recorder,
                page => page == "page1" ? PageFor(one) : PageFor(two));

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, $"page one with {what}", recorder);
                var afterPageOne = recorder.Count;
                Assert.IsTrue(afterPageOne >= 1,
                    "page one must reach the endpoint at least once. " +
                    recorder.Describe());
                CollectionAssert.Contains(GetSessionStorageKeys(js), "fod",
                    "page one must leave a cached response behind, " +
                    "otherwise page two has nothing to invalidate");

                // Page two's request is held open, so the flags can be
                // read at the moment it is made rather than after the
                // response has written them again.
                recorder.Gate = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                driver.Navigate().GoToUrl(url + "page2");
                WaitForPostCount(recorder, afterPageOne + 1,
                    $"page two's request with {what} moved");

                var keysWhileInFlight = GetSessionStorageKeys(js);
                Assert.AreEqual(0,
                    keysWhileInFlight.Count(k => k.StartsWith(
                        "fod_property_", StringComparison.Ordinal)),
                    "the flags retire the snippets, so the request that " +
                    "goes has to be made with them gone. Keys were: " +
                    string.Join(", ", keysWhileInFlight));

                recorder.Gate.SetResult(true);
                recorder.Gate = null;
                WaitForFodDone(js, $"page two with {what}", recorder);

                var body = recorder.At(afterPageOne);
                StringAssert.Contains(body, $"{key}={two}",
                    $"page two's request must carry its own {what}");
                Assert.AreEqual(0, KeyCount(body, $"{key}={one}"),
                    $"and none of page one's {what}, body was: {body}");
                StringAssert.Contains(body, "51D_testvalue=purple",
                    "the snippets ran again for the fresh request, so their " +
                    "values are in it");
            }
            finally
            {
                recorder.Gate?.TrySetResult(true);
                QuitDriver(driver);
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// Test 17. A stored record carrying an answer, a page view that
        /// has none yet and a platform that has not delivered is served
        /// from the cache, because no answer yet means unknown rather than
        /// different. A later delivery of the same answer asks for nothing
        /// and a different one asks once.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        [DataRow("CPsameAnswer", false)]
        [DataRow("CPdifferentAnswer", true)]
        public async Task NoAnswerYetWithAPlatformPresentIsServedFromTheCache(
            string secondAnswer, bool expectARequest)
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            var recorder = new Recorder();

            using var pipeline = BuildPipeline(port,
                new SnippetElement(_loggerFactory),
                new StubDidElement(_loggerFactory));
            var app = BuildApp(pipeline, url, recorder,
                page => Page("platform page", TcfStub));

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                // Page one answers, so the record of what was sent carries
                // the answer.
                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "page one", recorder);
                js.ExecuteScript("window.__tcfDeliver('CPsameAnswer')");
                WaitForPostCount(recorder, 2, "page one's answered request");
                WaitForScript(js,
                    "return !!(fod.fodid && fod.fodid.idprobglobal)",
                    "page one's identifier", recorder);
                var afterPageOne = recorder.Count;

                // Page two's platform has not delivered when the script
                // constructs. The stored answer stands in for the missing
                // one, so the entry is served and nothing is asked for.
                driver.Navigate().GoToUrl(url + "page2");
                WaitForFodDone(js, "page two, served from the cache", recorder);
                Assert.AreEqual(afterPageOne, recorder.Count,
                    "no answer yet on a page with a platform means unknown " +
                    "rather than different, so the entry must be served. " +
                    recorder.Describe());
                Assert.AreEqual("purple",
                    (string)js.ExecuteScript("return window.fodValue"),
                    "and the page still sees its values");

                // The platform then delivers.
                js.ExecuteScript($"window.__tcfDeliver('{secondAnswer}')");
                if (expectARequest)
                {
                    WaitForPostCount(recorder, afterPageOne + 1,
                        "the request for the different answer");
                    StringAssert.Contains(recorder.At(afterPageOne),
                        $"tcstring={secondAnswer}",
                        "a different answer must produce a fresh request");
                    Assert.AreEqual(afterPageOne + 1, recorder.Count,
                        "and exactly one. " + recorder.Describe());
                }
                else
                {
                    // A positive observation that nothing was sent: the
                    // delivery has been taken, which the page shows by
                    // still holding its values, and the count has not
                    // moved.
                    WaitForScript(js,
                        "return window.__tcfListener !== null",
                        "the listener still being registered", recorder);
                    Assert.AreEqual(afterPageOne, recorder.Count,
                        "the same answer changes nothing, so there is " +
                        "nothing to ask for. " + recorder.Describe());
                }
            }
            finally
            {
                QuitDriver(driver);
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// Test 17's last sentence. The same pair of page views with no
        /// platform on the second page reads the missing answer literally,
        /// so the entry is cleared and a request goes.
        /// </summary>
        [TestMethod]
        [Timeout(300_000)]
        public async Task NoPlatformAtAllOnTheSecondPageClearsAndAsksAgain()
        {
            var port = TestHttpListener.GetRandomUnusedPort();
            var url = $"http://localhost:{port}/";
            var recorder = new Recorder();

            using var pipeline = BuildPipeline(port,
                new SnippetElement(_loggerFactory),
                new StubDidElement(_loggerFactory));
            var app = BuildApp(pipeline, url, recorder,
                page => page == "page1"
                    ? Page("page one", TcfStub)
                    : Page("page two with no platform"));

            ChromeDriver driver = null;
            try
            {
                await app.StartAsync();
                driver = CreateDriver();
                IJavaScriptExecutor js = driver;

                driver.Navigate().GoToUrl(url + "page1");
                WaitForFodDone(js, "page one", recorder);
                js.ExecuteScript("window.__tcfDeliver('CPanswered')");
                WaitForPostCount(recorder, 2, "page one's answered request");
                var afterPageOne = recorder.Count;

                driver.Navigate().GoToUrl(url + "page2");
                WaitForFodDone(js, "page two with no platform", recorder);

                Assert.IsTrue(recorder.Count > afterPageOne,
                    "with no platform present the missing answer is read " +
                    "literally, so the entry is cleared and a request goes. " +
                    recorder.Describe());
                var body = recorder.At(recorder.Count - 1);
                Assert.AreEqual(0, KeyCount(body, "tcstring"),
                    "and it carries no answer, body was: " + body);
            }
            finally
            {
                QuitDriver(driver);
                await app.DisposeAsync();
            }
        }
    }
}
