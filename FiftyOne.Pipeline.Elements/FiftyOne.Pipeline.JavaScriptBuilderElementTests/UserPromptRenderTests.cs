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
using FiftyOne.Pipeline.JsonBuilder.FlowElement;

namespace FiftyOne.Pipeline.JavaScript.Tests
{
    /// <summary>
    /// The guard test of the create last programme, plus test 19 of
    /// pipeline-dotnet#414. Both read the script a real pipeline serves,
    /// with no browser involved, so a build that quietly picked up the old
    /// template fails here rather than in a Selenium run.
    /// </summary>
    [TestClass]
    public class UserPromptRenderTests
    {
        /// <summary>
        /// The text agent A's template prints when refresh() is called at
        /// the server's iteration cap. It exists only in the template as
        /// changed on javascript-templates feature/create-last-a, so a
        /// script that does not carry it was rendered from the old
        /// template and every test in this project would be proving
        /// nothing. Recorded in D:\Workspace\create-last\reports\A.md
        /// under "GUARD STRING".
        /// </summary>
        private const string GuardString = "51Degrees: the maximum of";

        /// <summary>
        /// Two names that appear only inside the template's user prompt
        /// section, so they say whether the block itself was rendered.
        /// The guard string above sits outside the section and is present
        /// whenever updates are enabled, which agent A's report states and
        /// this class pins.
        /// </summary>
        private static readonly string[] BlockOnlyNames =
        {
            "__51d_pmp",
            "__tcfapi",
            "51d-pmp-preference",
        };

        private readonly TestLoggerFactory _loggerFactory =
            new TestLoggerFactory();

        private class StubData : ElementDataBase
        {
            public StubData(ILogger<ElementDataBase> logger, IPipeline pipeline)
                : base(logger, pipeline)
            {
            }
        }

        /// <summary>
        /// Stands in for one of the two 51Did engines. It declares the
        /// element data key both of them return and offers one available
        /// property, which is all the builder's rule asks for.
        /// </summary>
        private class StubDidElement
            : FlowElementBase<StubData, ElementPropertyMetaData>
        {
            private readonly ILoggerFactory _loggerFactory;
            private readonly bool _available;

            public StubDidElement(ILoggerFactory loggerFactory, bool available)
                : base(loggerFactory.CreateLogger<
                    FlowElementBase<StubData, ElementPropertyMetaData>>())
            {
                _loggerFactory = loggerFactory;
                _available = available;
            }

            public override string ElementDataKey =>
                JavaScriptBuilderElement.FODID_ELEMENT_DATA_KEY;

            public override IEvidenceKeyFilter EvidenceKeyFilter =>
                new EvidenceKeyFilterWhitelist(new List<string>());

            public override IList<ElementPropertyMetaData> Properties =>
                new List<ElementPropertyMetaData>()
                {
                    new ElementPropertyMetaData(
                        this, "IdProbGlobal", typeof(string), _available),
                };

            protected override void ProcessInternal(IFlowData data)
            {
                var result = new StubData(
                    _loggerFactory.CreateLogger<StubData>(), data.Pipeline);
                result["IdProbGlobal"] =
                    new AspectPropertyValue<string>("A-51Did");
                data.GetOrAdd(ElementDataKey, p => result);
            }

            protected override void ManagedResourcesCleanup() { }
            protected override void UnmanagedResourcesCleanup() { }
        }

        private class DeviceData : ElementDataBase
        {
            public DeviceData(ILogger<ElementDataBase> logger, IPipeline pipeline)
                : base(logger, pipeline)
            {
            }
        }

        /// <summary>
        /// An ordinary element, so the pipeline has something to report
        /// whether or not the 51Did element is in it.
        /// </summary>
        private class DeviceElement
            : FlowElementBase<DeviceData, ElementPropertyMetaData>
        {
            private readonly ILoggerFactory _loggerFactory;

            public DeviceElement(ILoggerFactory loggerFactory)
                : base(loggerFactory.CreateLogger<
                    FlowElementBase<DeviceData, ElementPropertyMetaData>>())
            {
                _loggerFactory = loggerFactory;
            }

            public override string ElementDataKey => "device";

            public override IEvidenceKeyFilter EvidenceKeyFilter =>
                new EvidenceKeyFilterWhitelist(new List<string>());

            public override IList<ElementPropertyMetaData> Properties =>
                new List<ElementPropertyMetaData>()
                {
                    new ElementPropertyMetaData(
                        this, "ismobile", typeof(bool), true),
                };

            protected override void ProcessInternal(IFlowData data)
            {
                var result = new DeviceData(
                    _loggerFactory.CreateLogger<DeviceData>(), data.Pipeline);
                result["ismobile"] = new AspectPropertyValue<bool>(true);
                data.GetOrAdd(ElementDataKey, p => result);
            }

            protected override void ManagedResourcesCleanup() { }
            protected override void UnmanagedResourcesCleanup() { }
        }

