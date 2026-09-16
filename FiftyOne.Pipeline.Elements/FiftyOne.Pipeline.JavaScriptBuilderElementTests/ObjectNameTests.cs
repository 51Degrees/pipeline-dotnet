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
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Core.TypedMap;
using FiftyOne.Pipeline.JavaScriptBuilder.Data;
using FiftyOne.Pipeline.JavaScriptBuilder.FlowElement;
using FiftyOne.Pipeline.JsonBuilder.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;

namespace FiftyOne.Pipeline.JavaScript.Tests
{
    /// <summary>
    /// Checks that the name of the object the client script creates can be
    /// changed, from configuration and from the request, and that a name
    /// which is not a valid JavaScript identifier never reaches the script.
    /// </summary>
    /// <remarks>
    /// No browser is used. The rendered script is parsed and run by Node in
    /// a small fake window, document and session storage, so Node must be
    /// on the path. Each test is inconclusive when it is not.
    /// </remarks>
    [TestClass]
    public class ObjectNameTests
    {
        private const string CUSTOM_NAME = "myFod";

        private const string DEFAULT_NAME =
            JavaScriptBuilder.Constants.BUILDER_DEFAULT_OBJECT_NAME;

        /// <summary>
        /// Runs the script given as the first argument in a fake browser
        /// and prints what it finds on the object named by the second
        /// argument as one line of JSON. The script is compiled before it
        /// runs, so a script that does not parse is reported as such.
        /// </summary>
        private const string NODE_HARNESS = @"
const fs = require('fs');
const vm = require('vm');
const source = fs.readFileSync(process.argv[2], 'utf8');
const name = process.argv[3];
const result = { parses: false, ran: false };
let script;
try {
    script = new vm.Script(source, { filename: 'script.js' });
    result.parses = true;
} catch (e) {
    result.parseError = String(e.message);
}
function makeStorage() {
    const data = {};
    const methods = {
        getItem: k => Object.prototype.hasOwnProperty.call(data, k)
            ? data[k] : null,
        setItem: (k, v) => { data[String(k)] = String(v); },
        removeItem: k => { delete data[k]; },
        key: i => Object.keys(data)[i] || null,
        clear: () => Object.keys(data).forEach(k => delete data[k])
    };
    const storage = new Proxy(data, {
        get(t, p) {
            if (p === 'length') { return Object.keys(data).length; }
            if (Object.prototype.hasOwnProperty.call(methods, p)) {
                return methods[p];
            }
            return data[p];
        },
        set(t, p, v) { data[String(p)] = String(v); return true; }
    });
    return { data: data, storage: storage };
}
const session = makeStorage();
const local = makeStorage();
const quiet = function () {};
const sandbox = {
    console: { log: quiet, warn: quiet, error: quiet, info: quiet },
    sessionStorage: session.storage,
    localStorage: local.storage,
    document: { cookie: '' },
    navigator: {},
    setTimeout: setTimeout,
    clearTimeout: clearTimeout,
    addEventListener: quiet,
    removeEventListener: quiet,
    fetch: () => new Promise(() => {})
};
sandbox.window = sandbox;
if (script) {
    try {
        script.runInContext(vm.createContext(sandbox));
        result.ran = true;
    } catch (e) {
        result.runError = String(e.message);
    }
}
const obj = sandbox[name];
result.exists = typeof obj === 'object' && obj !== null;
if (result.exists) {
    result.complete = typeof obj.complete;
    result.onChange = typeof obj.onChange;
    result.refresh = typeof obj.refresh;
    result.ismobile = obj.device ? obj.device.ismobile : null;
}
result.storageKeys = Object.keys(session.data);
console.log(JSON.stringify(result));
";

        private static string _harnessPath;

        private TestLoggerFactory _loggerFactory;

        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            _harnessPath = Path.Combine(
                Path.GetTempPath(),
                $"objname-harness-{Guid.NewGuid():N}.js");
            File.WriteAllText(_harnessPath, NODE_HARNESS);
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            if (_harnessPath != null && File.Exists(_harnessPath))
            {
                File.Delete(_harnessPath);
            }
        }

        [TestInitialize]
        public void Init()
        {
            _loggerFactory = new TestLoggerFactory();
        }

        /// <summary>
        /// A name set in the builder's configuration is used everywhere the
        /// template writes the name, and the script runs and creates the
        /// object under that name.
        /// </summary>
        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ObjectName_FromConfiguration_Works(bool minify)
        {
            var element = new JavaScriptBuilderElementBuilder(_loggerFactory)
                .SetEndpoint("/json")
                .SetObjectName(CUSTOM_NAME)
                .SetMinify(minify)
                .Build();

            var script = Render(element, null);

            AssertUsesName(script, CUSTOM_NAME, DEFAULT_NAME);
            AssertRuns(script, CUSTOM_NAME);
        }

