/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2023 51 Degrees Mobile Experts Limited, Davidson House,
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.Engines.FiftyOne.Data;
using System.Linq;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Tests.FlowElements
{
    [TestClass]
    public class SetHeadersElementTests
    {
        // These inner classes are used to create stubs for testing against.
        #region inner classes
        private class SetHeadersSourceData : ElementDataBase
        {
            public SetHeadersSourceData(ILogger<ElementDataBase> logger, IPipeline pipeline) : base(logger, pipeline)
            {
            }
        }

        private class ActivePropertySourceElement : FlowElementBase<SetHeadersSourceData, IElementPropertyMetaData>
        {
            private Dictionary<string, object> _propertyNameValuesToReturn;

            public ActivePropertySourceElement(
                ILogger<FlowElementBase<SetHeadersSourceData, IElementPropertyMetaData>> logger,
                Func<IPipeline, FlowElementBase<SetHeadersSourceData, IElementPropertyMetaData>, SetHeadersSourceData> elementDataFactory,
                Dictionary<string, object> propertyNameValuesToReturn) 
                : base(logger, elementDataFactory)
            {
                _propertyNameValuesToReturn = propertyNameValuesToReturn;
            }

            public override string ElementDataKey => "setheaderssourceelement";

            public override IEvidenceKeyFilter EvidenceKeyFilter => new EvidenceKeyFilterWhitelist(new List<string>());

            public override IList<IElementPropertyMetaData> Properties => _propertyNameValuesToReturn
                .Select(p => new ElementPropertyMetaData(this, p.Key, typeof(object), true))
                .Cast<IElementPropertyMetaData>()
                .ToList();

            protected override void ManagedResourcesCleanup()
            {
            }

            protected override void ProcessInternal(IFlowData data)
            {
                var sourceData = data.GetOrAdd(ElementDataKey, p => CreateElementData(p));
                sourceData.PopulateFrom(_propertyNameValuesToReturn);
            }

            protected override void UnmanagedResourcesCleanup()
            {
            }
        }
        #endregion 

        private SetHeadersElement _element;
        private ActivePropertySourceElement _sourceElement;
        private TestLoggerFactory _loggerFactory;
        private IPipeline _pipeline;

        [TestInitialize]
        public void Init()
        {
            _loggerFactory = new TestLoggerFactory();
        }

        /// <summary>
        /// Helper method to create the flow elements and configure the pipeline
        /// </summary>
        private void CreatePipeline(
            Dictionary<string, object> propertyNameValues)
        {
            _sourceElement = new ActivePropertySourceElement(
                _loggerFactory.CreateLogger<ActivePropertySourceElement>(),
                (IPipeline pipeline, FlowElementBase<SetHeadersSourceData, IElementPropertyMetaData> element) =>
                {
                    return new SetHeadersSourceData(_loggerFactory.CreateLogger<SetHeadersSourceData>(), pipeline);
                },
                propertyNameValues);

            _element = new SetHeadersElement(
                _loggerFactory.CreateLogger<SetHeadersElement>(),
                (IPipeline pipeline, FlowElementBase<ISetHeadersData, IElementPropertyMetaData> element) =>
                {
                    return new SetHeadersData(_loggerFactory.CreateLogger<SetHeadersData>(), pipeline);
                });

            _pipeline = new PipelineBuilder(_loggerFactory)
                .AddFlowElement(_sourceElement)
                .AddFlowElement(_element)
                .Build();
        }

        /// <summary>
        /// The 'SetHeaderAcceptCH' property contains JSON for a single
        /// header.
        /// Output from SetHeadersElement should contain the expected 
        /// header value.
        /// </summary>
        [DataTestMethod]
        // Output should be the same whether value is wrapped in 
        // AspectPropertyValue or not.
        [DataRow(true)]
        [DataRow(false)]
        public void SetHeadersElement(bool valueIsAPV)
        {
            var valueStr = "UA-Platform";
            object value = valueStr;
            if (valueIsAPV)
            {
                value = new AspectPropertyValue<string>(valueStr);
            }

            var propertyNameValues = new Dictionary<string, object>() 
            {
                { "SetHeaderBrowserAccept-CH", value }
            };
            CreatePipeline(propertyNameValues);            
            var data = _pipeline.CreateFlowData();
            data.Process();

            // Verify the output
            var typedOutput = GetFromFlowData(data);
            Assert.AreEqual(1, typedOutput.ResponseHeaderDictionary.Count);
            Assert.IsTrue(typedOutput.ResponseHeaderDictionary.ContainsKey("Accept-CH"));
            Assert.AreEqual("UA-Platform", typedOutput.ResponseHeaderDictionary["Accept-CH"]);
        }

        /// <summary>
        /// The 'SetHeaderAcceptCH' property is set to various invalid values.
        /// Output from SetHeadersElement should be an empty dictionary.
        /// </summary>
        [DataTestMethod]
        [DataRow(null)]
        [DataRow(123)]
        [DataRow("Unknown")]
        public void SetHeadersElement_InvalidPropertyValues(object sourcePropertyValue)
        {
            var propertyNameValues = new Dictionary<string, object>()
            {
                { "SetHeaderBrowserAccept-CH", sourcePropertyValue }
            };
            CreatePipeline(propertyNameValues);
            var data = _pipeline.CreateFlowData();
            data.Process();

            // Verify the output
            var typedOutput = GetFromFlowData(data);
            Assert.AreEqual(0, typedOutput.ResponseHeaderDictionary.Count);
        }

        /// <summary>
        /// The 'SetHeader*' property is set to various invalid values
        /// that are wrapped in an AspectPropertyValue.
        /// Output from SetHeadersElement should be an empty dictionary.
        /// </summary>
        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true, null)]
        public void SetHeadersElement_APV_InvalidPropertyValues(bool hasValue, string sourcePropertyValue = null)
        {
            var value = new AspectPropertyValue<string>();
            if (hasValue)
            {
                value.Value = sourcePropertyValue;
            }

            var propertyNameValues = new Dictionary<string, object>()
            {
                { "SetHeaderBrowserAccept-CH", value }
            };
            CreatePipeline(propertyNameValues);
            var data = _pipeline.CreateFlowData();
            data.Process();

            // Verify the output
            var typedOutput = GetFromFlowData(data);
            Assert.AreEqual(0, typedOutput.ResponseHeaderDictionary.Count);
        }

        /// <summary>
        /// Test that various invalid property names cause 
        /// an exception to be thrown
        /// </summary>
        [DataTestMethod]
        [DataRow("SetHeader")]
        [DataRow("SetHeaderBrowser")]
        [ExpectedException(typeof(AggregateException))]
        public void SetHeadersElement_InvalidPropertyNames(string sourcePropertyName)
        {
            var propertyNameValues = new Dictionary<string, object>()
            {
                { sourcePropertyName, "TEST" }
            };
            CreatePipeline(propertyNameValues);
            var data = _pipeline.CreateFlowData();
            data.Process();
        }


        /// <summary>
        /// Test that multiple properties will result in multiple headers
        /// being set in the response.
        /// </summary>
        [TestMethod]
        public void SetHeadersElement_MultipleProperties()
        {
            var propertyNameValues = new Dictionary<string, object>()
            {
                { "SetHeaderBrowserAccept-CH", "Sec-CH-UA" },
                { "SetHeaderHardwareCritical-CH", "Sec-CH-UA-Model,Sec-CH-UA-Mobile" }
            };
            CreatePipeline(propertyNameValues);
            var data = _pipeline.CreateFlowData();
            data.Process();

            var typedOutput = GetFromFlowData(data);
            Assert.AreEqual(2, typedOutput.ResponseHeaderDictionary.Count);
            Assert.IsTrue(typedOutput.ResponseHeaderDictionary.ContainsKey("Accept-CH"));
            Assert.IsTrue(typedOutput.ResponseHeaderDictionary.ContainsKey("Critical-CH"));
            Assert.AreEqual("Sec-CH-UA",
                typedOutput.ResponseHeaderDictionary["Accept-CH"]);
            Assert.AreEqual("Sec-CH-UA-Model,Sec-CH-UA-Mobile",
                typedOutput.ResponseHeaderDictionary["Critical-CH"]);
        }

        /// <summary>
        /// Test that the SetHeadersElement will combine values from 
        /// multiple properties that are associated with the same header.
        /// </summary>
        [TestMethod]
        public void SetHeadersElement_MultipleProperties_SameHeader()
        {
            var propertyNameValues = new Dictionary<string, object>()
            {
                { "SetHeaderBrowserAccept-CH", "Sec-CH-UA" },
                { "SetHeaderHardwareAccept-CH", "Sec-CH-UA-Model,Sec-CH-UA-Mobile" }
            };
            CreatePipeline(propertyNameValues);
            var data = _pipeline.CreateFlowData();
            data.Process();

            var typedOutput = GetFromFlowData(data);
            Assert.AreEqual(1, typedOutput.ResponseHeaderDictionary.Count);
            Assert.IsTrue(typedOutput.ResponseHeaderDictionary.ContainsKey("Accept-CH"));
            Assert.AreEqual("Sec-CH-UA,Sec-CH-UA-Model,Sec-CH-UA-Mobile", 
                typedOutput.ResponseHeaderDictionary["Accept-CH"]);
        }

        /// <summary>
        /// Test that the SetHeadersElement will remove duplicate values from multiple properties 
        /// that are associated with the same header.
        /// I.e. there is only one 'Sec-CH-UA-Model' in the output, despite it appearing in both
        /// property values below.
        /// </summary>
        [TestMethod]
        public void SetHeadersElement_MultipleProperties_DuplicateValues()
        {
            var propertyNameValues = new Dictionary<string, object>()
            {
                { "SetHeaderBrowserAccept-CH", "Sec-CH-UA,Sec-CH-UA-Model" },
                { "SetHeaderHardwareAccept-CH", "Sec-CH-UA-Model,Sec-CH-UA-Mobile" }
            };
            CreatePipeline(propertyNameValues);
            var data = _pipeline.CreateFlowData();
            data.Process();

            var typedOutput = GetFromFlowData(data);
            Assert.AreEqual(1, typedOutput.ResponseHeaderDictionary.Count);
            Assert.IsTrue(typedOutput.ResponseHeaderDictionary.ContainsKey("Accept-CH"));
            Assert.AreEqual("Sec-CH-UA,Sec-CH-UA-Model,Sec-CH-UA-Mobile",
                typedOutput.ResponseHeaderDictionary["Accept-CH"]);
        }

        private SetHeadersData GetFromFlowData(IFlowData data)
        {
            var output = data.ElementDataAsDictionary();
            var elementOutput = output[_element.ElementDataKey];
            Assert.IsNotNull(elementOutput);
            Assert.IsInstanceOfType(elementOutput, typeof(SetHeadersData));
            var typedOutput = elementOutput as SetHeadersData;
            Assert.IsNotNull(typedOutput.ResponseHeaderDictionary);
            return typedOutput;
        }
    }
}
