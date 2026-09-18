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
using FiftyOne.Pipeline.Core.TypedMap;
using FiftyOne.Pipeline.JavaScriptBuilder.Data;
using FiftyOne.Pipeline.JavaScriptBuilder.FlowElement;
using FiftyOne.Pipeline.JavaScriptBuilder.TemplateData;
using FiftyOne.Pipeline.JsonBuilder.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Diagnostics;

namespace FiftyOne.Pipeline.JavaScript.Tests
{
    /// <summary>
    /// Checks that the session id and the sequence reach the client script
    /// only in a form that cannot break it. The session id is written
    /// inside quotes and the sequence as a number, both as they are, so a
    /// session id that is not made of ASCII letters, digits and hyphens (1
    /// to 64 of them) is written as an empty string, and a sequence that
    /// is not a positive 32 bit integer is written as 1.
    /// </summary>
    /// <remarks>
    /// No browser is used. The element is given the values as evidence
    /// with no Sequence Element in the pipeline, which is what reaches the
    /// script when a page sends them itself. The rendered script is
    /// checked with <c>node --check</c>, so Node must be on the path, and
    /// each test is inconclusive when it is not.
    /// </remarks>
    [TestClass]
    public class SessionIdSequenceTests
    {
        private const string SESSION_ID_KEY =
            Engines.FiftyOne.Constants.EVIDENCE_SESSIONID;

        private const string SEQUENCE_KEY =
            Engines.FiftyOne.Constants.EVIDENCE_SEQUENCE;

        private TestLoggerFactory _loggerFactory;

        [TestInitialize]
        public void Init()
        {
            _loggerFactory = new TestLoggerFactory();
        }