        /// <summary>
        /// Builds a pipeline and returns the script it serves.
        /// </summary>
        /// <param name="didElement">
        /// Null for a pipeline holding no 51Did element, otherwise the
        /// stub to include.
        /// </param>
        private string Render(StubDidElement didElement)
        {
            var builder = new PipelineBuilder(_loggerFactory)
                .AddFlowElement(new DeviceElement(_loggerFactory));
            if (didElement != null)
            {
                builder = builder.AddFlowElement(didElement);
            }
            using var pipeline = builder
                .AddFlowElement(new SequenceElementBuilder(_loggerFactory).Build())
                .AddFlowElement(new JsonBuilderElementBuilder(_loggerFactory).Build())
                .AddFlowElement(new JavaScriptBuilderElementBuilder(_loggerFactory)
                    .SetMinify(false)
                    .SetProtocol("http")
                    .SetHost("localhost:1234")
                    .SetEndpoint("/51dpipeline/json")
                    .SetEnableCookies(false)
                    .Build())
                .Build();

            using var data = pipeline.CreateFlowData();
            data.Process();
            return data.Get<IJavaScriptBuilderElementData>().JavaScript;
        }

        /// <summary>
        /// Standing instruction 3 of the create last programme. The script
        /// rendered with the block on has to carry text that exists only
        /// in agent A's template, or the build picked up the old one and
        /// nothing else in this project is proving what it claims.
        /// </summary>
        [TestMethod]
        public void GuardStringIsInTheScriptRenderedWithTheBlockOn()
        {
            var script = Render(new StubDidElement(_loggerFactory, true));

            Assert.IsTrue(script.Contains(GuardString, StringComparison.Ordinal),
                $"the rendered script does not contain \"{GuardString}\", so " +
                "the build is using a template without the create last " +
                "changes and no test in this project means anything. The " +
                "Templates submodule has to point at the template branch.");
        }

        /// <summary>
        /// The block itself. The guard string sits outside the template's
        /// user prompt section, so a second pair of names is what says the
        /// section rendered, and the same names say it did not render for
        /// a pipeline that cannot return a 51Did.
        /// </summary>
        [TestMethod]
        public void TheBlockIsPresentWithTheHookOnAndAbsentWithItOff()
        {
            var withDid = Render(new StubDidElement(_loggerFactory, true));
            var withoutDid = Render(null);

            foreach (var name in BlockOnlyNames)
            {
                Assert.IsTrue(
                    withDid.Contains(name, StringComparison.Ordinal),
                    $"a pipeline offering a 51Did property must render the " +
                    $"block, and \"{name}\" is missing from the script");
                Assert.IsFalse(
                    withoutDid.Contains(name, StringComparison.Ordinal),
                    $"a pipeline with no 51Did element must not render the " +
                    $"block, and \"{name}\" is in the script");
            }

            Assert.IsTrue(
                withDid.Contains(GuardString, StringComparison.Ordinal) &&
                withoutDid.Contains(GuardString, StringComparison.Ordinal),
                "the guard string sits outside the section, so it is in " +
                "both scripts whenever updates are enabled. Agent A's " +
                "report states this and it is pinned here so that a later " +
                "template change that moves it inside the section is " +
                "noticed rather than silently weakening the guard.");
        }

        /// <summary>
        /// Test 19. An element carrying the 51Did key but nothing
        /// available is the unentitled customer pipeline, and it must
        /// render no block, because the outer entry exists for it either
        /// way.
        /// </summary>
        [TestMethod]
        public void AnElementWithNothingAvailableRendersNoBlock()
        {
            var script = Render(new StubDidElement(_loggerFactory, false));

            foreach (var name in BlockOnlyNames)
            {
                Assert.IsFalse(
                    script.Contains(name, StringComparison.Ordinal),
                    "an element offering no available property must not " +
                    $"render the block, and \"{name}\" is in the script");
            }
        }

        /// <summary>
        /// Test 18's second half. The number the template stops at is the
        /// JSON builder's own constant, so a change to one without the
        /// other is caught here rather than by a page that stops
        /// refreshing early or keeps asking forever.
        /// </summary>
        [TestMethod]
        public void TheTemplatesIterationCapIsTheJsonBuildersConstant()
        {
            var script = Render(new StubDidElement(_loggerFactory, true));
            var expected =
                JsonBuilder.Constants.MAX_JAVASCRIPT_ITERATIONS
                    .ToString(System.Globalization.CultureInfo.InvariantCulture);

            Assert.IsTrue(
                script.Contains(
                    $"maxIterations = {expected}", StringComparison.Ordinal),
                "the script must stop at the same number of iterations the " +
                "JSON builder stops listing snippets at, which is " +
                $"{expected}. The rendered script does not set " +
                $"maxIterations to it.");
        }
    }
}