        /// <summary>
        /// A name given in the request's evidence is used everywhere the
        /// template writes the name, and the script runs and creates the
        /// object under that name.
        /// </summary>
        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ObjectName_FromEvidence_Works(bool minify)
        {
            var element = new JavaScriptBuilderElementBuilder(_loggerFactory)
                .SetEndpoint("/json")
                .SetMinify(minify)
                .Build();

            var script = Render(element, CUSTOM_NAME);

            AssertUsesName(script, CUSTOM_NAME, DEFAULT_NAME);
            AssertRuns(script, CUSTOM_NAME);
        }

        /// <summary>
        /// A name in the request that is not a valid JavaScript identifier
        /// is ignored. The configured default is used, a warning is logged,
        /// the script runs, and the requested text is not written into the
        /// script anywhere outside the query parameters the script sends
        /// back, which are encoded and held in a JSON string.
        /// </summary>
        [DataTestMethod]
        [DataRow("a;b")]
        [DataRow("a;b//")]
        [DataRow("9bad")]
        [DataRow("x\"y")]
        [DataRow("")]
        [DataRow(" fod")]
        [DataRow("fod\n")]
        [DataRow("var")]
        [DataRow("class")]
        [DataRow("undefined")]
        [DataRow("fiftyoneDegreesManager")]
        public void ObjectName_FromEvidence_Invalid_UsesDefault(
            string requested)
        {
            var element = new JavaScriptBuilderElementBuilder(_loggerFactory)
                .SetEndpoint("/json")
                .SetMinify(false)
                .Build();

            var script = Render(element, requested);

            AssertUsesName(script, DEFAULT_NAME, null);
            if (requested.Length > 0)
            {
                // The parameters are the only place the request may appear.
                var parameters = JsonConvert.SerializeObject(
                    new Dictionary<string, string>
                    {
                        {
                            JavaScriptBuilder.Constants
                                .EVIDENCE_OBJECT_NAME_SUFFIX,
                            WebUtility.UrlEncode(requested)
                        }
                    });
                Assert.Contains(parameters, script);
                var rest = script.Replace(parameters, string.Empty);
                Assert.DoesNotContain(
                    $"var {requested}", rest,
                    "The requested name was declared.");
                Assert.DoesNotContain(
                    $"sessionKey = \"{requested}\"", rest,
                    "The requested name was used as the storage key.");
                Assert.DoesNotContain(
                    $"{requested}Evidence", rest,
                    "The requested name was used for the evidence lookup.");
            }
            AssertRuns(script, DEFAULT_NAME);

            var warnings = _loggerFactory.Loggers
                .SelectMany(l => l.ExtendedEntries)
                .Where(e => e.LogLevel == LogLevel.Warning)
                .ToList();
            Assert.HasCount(1, warnings);
            Assert.Contains($"'{DEFAULT_NAME}'", warnings[0].Message);
        }

        /// <summary>
        /// A configured name that is not a valid JavaScript identifier is
        /// refused when the element is configured.
        /// </summary>
        [DataTestMethod]
        [DataRow("a;b")]
        [DataRow("9bad")]
        [DataRow("x\"y")]
        [DataRow("")]
        [DataRow("fod\n")]
        [DataRow("var")]
        [DataRow("NaN")]
        [DataRow("fiftyoneDegreesManager")]
        [DataRow(null)]
        public void ObjectName_FromConfiguration_Invalid_Refused(
            string configured)
        {
            Assert.ThrowsExactly<PipelineConfigurationException>(() =>
                new JavaScriptBuilderElementBuilder(_loggerFactory)
                    .SetObjectName(configured)
                    .Build());
        }

        /// <summary>
        /// A subclass that calls the element's constructor directly, rather
        /// than going through the builder, is refused in the same way.
        /// </summary>
        [DataTestMethod]
        [DataRow("a;b")]
        [DataRow("9bad")]
        [DataRow("")]
        [DataRow("this")]
        [DataRow("fiftyoneDegreesManager")]
        public void ObjectName_FromConstructor_Invalid_Refused(
            string configured)
        {
            Assert.ThrowsExactly<PipelineConfigurationException>(() =>
                new JavaScriptBuilderElement(
                    _loggerFactory.CreateLogger<JavaScriptBuilderElement>(),
                    null,
                    "/json",
                    configured,
                    false,
                    false));
        }

        /// <summary>
        /// The identifier check accepts what JavaScript accepts and refuses
        /// the rest.
        /// </summary>
        [DataTestMethod]
        [DataRow("fod", true)]
        [DataRow("myFod", true)]
        [DataRow("_x", true)]
        [DataRow("$x1", true)]
        [DataRow("letter", true)]
        [DataRow("a;b", false)]
        [DataRow("9bad", false)]
        [DataRow("", false)]
        [DataRow(null, false)]
        [DataRow("fod\n", false)]
        [DataRow("let", false)]
        [DataRow("null", false)]
        [DataRow("await", false)]
        [DataRow("Infinity", false)]
        [DataRow("NaN", false)]
        [DataRow("undefined", false)]
        [DataRow("fiftyoneDegreesManager", false)]
        [DataRow("eval", true)]
        [DataRow("arguments", true)]
        public void ObjectName_IsValidObjectName(string name, bool expected)
        {
            Assert.AreEqual(
                expected,
                JavaScriptBuilderElement.IsValidObjectName(name));
        }