        /// <summary>
        /// A session id made of letters, digits and hyphens is written into
        /// the script as it is, up to 64 characters.
        /// </summary>
        [DataTestMethod]
        [DataRow("abc-123")]
        [DataRow("f47ac10b-58cc-4372-a567-0e02b2c3d479")]
        [DataRow("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
        public void SessionId_Valid_IsRendered(string sessionId)
        {
            var script = Render(sessionId, null);

            AssertSessionId(script, sessionId);
            AssertParses(script);
        }

        /// <summary>
        /// A session id that is not made of 1 to 64 ASCII letters, digits
        /// and hyphens is written as an empty string, and the script still
        /// parses.
        /// </summary>
        [DataTestMethod]
        [DataRow("a\"b")]
        [DataRow("a\\b")]
        [DataRow("</script>")]
        [DataRow("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
        [DataRow("caf\u00e9")]
        [DataRow("a\u2028b")]
        [DataRow("abc\n")]
        [DataRow("abc 123")]
        [DataRow("")]
        public void SessionId_Invalid_IsEmpty(string sessionId)
        {
            var script = Render(sessionId, null);

            AssertSessionId(script, string.Empty);
            if (sessionId.Length > 0)
            {
                Assert.DoesNotContain(sessionId, script,
                    "The session id was written into the script.");
            }
            AssertParses(script);
        }

        /// <summary>
        /// With no session id and no sequence in the evidence, which is
        /// the case when there is no Sequence Element, the session id is
        /// empty and the sequence is 1.
        /// </summary>
        [TestMethod]
        public void SessionIdAndSequence_Absent_EmptyAndOne()
        {
            var script = Render(null, null);

            AssertSessionId(script, string.Empty);
            AssertSequence(script, 1);
            AssertParses(script);
        }

        /// <summary>
        /// A sequence that is a positive 32 bit integer is written as it
        /// is, whether the evidence holds a string or an integer.
        /// </summary>
        [DataTestMethod]
        [DataRow("1", 1)]
        [DataRow("5", 5)]
        [DataRow("2147483647", int.MaxValue)]
        [DataRow(7, 7)]
        public void Sequence_Valid_IsRendered(object sequence, int expected)
        {
            var script = Render(null, sequence);

            AssertSequence(script, expected);
            AssertParses(script);
        }

        /// <summary>
        /// A sequence that is not a positive 32 bit integer is written as
        /// 1, and the script still parses.
        /// </summary>
        [DataTestMethod]
        [DataRow("abc")]
        [DataRow("-1")]
        [DataRow("0")]
        [DataRow("99999999999")]
        [DataRow("2147483648")]
        [DataRow("")]
        [DataRow(" 5")]
        [DataRow("+5")]
        [DataRow(-1)]
        [DataRow(0)]
        [DataRow(int.MinValue)]
        public void Sequence_Invalid_IsOne(object sequence)
        {
            var script = Render(null, sequence);

            AssertSequence(script, 1);
            AssertParses(script);
        }

        /// <summary>
        /// A subclass that passes its own values to BuildJavaScript, as the
        /// cloud service's builder does, cannot write an unsafe session id
        /// or sequence either, because the template data applies the same
        /// rule.
        /// </summary>
        [DataTestMethod]
        [DataRow("a\"b", 0, "", 1)]
        [DataRow("</script>", -5, "", 1)]
        [DataRow(null, int.MinValue, "", 1)]
        [DataRow("abc-123", 3, "abc-123", 3)]
        public void TemplateData_AppliesRule(
            string sessionId,
            int sequence,
            string expectedSessionId,
            int expectedSequence)
        {
            var viaString = new JavaScriptResource(
                "fod", "{}", sessionId, sequence, false, false,
                "https://localhost/json", "{}", false, true, false)
                .AsDictionary();
            var viaUri = new JavaScriptResource(
                "fod", "{}", sessionId, sequence, false, false,
                new Uri("https://localhost/json"), "{}", false, true, false)
                .AsDictionary();

            foreach (var values in new[] { viaString, viaUri })
            {
                Assert.AreEqual(expectedSessionId, values["_sessionId"]);
                Assert.AreEqual(expectedSequence, values["_sequence"]);
            }
        }

        /// <summary>
        /// The rule itself.
        /// </summary>
        [DataTestMethod]
        [DataRow("abc-123", true)]
        [DataRow("A", true)]
        [DataRow("-", true)]
        [DataRow("", false)]
        [DataRow(null, false)]
        [DataRow("a_b", false)]
        [DataRow("a.b", false)]
        [DataRow("abc\n", false)]
        [DataRow("\u0661", false)]
        public void IsValidSessionId(string sessionId, bool expected)
        {
            Assert.AreEqual(
                expected,
                JavaScriptResource.IsValidSessionId(sessionId));
        }

        private static void AssertSessionId(string script, string expected)
        {
            Assert.Contains(
                $"var sessionId = \"{expected}\";",
                script,
                "The session id is not rendered as expected.");
        }

        private static void AssertSequence(string script, int expected)
        {
            Assert.Contains(
                $"var sequence = {expected};",
                script,
                "The sequence is not rendered as expected.");
        }

        /// <summary>
        /// Fails unless <c>node --check</c> accepts the script.
        /// </summary>
        private static void AssertParses(string script)
        {
            var path = Path.Combine(
                Path.GetTempPath(),
                $"sessid-script-{Guid.NewGuid():N}.js");
            File.WriteAllText(path, script);
            try
            {
                var start = new ProcessStartInfo("node")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
                start.ArgumentList.Add("--check");
                start.ArgumentList.Add(path);
                Process process;
                try
                {
                    process = Process.Start(start);
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    Assert.Inconclusive("Node is not on the path.");
                    return;
                }
                using (process)
                {
                    process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    Assert.AreEqual(0, process.ExitCode,
                        $"The script does not parse. {error}");
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        private delegate bool TryGetDelegate(string key, out object value);

        /// <summary>
        /// Processes a mocked request, with no Sequence Element, carrying
        /// the session id and sequence evidence given (left out when null),
        /// and returns the rendered script, which is not minified so that
        /// the values can be found in it.
        /// </summary>
        private string Render(string sessionId, object sequence)
        {
            var element = new JavaScriptBuilderElementBuilder(_loggerFactory)
                .SetEndpoint("/json")
                .SetMinify(false)
                .Build();

            var evidence = new Dictionary<string, object>
            {
                [JavaScriptBuilder.Constants.EVIDENCE_HOST_KEY] = "localhost",
            };
            if (sessionId != null)
            {
                evidence[SESSION_ID_KEY] = sessionId;
            }
            if (sequence != null)
            {
                evidence[SEQUENCE_KEY] = sequence;
            }

            var flowData = new Mock<IFlowData>();
            flowData.Setup(d => d.TryGetEvidence(
                    It.IsAny<string>(), out It.Ref<object>.IsAny))
                .Returns(new TryGetDelegate((string key, out object value) =>
                    evidence.TryGetValue(key, out value)));
            flowData.Setup(d => d.Get<IJsonBuilderElementData>())
                .Returns(() => new JsonBuilderElementData(
                    new Mock<ILogger<JsonBuilderElementData>>().Object,
                    null)
                {
                    Json = "{\"device\":{\"ismobile\":true}}"
                });
            flowData.Setup(d => d.GetEvidence().AsDictionary())
                .Returns(evidence);

            IJavaScriptBuilderElementData data = null;
            flowData.Setup(d => d.GetOrAdd(
                    It.IsAny<ITypedKey<IJavaScriptBuilderElementData>>(),
                    It.IsAny<Func<IPipeline, IJavaScriptBuilderElementData>>()))
                .Returns<ITypedKey<IJavaScriptBuilderElementData>,
                    Func<IPipeline, IJavaScriptBuilderElementData>>((k, f) =>
                    {
                        data = f(null);
                        return data;
                    });

            element.Process(flowData.Object);

            Assert.IsNotNull(data, "No script was rendered.");
            return data.JavaScript;
        }
    }
}
