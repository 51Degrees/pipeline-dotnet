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

using FiftyOne.Did.Core.FlowElements;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.JavaScriptBuilder.Data;
using FiftyOne.Pipeline.JavaScriptBuilder.FlowElement;
using Moq;

namespace FiftyOne.Pipeline.JavaScript.Tests
{
    /// <summary>
    /// Unit tests for the builder's decision to render the user prompt
    /// block, which is test 20 of pipeline-dotnet#414. The browser side of
    /// the same decision is in SessionStorageCacheTests.
    /// </summary>
    [TestClass]
    public class UserPromptHookTests
    {
        /// <summary>
        /// Exposes the protected hook so the tests can call it without
        /// standing a whole pipeline up.
        /// </summary>
        private sealed class ExposedBuilder : JavaScriptBuilderElement
        {
            public ExposedBuilder()
                : base(
                    new Mock<ILogger<JavaScriptBuilderElement>>().Object,
                    (pipeline, element) =>
                        new JavaScriptBuilderElementData(
                            new Mock<ILogger<JavaScriptBuilderElementData>>().Object,
                            pipeline),
                    "/51dpipeline/json",
                    "fod",
                    false,
                    false)
            {
            }

            public bool CallRenderUserPrompt(IFlowData data)
                => RenderUserPrompt(data);
        }

        private static IElementPropertyMetaData Property(string name)
        {
            var element = new Mock<IFlowElement>();
            return new ElementPropertyMetaData(
                element.Object, name, typeof(string), true);
        }

        private static Dictionary<string,
            IReadOnlyDictionary<string, IElementPropertyMetaData>> Available(
                string elementKey, params string[] propertyNames)
        {
            var inner = new Dictionary<string, IElementPropertyMetaData>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var name in propertyNames)
            {
                inner[name] = Property(name);
            }
            return new Dictionary<string,
                IReadOnlyDictionary<string, IElementPropertyMetaData>>(
                    StringComparer.OrdinalIgnoreCase)
            {
                [elementKey] = inner,
            };
        }

        private static Dictionary<string,
            IReadOnlyDictionary<string, IElementPropertyMetaData>> Nothing()
            => new Dictionary<string,
                IReadOnlyDictionary<string, IElementPropertyMetaData>>(
                    StringComparer.OrdinalIgnoreCase);

        private static IFlowData FlowDataFor(
            IReadOnlyDictionary<string,
                IReadOnlyDictionary<string, IElementPropertyMetaData>> available)
        {
            var pipeline = new Mock<IPipeline>();
            pipeline.Setup(p => p.ElementAvailableProperties)
                .Returns(available);
            var data = new Mock<IFlowData>();
            data.Setup(d => d.Pipeline).Returns(pipeline.Object);
            return data.Object;
        }

        /// <summary>
        /// The key the builder looks up has to stay the same string as the
        /// one both 51Did engines return from ElementDataKey. They sit in
        /// different packages, so only a test keeps them together.
        /// </summary>
        [TestMethod]
        public void MirroredKeyMatchesTheEnginesElementDataKey()
        {
            Assert.AreEqual(
                DidBaseEnginePropertiesBuilder.ComponentName,
                JavaScriptBuilderElement.FODID_ELEMENT_DATA_KEY,
                "the builder's copy of the 51Did element data key has " +
                "drifted from the one the engines return");
        }

        /// <summary>
        /// An entry for the 51Did element carrying at least one available
        /// property is what the block is for.
        /// </summary>
        [TestMethod]
        public void EntryWithOnePropertyRenders()
        {
            Assert.IsTrue(
                new ExposedBuilder().CallRenderUserPrompt(
                    FlowDataFor(Available("FODid", "IdProbGlobal"))),
                "a pipeline offering a 51Did property must render the block");
        }

        /// <summary>
        /// The outer entry is created before the properties are filtered,
        /// so an element offering nothing still has a key. That case must
        /// not render.
        /// </summary>
        [TestMethod]
        public void EntryWithAnEmptyInnerDictionaryDoesNotRender()
        {
            Assert.IsFalse(
                new ExposedBuilder().CallRenderUserPrompt(
                    FlowDataFor(Available("FODid"))),
                "an element with nothing available still has an outer key, " +
                "so the inner dictionary is what decides");
        }

