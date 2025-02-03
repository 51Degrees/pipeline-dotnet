/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2025 51 Degrees Mobile Experts Limited, Davidson House,
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
using FiftyOne.Pipeline.Core.Configuration;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Core.Services;
using FiftyOne.Pipeline.Core.Tests.HelperClasses;
using FiftyOne.Pipeline.Core.Tests.HelperClasses.CompositeConfig;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace FiftyOne.Pipeline.Core.Tests.FlowElements
{
    /// <summary>
    /// Tests impacting the <see cref="PipelineBuilder"/> class
    /// </summary>
    [TestClass]
    public class PipelineBuilderTests
    {
        private PipelineBuilder _builder;
        private TestLoggerFactory _loggerFactory;

        private Mock<IFlowElement> _element = new Mock<IFlowElement>();

        private int _maxErrors = 0;
        private int _maxWarnings = 0;


        [TestInitialize]
        public void Initialize()
        {
            _loggerFactory = new TestLoggerFactory();
            _builder = new PipelineBuilder(_loggerFactory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            foreach (var logger in _loggerFactory.Loggers)
            {
                logger.AssertMaxErrors(_maxErrors);
                logger.AssertMaxWarnings(_maxWarnings);
            }
        }

        /// <summary>
        /// Test that the expected exception is thrown if a disposed
        /// <see cref="IFlowElement"/> is added to the builder.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ObjectDisposedException))]
        public void PipelineBuilder_AddFlowElement_Disposed()
        {
            _element.Setup(e => e.IsDisposed).Returns(true);
            _builder.AddFlowElement(_element.Object);
        }

        /// <summary>
        /// Test that the expected exception is thrown if a disposed
        /// <see cref="IFlowElement"/> is added to the builder using 
        /// the AddFlowElementsParallel method.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ObjectDisposedException))]
        public void PipelineBuilder_AddFlowElementsParallel_Disposed()
        {
            _element = new Mock<IFlowElement>();
            _element.Setup(e => e.IsDisposed).Returns(true);
            Mock<IFlowElement> element2 = new Mock<IFlowElement>();

            _builder.AddFlowElementsParallel(element2.Object, _element.Object);
        }

        /// <summary>
        /// Test that the expected exception gets thrown if 
        /// BuildFromConfiguration is passed a null parameter.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void PipelineBuilder_BuildFromConfiguration_Null()
        {
            var pipeline = _builder.BuildFromConfiguration(null);
        }

        /// <summary>
        /// Test that the Pipeline builder works as expected when building 
        /// a pipeline from configuration.
        /// The element builder used has a single mandatory parameter on
        /// the build method.
        /// </summary>
        [TestMethod]
        public void PipelineBuilder_BuildFromConfiguration_SingleMandatoryParameter()
        {
            // Create the configuration object.
            var element = new ElementOptions()
            {
                BuilderName = "MultiplyByElementBuilder"
            };
            element.BuildParameters.Add("multiple", "8");

            PipelineOptions opts = new PipelineOptions();
            opts.Elements = new List<ElementOptions>
            {
                element
            };

            VerifyMultiplyByElementPipeline(opts);
        }

        /// <summary>
        /// Test that the Pipeline builder works as expected when building 
        /// a pipeline from configuration where the builder class name
        /// matches by the convention of adding 'Builder' to the end of 
        /// the supplied string.
        /// </summary>
        [TestMethod]
        public void PipelineBuilder_BuildFromConfiguration_ClassNameByConvention()
        {
            // Create the configuration object.
            var element = new ElementOptions()
            {
                BuilderName = "MultiplyByElement"
            };
            element.BuildParameters.Add("multiple", "8");

            PipelineOptions opts = new PipelineOptions();
            opts.Elements = new List<ElementOptions>
            {
                element
            };

            VerifyMultiplyByElementPipeline(opts);
        }

        /// <summary>
        /// Test that the Pipeline builder works as expected when building 
        /// a pipeline from configuration where the builder class name
        /// matches by the convention of adding 'Builder' to the end of 
        /// the supplied string.
        /// </summary>
        [TestMethod]
        public void PipelineBuilder_BuildFromConfiguration_ClassNameAlternate()
        {
            var element = new ElementOptions()
            {
                BuilderName = "Multiply"
            };
            element.BuildParameters.Add("multiple", "8");

            // Create the configuration object.
            PipelineOptions opts = new PipelineOptions();
            opts.Elements = new List<ElementOptions>
            {
                element
            };

            VerifyMultiplyByElementPipeline(opts);
        }

        /// <summary>
        /// Test that the Pipeline builder throws the expected exception
        /// when mandatory parameters are not provided by the configuration
        /// object.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(PipelineConfigurationException))]
        public void PipelineBuilder_BuildFromConfiguration_MandatoryParameterNotSet()
        {
            // Create the configuration object.
            PipelineOptions opts = new PipelineOptions();
            opts.Elements = new List<ElementOptions>
            {
                new ElementOptions()
                {
                    BuilderName = "MultiplyByElementBuilder"
                }
            };
            _maxErrors = 1;

            // Pass the configuration to the builder to create the pipeline.
            var pipeline = _builder.BuildFromConfiguration(opts);
        }

        /// <summary>
        /// Test that the Pipeline builder throws the expected exception
        /// when the mandatory parameter value cannot be parsed to the 
        /// expected type
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(PipelineConfigurationException))]
        public void PipelineBuilder_BuildFromConfiguration_MandatoryParameterWrongType()
        {
            var element = new ElementOptions()
            {
                BuilderName = "MultiplyByElementBuilder"
            };
            element.BuildParameters.Add("multiple", "WrongType");

            // Create the configuration object.
            PipelineOptions opts = new PipelineOptions();
            opts.Elements = new List<ElementOptions>
            {
                element
            };
            _maxErrors = 1;

            // Pass the configuration to the builder to create the pipeline.
            var pipeline = _builder.BuildFromConfiguration(opts);
        }

        /// <summary>
        /// Test that the Pipeline builder throws the expected exception
        /// when the specified builder does not exist
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(PipelineConfigurationException))]
        public void PipelineBuilder_BuildFromConfiguration_NoBuilder()
        {
            var element = new ElementOptions()
            {
                BuilderName = "NoBuilder"
            };

            // Create the configuration object.
            PipelineOptions opts = new PipelineOptions();
            opts.Elements = new List<ElementOptions>
            {
                element
            };
            _maxErrors = 1;

            // Pass the configuration to the builder to create the pipeline.
            var pipeline = _builder.BuildFromConfiguration(opts);
        }

        /// <summary>
        /// Test that the Pipeline builder throws the expected exception
        /// when the specified class exists but is not used for building
        /// IFlowElements
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(PipelineConfigurationException))]
        public void PipelineBuilder_BuildFromConfiguration_WrongBuilder()
        {
            // Create the configuration object.
            PipelineOptions opts = new PipelineOptions();
            opts.Elements = new List<ElementOptions>
            {
                new ElementOptions()
                {
                    BuilderName = "PipelineBuilder"
                }
            };
            _maxErrors = 1;

            // Pass the configuration to the builder to create the pipeline.
            var pipeline = _builder.BuildFromConfiguration(opts);
        }

        /// <summary>
        /// Test that the Pipeline builder works when setting an optional
        /// parameter using the full method name
        /// </summary>
        [TestMethod]
        public void PipelineBuilder_BuildFromConfiguration_OptionalParameter()
        {
            var element = new ElementOptions()
            {
                BuilderName = "ListSplitterElement"
            };
            element.BuildParameters.Add("SetDelimiter", "|");

            // Create the configuration object.
            PipelineOptions opts = new PipelineOptions();
            opts.Elements = new List<ElementOptions>
            {
                element
            };

            VerifyListSplitterElementPipeline(opts, SplitOption.Pipe);
        }

        /// <summary>
        /// Test that the Pipeline builder works when setting an optional
        /// parameter using the convention of removing 'Set' from the start
        /// of the method name.
        /// </summary>
        [TestMethod]
        public void PipelineBuilder_BuildFromConfiguration_MethodNameConvention()
        {
            var element = new ElementOptions()
            {
                BuilderName = "ListSplitterElement"
            };
            element.BuildParameters.Add("Delimiter", "|");

            // Create the configuration object.
            PipelineOptions opts = new PipelineOptions();
            opts.Elements = new List<ElementOptions>
            {
                element
            };

            VerifyListSplitterElementPipeline(opts, SplitOption.Pipe);
        }

        /// <summary>
        /// Test that the Pipeline builder works when setting an optional
        /// parameter using the alternate name attribute on the method.
        /// </summary>
        [TestMethod]
        public void PipelineBuilder_BuildFromConfiguration_MethodNameAlternate()
        {
            var element = new ElementOptions()
            {
                BuilderName = "ListSplitterElement"
            };
            element.BuildParameters.Add("delim", "|");

            // Create the configuration object.
            PipelineOptions opts = new PipelineOptions();
            opts.Elements = new List<ElementOptions>
            {
                element
            };

            VerifyListSplitterElementPipeline(opts, SplitOption.Pipe);
        }

        /// <summary>
        /// Test that the Pipeline builder works when an optional parameter
        /// is left as the default value
        /// </summary>
        [TestMethod]
        public void PipelineBuilder_BuildFromConfiguration_OptionalDefault()
        {
            // Create the configuration object.
            PipelineOptions opts = new PipelineOptions();
            opts.Elements = new List<ElementOptions>
            {
                new ElementOptions()
                {
                    BuilderName = "ListSplitterElement",
                }
            };

            VerifyListSplitterElementPipeline(opts, SplitOption.Comma);
        }

        /// <summary>
        /// Test that the Pipeline builder throws an exception if the specified
        /// optional parameter method name does not exist.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(PipelineConfigurationException))]
        public void PipelineBuilder_BuildFromConfiguration_OptionalMethodMissing()
        {
            var element = new ElementOptions()
            {
                BuilderName = "ListSplitterElement"
            };
            element.BuildParameters.Add("NoMethod", "|");

            // Create the configuration object.
            PipelineOptions opts = new PipelineOptions();
            opts.Elements = new List<ElementOptions>
            {
                element
            };
            _maxErrors = 1;

            VerifyListSplitterElementPipeline(opts, SplitOption.Comma);
        }

        /// <summary>
        /// Test that the Pipeline builder works as expected if the specified
        /// optional parameter method takes an integer
        /// </summary>
        [TestMethod]
        public void PipelineBuilder_BuildFromConfiguration_OptionalMethodInteger()
        {
            var element = new ElementOptions()
            {
                BuilderName = "ListSplitterElement"
            };
            element.BuildParameters.Add("MaxLength", "3");

            // Create the configuration object.
            PipelineOptions opts = new PipelineOptions();
            opts.Elements = new List<ElementOptions>
            {
                element
            };

            VerifyListSplitterElementPipeline(opts, SplitOption.CommaMaxLengthThree);
        }

        /// <summary>
        /// Test that the Pipeline builder throws an exception if the specified
        /// optional parameter method takes an integer but is given a value
        /// that cannot be parsed
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(PipelineConfigurationException))]
        public void PipelineBuilder_BuildFromConfiguration_OptionalMethodWrongType()
        {
            var element = new ElementOptions()
            {
                BuilderName = "ListSplitterElement"
            };
            element.BuildParameters.Add("MaxLength", "WrongType");

            // Create the configuration object.
            PipelineOptions opts = new PipelineOptions();
            opts.Elements = new List<ElementOptions>
            {
                element
            };
            _maxErrors = 1;

            VerifyListSplitterElementPipeline(opts, SplitOption.Comma);
        }


        /// <summary>
        /// Test that the Pipeline builder works when setting an optional
        /// parameter using the full method name
        /// </summary>
        [TestMethod]
        public void PipelineBuilder_BuildFromConfiguration_ParallelElements()
        {
            var multiplyElement = new ElementOptions()
            {
                BuilderName = "MultiplyByElement"
            };
            multiplyElement.BuildParameters.Add("Multiple", "3");

            // Create the element that holds the elements that
            // will be run in parallel.
            var parentElement = new ElementOptions()
            {
            };
            parentElement.SubElements.Add(new ElementOptions()
            {
                BuilderName = "ListSplitterElement"
            });
            parentElement.SubElements.Add(multiplyElement);

            // Create the configuration object.
            PipelineOptions options = new PipelineOptions();
            options.Elements = new List<ElementOptions>
            {
                parentElement
            };

            // Pass the configuration to the builder to create the pipeline.
            var pipeline = _builder.BuildFromConfiguration(options);
            // Get the elements
            var splitterElement = pipeline.GetElement<ListSplitterElement>();
            var multiplyByElement = pipeline.GetElement<MultiplyByElement>();

            // Create, populate and process flow data.
            using (var flowData = pipeline.CreateFlowData())
            {
                flowData
                    .AddEvidence(splitterElement.EvidenceKeys[0], "1,2,abc")
                    .AddEvidence(multiplyByElement.EvidenceKeys[0], 25)
                    .Process();

                // Get the results and verify them.
                var splitterData = flowData.GetFromElement(splitterElement);
                var multiplyByData = flowData.GetFromElement(multiplyByElement);

                Assert.AreEqual("1", splitterData.Result[0]);
                Assert.AreEqual("2", splitterData.Result[1]);
                Assert.AreEqual("abc", splitterData.Result[2]);
                Assert.AreEqual(75, multiplyByData.Result);
            }
        }

        /// <summary>
        /// Test that the Pipeline builder can get element builder instances
        /// from a service collection.
        /// </summary>
        [TestMethod]
        public void PipelineBuilder_BuildFromConfiguration_ServiceCollection()
        {
            var element = new ElementOptions()
            {
                BuilderName = "MultiplyByElementBuilder"
            };
            element.BuildParameters.Add("multiple", "3");

            // Create the configuration object.
            PipelineOptions opts = new PipelineOptions();
            opts.Elements = new List<ElementOptions>
            {
                element
            };

            Mock<IServiceProvider> services = new Mock<IServiceProvider>();
            services.Setup(s => s.GetService(typeof(MultiplyByElementBuilder)))
                .Returns(new MultiplyByElementBuilder());

            // Pass the configuration to the builder to create the pipeline.
            var pipeline = new PipelineBuilder(_loggerFactory, services.Object)
                .BuildFromConfiguration(opts);

            // Get the element
            var multiplyByElement = pipeline.GetElement<MultiplyByElement>();

            // Create, populate and process flow data.
            using (var flowData = pipeline.CreateFlowData())
            {
                flowData
                    .AddEvidence(multiplyByElement.EvidenceKeys[0], 25)
                    .Process();

                // Get the results and verify them.
                var multiplyByData = flowData.GetFromElement(multiplyByElement);

                Assert.AreEqual(75, multiplyByData.Result);
            }
        }

        /// <summary>
        /// Test that the Pipeline builder throws an exception when a builder
        /// is not available in the service collection.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void PipelineBuilder_BuildFromConfiguration_NotInServiceCollection()
        {
            var element = new ElementOptions()
            {
                BuilderName = "MultiplyByElementBuilder"
            };
            element.BuildParameters.Add("multiple", "3");

            // Create the configuration object.
            PipelineOptions opts = new PipelineOptions();
            opts.Elements = new List<ElementOptions>
            {
                element
            };

            _maxErrors = 1;

            Mock<IServiceProvider> services = new Mock<IServiceProvider>();
            services.Setup(s => s.GetService(typeof(MultiplyByElementBuilder)))
                .Returns(null);

            // Pass the configuration to the builder to create the pipeline.
            var pipeline = new PipelineBuilder(_loggerFactory, services.Object)
                .BuildFromConfiguration(opts);
        }

        [TestMethod]
        public void PipelineBuilder_BuildFromConfiguration_Composite_ThroughExtensions()
        {
            var element = new ElementOptions()
            {
                BuilderName = "CompositeConfigElement"
            };
            element.BuildParameters.Add("Number", 42);
            element.BuildParameters.Add("Text", "dummy");

            // Create the configuration object.
            PipelineOptions opts = new PipelineOptions();
            opts.Elements = new List<ElementOptions>
            {
                element
            };

            var pipeline = _builder.BuildFromConfiguration(opts);
            var builtElement = pipeline.GetElement<CompositeConfigElement>();
            Assert.AreEqual(42, builtElement.Number);
            Assert.AreEqual("dummy", builtElement.Text);
        }

        [TestMethod]
        public void PipelineBuilder_BuildFromConfiguration_Composite_NoValues()
        {
            var element = new ElementOptions()
            {
                BuilderName = "CompositeConfigElement"
            };

            // Create the configuration object.
            PipelineOptions opts = new PipelineOptions();
            opts.Elements = new List<ElementOptions>
            {
                element
            };

            var pipeline = _builder.BuildFromConfiguration(opts);
            var builtElement = pipeline.GetElement<CompositeConfigElement>();
            Assert.AreEqual(0, builtElement.Number);
            Assert.IsNull(builtElement.Text);
        }

        [TestMethod]
        public void PipelineBuilder_BuildFromConfiguration_Composite_BuilderControl()
        {
            var builtElement = new CompositeConfigElementBuilder()
                .SetNumber(88888888)
                .SetText("Lucky")
                .Build() as CompositeConfigElement;
            Assert.AreEqual(88888888, builtElement.Number);
            Assert.AreEqual("Lucky", builtElement.Text);
        }

        /// <summary>
        /// Test that the Pipeline builder works when the set method
        /// takes a list parameter and we pass an array
        /// </summary>
        [TestMethod]
        public void PipelineBuilder_BuildFromConfiguration_ListFromArray()
        {
            var element = new ElementOptions()
            {
                BuilderName = "ListSplitterElement"
            };
            element.BuildParameters.Add("Delimiters", new string[] { "|", "," });

            // Create the configuration object.
            PipelineOptions opts = new PipelineOptions();
            opts.Elements = new List<ElementOptions>
            {
                element
            };

            VerifyListSplitterElementPipeline(opts, SplitOption.CommaAndPipe);
        }

        /// <summary>
        /// Test that the Pipeline builder works when the set method
        /// takes a list parameter and we pass a list
        /// </summary>
        [TestMethod]
        public void PipelineBuilder_BuildFromConfiguration_List()
        {
            var element = new ElementOptions()
            {
                BuilderName = "ListSplitterElement"
            };
            element.BuildParameters.Add("Delimiters", new List<string>() { "|", "," });

            // Create the configuration object.
            PipelineOptions opts = new PipelineOptions();
            opts.Elements = new List<ElementOptions>
            {
                element
            };

            VerifyListSplitterElementPipeline(opts, SplitOption.CommaAndPipe);
        }

        /// <summary>
        /// Test that the Pipeline builder works when the set method
        /// takes a list parameter and we pass a comma-separated string
        /// containing the delimiters 'A' and '|'
        /// </summary>
        [TestMethod]
        public void PipelineBuilder_BuildFromConfiguration_ListFromString()
        {
            var element = new ElementOptions()
            {
                BuilderName = "ListSplitterElement"
            };
            element.BuildParameters.Add("Delimiters", "A,|");

            // Create the configuration object.
            PipelineOptions opts = new PipelineOptions();
            opts.Elements = new List<ElementOptions>
            {
                element
            };

            VerifyListSplitterElementPipeline(opts, SplitOption.Pipe);
        }

        /// <summary>
        /// Test that the Pipeline builder works when the set method
        /// takes a list parameter and we pass a comma-separated string
        /// where one of the delimiters is a comma.
        /// </summary>
        [TestMethod]
        public void PipelineBuilder_BuildFromConfiguration_ListFromStringWithComma()
        {
            var element = new ElementOptions()
            {
                BuilderName = "ListSplitterElement"
            };
            element.BuildParameters.Add("Delimiters", "|,\",\"");

            // Create the configuration object.
            PipelineOptions opts = new PipelineOptions();
            opts.Elements = new List<ElementOptions>
            {
                element
            };

            VerifyListSplitterElementPipeline(opts, SplitOption.CommaAndPipe);
        }

        /// <summary>
        /// Test that the Pipeline builder works when the set method
        /// takes a list parameter and we pass a comma-separated string
        /// containing an escaped double quote character.
        /// </summary>
        [TestMethod]
        public void PipelineBuilder_BuildFromConfiguration_ListFromStringWithQuote()
        {
            var element = new ElementOptions()
            {
                BuilderName = "ListSplitterElement"
            };
            element.BuildParameters.Add("Delimiters", "|,\"\"\"\"");

            // Create the configuration object.
            PipelineOptions opts = new PipelineOptions();
            opts.Elements = new List<ElementOptions>
            {
                element
            };

            VerifyListSplitterElementPipeline(opts, SplitOption.PipeAndQuote);
        }

        /// <summary>
        /// Test that the Pipeline builder works when the set method
        /// takes a list parameter and we pass a comma-separated string
        /// that only has one item
        /// </summary>
        [TestMethod]
        public void PipelineBuilder_BuildFromConfiguration_ListFromStringSingleEntry()
        {
            var element = new ElementOptions()
            {
                BuilderName = "ListSplitterElement"
            };
            element.BuildParameters.Add("Delimiters", "|");

            // Create the configuration object.
            PipelineOptions opts = new PipelineOptions();
            opts.Elements = new List<ElementOptions>
            {
                element
            };

            VerifyListSplitterElementPipeline(opts, SplitOption.Pipe);
        }

        /// <summary>
        /// Test that if the services declared in a builders constructor are not
        /// provided, but there are other constructors available, that the correct
        /// constructor is called.
        /// This is specifically testing the logic used when a ServiceCollection
        /// is not available e.g. in ASP.NET Framework.
        /// </summary>
        [TestMethod]
        public void PipelineBuilder_BuildFromConfiguration_AssemblyServices_NotAvailable()
        {
            var element = new ElementOptions()
            {
                BuilderName = "RequiredService"
            };

            // Create configuration object.
            PipelineOptions opts = new PipelineOptions();
            opts.Elements = new List<ElementOptions>
            {
                element
            };

            // Pass the configuration to the builder to create the pipeline.
            var pipeline = new PipelineBuilder(_loggerFactory, new FiftyOneServiceProvider())
                .BuildFromConfiguration(opts);

            Assert.IsNotNull(pipeline.GetElement<RequiredServiceElement>().LoggerFactory);
            // The service is not available in the service provider, but it can be created
            // by the service provider.
            Assert.IsNotNull(pipeline.GetElement<RequiredServiceElement>().Service);
            Assert.IsNull(pipeline.GetElement<RequiredServiceElement>().UpdateService);
        }

        /// <summary>
        /// Test that if the services declared in a builders constructor are provided,
        /// but there are other constructors available, that the correct constructor
        /// is called.
        /// This is specifically testing the logic used when a ServiceCollection
        /// is not available e.g. in ASP.NET Framework.
        /// </summary>
        [TestMethod]
        public void PipelineBuilder_BuildFromConfiguration_AssemblyServices_Available()
        {
            var element = new ElementOptions()
            {
                BuilderName = "RequiredService"
            };

            // Create configuration object.
            PipelineOptions opts = new PipelineOptions();
            opts.Elements = new List<ElementOptions>
            {
                element
            };

            var service = new RequiredServiceElementBuilder.EmptyService();
            var services = new FiftyOneServiceProvider();
            services.AddService(service);

            // Pass the configuration to the builder to create the pipeline.
            var pipeline = new PipelineBuilder(_loggerFactory, services)
                .BuildFromConfiguration(opts);

            Assert.IsNotNull(pipeline.GetElement<RequiredServiceElement>().LoggerFactory);
            Assert.IsNotNull(pipeline.GetElement<RequiredServiceElement>().Service);
            Assert.AreEqual(service, pipeline.GetElement<RequiredServiceElement>().Service);
            Assert.IsNull(pipeline.GetElement<RequiredServiceElement>().UpdateService);
        }

        /// <summary>
        /// Test that if the services declared in a builders constructor are provided,
        /// but there are other constructors available, that the correct constructor
        /// is called. This include the DataUpdateService to test the specific
        /// scenario in addition to the general one.
        /// This is specifically testing the logic used when a ServiceCollection
        /// is not available e.g. in ASP.NET Framework.
        /// </summary>
        [TestMethod]
        public void PipelineBuilder_BuildFromConfiguration_AssemblyServices_MultiAvailable()
        {
            var element = new ElementOptions()
            {
                BuilderName = "RequiredService"
            };

            // Create configuration object.
            PipelineOptions opts = new PipelineOptions();
            opts.Elements = new List<ElementOptions>
            {
                element
            };

            var service = new RequiredServiceElementBuilder.EmptyService();
            var httpClient = new Mock<HttpClient>();
            var updateService = new DataUpdateService(
                new Mock<ILogger<DataUpdateService>>().Object,
                httpClient.Object);
            var services = new FiftyOneServiceProvider();
            services.AddService(service);
            services.AddService(updateService);

            // Pass the configuration to the builder to create the pipeline.
            var pipeline = new PipelineBuilder(_loggerFactory, services)
                .BuildFromConfiguration(opts);

            Assert.IsNotNull(pipeline.GetElement<RequiredServiceElement>().LoggerFactory);
            Assert.IsNotNull(pipeline.GetElement<RequiredServiceElement>().Service);
            Assert.AreEqual(service, pipeline.GetElement<RequiredServiceElement>().Service);
            Assert.IsNotNull(pipeline.GetElement<RequiredServiceElement>().UpdateService);
            Assert.AreEqual(updateService, pipeline.GetElement<RequiredServiceElement>().UpdateService);
        }

        private enum SplitOption { Comma, Pipe, CommaMaxLengthThree, CommaAndPipe, PipeAndQuote }

        private void VerifyListSplitterElementPipeline(
            PipelineOptions options,
            SplitOption splitOn)
        {
            // Pass the configuration to the builder to create the pipeline.
            var pipeline = _builder.BuildFromConfiguration(options);

            var element = pipeline.GetElement<ListSplitterElement>();
            // Check we've got the expected number of evidence keys.
            Assert.AreEqual(1, element.EvidenceKeys.Count);

            // Create, populate and process flow data.
            using (var flowData = pipeline.CreateFlowData())
            {
                flowData.AddEvidence(element.EvidenceKeys[0], "123,45\"6|789,0")
                    .Process();

                // Get the result and verify it.
                var elementData = flowData.GetFromElement(element);
                switch (splitOn)
                {
                    case SplitOption.Comma:
                        Assert.AreEqual("123", elementData.Result[0]);
                        Assert.AreEqual("45\"6|789", elementData.Result[1]);
                        Assert.AreEqual("0", elementData.Result[2]);
                        break;
                    case SplitOption.Pipe:
                        Assert.AreEqual("123,45\"6", elementData.Result[0]);
                        Assert.AreEqual("789,0", elementData.Result[1]);
                        break;
                    case SplitOption.CommaMaxLengthThree:
                        Assert.AreEqual("123", elementData.Result[0]);
                        Assert.AreEqual("45\"", elementData.Result[1]);
                        Assert.AreEqual("6|7", elementData.Result[2]);
                        Assert.AreEqual("89", elementData.Result[3]);
                        Assert.AreEqual("0", elementData.Result[4]);
                        break;
                    case SplitOption.CommaAndPipe:
                        Assert.AreEqual("123", elementData.Result[0]);
                        Assert.AreEqual("45\"6", elementData.Result[1]);
                        Assert.AreEqual("789", elementData.Result[2]);
                        Assert.AreEqual("0", elementData.Result[3]);
                        break;
                    case SplitOption.PipeAndQuote:
                        Assert.AreEqual("123,45", elementData.Result[0]);
                        Assert.AreEqual("6", elementData.Result[1]);
                        Assert.AreEqual("789,0", elementData.Result[2]);
                        break;
                    default:
                        break;
                }
            }
        }

        private void VerifyMultiplyByElementPipeline(PipelineOptions options)
        {
            // Pass the configuration to the builder to create the pipeline.
            var pipeline = _builder.BuildFromConfiguration(options);
            var element = pipeline.GetElement<MultiplyByElement>();

            // Check we've got the expected number of evidence keys.
            Assert.AreEqual(1, element.EvidenceKeys.Count);

            // Create, populate and process flow data.
            using (var flowData = pipeline.CreateFlowData())
            {
                flowData.AddEvidence(element.EvidenceKeys[0], 5)
                    .Process();

                // Get the result and verify it.
                var elementData = flowData.GetFromElement(element);
                Assert.AreEqual(40, elementData.Result);
            }
        }
    }
}
