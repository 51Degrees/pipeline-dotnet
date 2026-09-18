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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FiftyOne.Did.Examples.Tests
{
    /// <summary>
    /// What the creator context example page does in a browser, checked
    /// by running its script in Node against a small stand in for a
    /// browser (CreatorContextPageHarness.js). No browser is started and
    /// nothing here reaches a network.
    /// </summary>
    /// <remarks>
    /// The service creates a 51Did only once the page has run the
    /// snippets it asks for and sent what they collected, so a page that
    /// asks for one directly is told the page has not finished and is
    /// given nothing. The 51Degrees client script is what runs those
    /// snippets, so the page has to create through the script and then
    /// send what the snippets collected with its verification call as
    /// well. These tests pin both, because neither can be seen from the
    /// server side of the example and neither shows up in a unit test of
    /// the redeem route. Node is needed to run them, and every GitHub
    /// hosted runner has it. Where it is absent the tests say so rather
    /// than passing on nothing.
    /// </remarks>
    [TestClass]
    public class CreatorContextPageTests
    {
        /// <summary>
        /// The licensed probabilistic identifier the stand in client
        /// script reports, once the page has made it safe for a URL.
        /// </summary>
        private const string CreatedUrlSafe = "prob-lic_value";

        /// <summary>
        /// The page and the harness are both copied beside the test
        /// assembly, the page by the example project it references.
        /// </summary>
        private static string Beside(string name) =>
            Path.Combine(AppContext.BaseDirectory, name);

        /// <summary>
        /// Runs a program and returns its standard output, failing the
        /// test with everything it printed when it does not succeed.
        /// </summary>
        private static string Run(string program, params string[] arguments)
        {
            var start = new ProcessStartInfo(program)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }
            Process? process;
            try
            {
                process = Process.Start(start);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Node is what runs the page's script, so without it
                // there is nothing to check and nothing is proved. Said
                // out loud rather than passed over in silence.
                Assert.Inconclusive(
                    $"{program} is not on the path, so the page was not run");
                throw;
            }
            Assert.IsNotNull(process);
            using var owned = process;
            var output = process!.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            Assert.IsTrue(
                process.WaitForExit(120000),
                $"{program} did not finish");
            Assert.AreEqual(
                0,
                process.ExitCode,
                $"{program} failed: {output}{error}");
            return output;
        }

        /// <summary>
        /// Runs the page's script and returns what the harness recorded.
        /// </summary>
        /// <param name="given">
        /// The identifier the page is opened with, which is the
        /// transplant path. Null creates one instead.
        /// </param>
        private static JsonElement RunPage(string? given = null)
        {
            var arguments = new List<string>
            {
                Beside("CreatorContextPageHarness.js"),
                Beside("page.html"),
            };
            if (given != null)
            {
                arguments.Add(given);
            }
            var printed = Run("node", arguments.ToArray())
                .Trim()
                .Split('\n')
                .Last()
                .Trim();
            return JsonDocument.Parse(printed).RootElement.Clone();
        }

        private static List<string> Strings(JsonElement record, string name)
        {
            return record.GetProperty(name)
                .EnumerateArray()
                .Select(value => value.GetString() ?? string.Empty)
                .ToList();
        }

        private static string Row(JsonElement record, string name)
        {
            return record.GetProperty("rows").GetProperty(name).GetString()
                ?? string.Empty;
        }

        private static string PageText() =>
            File.ReadAllText(Beside("page.html"));

        /// <summary>
        /// A page whose script does not parse defines nothing and reports
        /// nothing, and the browser says so only in its console. The
        /// <c>node --check</c> reader takes JavaScript rather than HTML,
        /// so the page's script block is written out on its own.
        /// </summary>
        [TestMethod]
        public void ThePageScriptParses()
        {
            // A checkout on Windows has carriage returns in it, so the
            // ends of lines are matched either way.
            var block = Regex.Match(
                PageText(),
                "<script>\r?\n(.*?)\r?\n</script>",
                RegexOptions.Singleline);
            Assert.IsTrue(block.Success, "the page has a script block");
            var file = Path.Combine(
                Path.GetTempPath(), Path.GetRandomFileName() + ".js");
            File.WriteAllText(file, block.Groups[1].Value);
            try
            {
                Run("node", "--check", file);
            }
            finally
            {
                File.Delete(file);
            }
            Assert.AreEqual(0, Strings(RunPage(), "errors").Count);
        }

        /// <summary>
        /// The page asks the cloud for the client script, with the usage
        /// and the email address on its address, and takes the identifier
        /// from what the script reports.
        /// </summary>
        [TestMethod]
        public void TheIdentifierComesFromTheClientScript()
        {
            var record = RunPage();
            var scripts = Strings(record, "scripts");
            Assert.AreEqual(
                1, scripts.Count, "the client script is loaded exactly once");
            StringAssert.Contains(scripts[0], "TEST-RESOURCE-KEY.js");
            StringAssert.Contains(scripts[0], "id.usage=non-marketing");
            StringAssert.Contains(scripts[0], "id.email=");
            Assert.AreEqual(
                "created for this browser", Row(record, "s-create"));
        }

        /// <summary>
        /// A request of the page's own would be made before the snippets
        /// had run, and the service would answer it with no identifier at
        /// all.
        /// </summary>
        [TestMethod]
        public void ThePageDoesNotAskForAnIdentifierItself()
        {
            foreach (var address in Strings(RunPage(), "fetches"))
            {
                Assert.IsFalse(
                    address.Contains("json?resource="),
                    $"the page asked for an identifier itself: {address}");
                Assert.IsFalse(
                    address.Contains("/json"),
                    $"the page asked for an identifier itself: {address}");
            }
        }

        /// <summary>
        /// The identifier the script reported is verified, made safe for
        /// a URL, and what the snippets collected goes with it, because
        /// the service compares this browser against the creator from
        /// those values.
        /// </summary>
        [TestMethod]
        public void TheVerificationCarriesTheIdentifierAndTheSnippets()
        {
            var verify = Strings(RunPage(), "fetches")
                .Where(address => address.Contains("id/verify-full"))
                .ToList();
            Assert.AreEqual(1, verify.Count);
            StringAssert.Contains(verify[0], CreatedUrlSafe);
            Assert.IsFalse(
                verify[0].Split('?')[0].Contains('+'),
                "the identifier is made safe for a URL");
            StringAssert.Contains(verify[0], "51D_ScreenPixelsHeight=1080");
            StringAssert.Contains(verify[0], "51D_ProfileIds=1-2-3");
            Assert.IsFalse(
                verify[0].Contains("unrelated=ignored"),
                "only the snippet values are forwarded");
        }

        /// <summary>
        /// The licence key lives on the server, so the sealed result goes
        /// there and the verdict comes back from there.
        /// </summary>
        [TestMethod]
        public void TheResultIsRedeemedOnThePagesOwnServer()
        {
            var record = RunPage();
            var redeem = Strings(record, "fetches")
                .Where(address => address.StartsWith("/redeem?"))
                .ToList();
            Assert.AreEqual(1, redeem.Count);
            StringAssert.Contains(redeem[0], "result=sealed-result");
            Assert.AreEqual("verified", Row(record, "s-signature"));
            Assert.AreEqual("verified", Row(record, "s-context"));
        }

        /// <summary>
        /// The page opened with an identifier from another browser checks
        /// that identifier, and still needs this browser's snippet values
        /// for the comparison, so the script runs on that path too.
        /// </summary>
        [TestMethod]
        public void ATransplantedIdentifierStillRunsTheClientScript()
        {
            var record = RunPage("given-value");
            Assert.AreEqual(1, Strings(record, "scripts").Count);
            var verify = Strings(record, "fetches")
                .Where(address => address.Contains("id/verify-full"))
                .ToList();
            Assert.AreEqual(1, verify.Count);
            StringAssert.Contains(verify[0], "given-value");
            StringAssert.Contains(verify[0], "51D_ProfileIds=1-2-3");
        }

        /// <summary>
        /// Someone running the example is interested in the subject, so
        /// the page ends with somewhere to go next. The links to
        /// 51degrees.com carry the campaign tags the link lint asks for
        /// and the source repositories are given as plain addresses.
        /// </summary>
        [TestMethod]
        public void ThePageEndsWithFindOutMore()
        {
            var page = PageText();
            StringAssert.Contains(page, "Find out more");
            StringAssert.Contains(page, "utm_campaign=pipeline-dotnet");
            StringAssert.Contains(
                page, "https://github.com/51Degrees/pipeline-dotnet");
        }
    }
}