        /// <summary>
        /// A pipeline with no 51Did element at all.
        /// </summary>
        [TestMethod]
        public void NoEntryDoesNotRender()
        {
            Assert.IsFalse(
                new ExposedBuilder().CallRenderUserPrompt(
                    FlowDataFor(Available("device", "ismobile"))),
                "a pipeline that cannot return a 51Did has no use for the " +
                "block");
        }

        /// <summary>
        /// Moq returns null for an unconfigured interface property, so
        /// every mocked builder test in this repository reaches the hook
        /// with a null pipeline. That is pinned here rather than assumed,
        /// because the whole null safe design rests on it.
        /// </summary>
        [TestMethod]
        public void NullPipelineDoesNotRender()
        {
            var data = new Mock<IFlowData>();
            Assert.IsNull(data.Object.Pipeline,
                "Moq must keep returning null for an unconfigured " +
                "interface property, which is what the hook is written for");

            Assert.IsFalse(
                new ExposedBuilder().CallRenderUserPrompt(data.Object),
                "a flow data with no pipeline must not render the block");
        }

        /// <summary>
        /// The cloud's two copies of the builder unit tests set up
        /// d.Pipeline.GetElement, which gives a recursive mock whose
        /// ElementAvailableProperties is null.
        /// </summary>
        [TestMethod]
        public void NullAvailablePropertiesDoesNotRender()
        {
            var pipeline = new Mock<IPipeline>();
            Assert.IsNull(pipeline.Object.ElementAvailableProperties,
                "the recursive mock case this test stands for depends on " +
                "an unconfigured dictionary coming back null");

            var data = new Mock<IFlowData>();
            data.Setup(d => d.Pipeline).Returns(pipeline.Object);

            Assert.IsFalse(
                new ExposedBuilder().CallRenderUserPrompt(data.Object),
                "a null dictionary must mean not rendered rather than an " +
                "exception");
        }

        /// <summary>
        /// A positive answer is remembered so the pipeline is not asked
        /// again, and a negative one is not, because a 51Did element whose
        /// properties have not loaded yet would otherwise switch the block
        /// off for the life of the process.
        /// </summary>
        [TestMethod]
        public void PositiveIsRememberedAndNegativeIsNot()
        {
            var withProperty = Available("FODid", "IdProbGlobal");
            var empty = Nothing();

            // The pipeline answers "nothing yet" on the first two reads and
            // then reports the property, which is the start up sequence for
            // a cloud engine whose properties arrive late.
            var reads = 0;
            var pipeline = new Mock<IPipeline>();
            pipeline.Setup(p => p.ElementAvailableProperties)
                .Returns(() =>
                {
                    reads++;
                    return reads <= 2 ? empty : withProperty;
                });
            var data = new Mock<IFlowData>();
            data.Setup(d => d.Pipeline).Returns(pipeline.Object);

            var builder = new ExposedBuilder();
            Assert.IsFalse(builder.CallRenderUserPrompt(data.Object),
                "nothing is available on the first read");
            Assert.IsFalse(builder.CallRenderUserPrompt(data.Object),
                "a negative answer must not be remembered, or a slow " +
                "start up would switch the block off permanently");
            Assert.IsTrue(builder.CallRenderUserPrompt(data.Object),
                "the property is available on the third read");

            var readsWhenTrue = reads;
            Assert.IsTrue(builder.CallRenderUserPrompt(data.Object),
                "the answer stays true");
            Assert.AreEqual(readsWhenTrue, reads,
                "a positive answer must be remembered rather than asked " +
                "for again");
        }

        /// <summary>
        /// The decision belongs to the pipeline the request came through,
        /// so two pipelines sharing one element instance get their own
        /// answers.
        /// </summary>
        [TestMethod]
        public void TwoPipelinesAreDecidedSeparately()
        {
            var builder = new ExposedBuilder();
            Assert.IsTrue(
                builder.CallRenderUserPrompt(
                    FlowDataFor(Available("FODid", "IdProbGlobal"))),
                "the pipeline that offers the property renders");
            Assert.IsFalse(
                builder.CallRenderUserPrompt(FlowDataFor(Nothing())),
                "the pipeline that does not offer it must not inherit the " +
                "other pipeline's answer");
        }
    }
}