        /// <summary>
        /// Checks every place the template writes the object name.
        /// </summary>
        private static void AssertUsesName(
            string script,
            string name,
            string absentName)
        {
            var escaped = Regex.Escape(name);
            Assert.IsTrue(
                Regex.IsMatch(script,
                    $@"\bvar\s+{escaped}\s*=\s*new\s+fiftyoneDegreesManager\b"),
                $"The script does not declare 'var {name}'.");
            Assert.Contains(
                $"\"{name}\"", script,
                "The session storage key does not use the name.");
            // The minifier turns window["xEvidence"] into window.xEvidence.
            Assert.IsTrue(
                Regex.IsMatch(script,
                    $@"window(\[""{escaped}Evidence""\]|\.{escaped}Evidence\b)"),
                "The evidence lookup does not use the name.");
            if (absentName != null)
            {
                Assert.IsFalse(
                    Regex.IsMatch(script,
                        $@"\bvar\s+{Regex.Escape(absentName)}\b"),
                    $"The script declares 'var {absentName}'.");
            }
        }

        /// <summary>
        /// Parses and runs the script in Node and checks the object.
        /// </summary>
        private static void AssertRuns(string script, string name)
        {
            var result = RunInNode(script, name);
            Assert.IsTrue(
                result.Value<bool>("parses"),
                $"The script does not parse. {result["parseError"]}");
            Assert.IsTrue(
                result.Value<bool>("ran"),
                $"The script threw. {result["runError"]}");
            Assert.IsTrue(
                result.Value<bool>("exists"),
                $"window.{name} does not exist.");
            Assert.AreEqual("function", result.Value<string>("complete"));
            Assert.AreEqual("function", result.Value<string>("onChange"));
            Assert.AreEqual("function", result.Value<string>("refresh"));
            Assert.IsTrue(
                result.Value<bool?>("ismobile") == true,
                $"{name}.device.ismobile is not readable.");
        }

        private static JObject RunInNode(string script, string name)
        {
            var scriptPath = Path.Combine(
                Path.GetTempPath(),
                $"objname-script-{Guid.NewGuid():N}.js");
            File.WriteAllText(scriptPath, script);
            try
            {
                var start = new ProcessStartInfo("node")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
                start.ArgumentList.Add(_harnessPath);
                start.ArgumentList.Add(scriptPath);
                start.ArgumentList.Add(name);
                Process process;
                try
                {
                    process = Process.Start(start);
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    Assert.Inconclusive("Node is not on the path.");
                    return null;
                }
                using (process)
                {
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    Assert.AreEqual(0, process.ExitCode, error);
                    return JObject.Parse(output.Trim());
                }
            }
            finally
            {
                File.Delete(scriptPath);
            }
        }

        private delegate void GetValueCallback(
            string key,
            out object result);

        /// <summary>
        /// Processes a mocked request carrying a small device payload and,
        /// when <paramref name="requestedName"/> is not null, the object
        /// name evidence, and returns the rendered script.
        /// </summary>
        private static string Render(
            JavaScriptBuilderElement element,
            string requestedName)
        {
            var flowData = new Mock<IFlowData>();
            var json = new JObject
            {
                ["device"] = new JObject(new JProperty("ismobile", true))
            };
            flowData.Setup(d => d.Get<IJsonBuilderElementData>())
                .Returns(() => new JsonBuilderElementData(
                    new Mock<ILogger<JsonBuilderElementData>>().Object,
                    null)
                {
                    Json = json.ToString()
                });
            flowData.Setup(d => d.TryGetEvidence(
                    JavaScriptBuilder.Constants.EVIDENCE_HOST_KEY,
                    out It.Ref<object>.IsAny))
                .Callback(new GetValueCallback(
                    (string key, out object result) =>
                    {
                        result = "localhost";
                    }))
                .Returns(true);
            var evidence = new Dictionary<string, object>();
            if (requestedName != null)
            {
                flowData.Setup(d => d.TryGetEvidence(
                        JavaScriptBuilder.Constants.EVIDENCE_OBJECT_NAME,
                        out It.Ref<object>.IsAny))
                    .Callback(new GetValueCallback(
                        (string key, out object result) =>
                        {
                            result = requestedName;
                        }))
                    .Returns(true);
                evidence.Add(
                    JavaScriptBuilder.Constants.EVIDENCE_OBJECT_NAME,
                    requestedName);
            }
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
