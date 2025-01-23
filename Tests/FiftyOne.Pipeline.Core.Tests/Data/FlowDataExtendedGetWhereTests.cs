using Microsoft.VisualStudio.TestTools.UnitTesting;
using FiftyOne.Pipeline.Core.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FiftyOne.Pipeline.Core.FlowElements;
using Moq;
using Microsoft.Extensions.Logging;
using FiftyOne.Pipeline.Core.Tests.HelperClasses;
using System.Threading;
using FiftyOne.Pipeline.Core.TypedMap;
using System.Xml.Linq;

namespace FiftyOne.Pipeline.Core.Tests.Data;

/// <summary>
/// A seperate testing class that tests
/// </summary>
[TestClass]
public class FlowDataExtendedGetWhereTests
{
    private Mock<ILogger<ParallelElements>> _logger;
    private ILoggerFactory _loggerFactory;

    [TestInitialize]
    public void Initialize()
    {
        _logger = new Mock<ILogger<ParallelElements>>();
        _loggerFactory = new LoggerFactory();
    }

    /// <summary>
    /// Tests that the <see cref="FlowData.
    /// GetWhere(Func{IElementPropertyMetaData, bool})"/>
    /// returns all elements, inlcuding sub elements that are part of 
    /// Parallel elements. 
    /// </summary>
    [TestMethod]
    public void FlowData_GetWhere_ParallelElement()
    {
        // Arrange
        var element1 = GetMockFlowElement("element1");
        var element2 = GetMockFlowElement("element2");
        var element3 = GetMockFlowElement("element3");
        var element4 = GetMockFlowElement("element4");
        SetUpElement(element1, "element1");
        SetUpElement(element2, "element2");
        SetUpElement(element3, "element3");
        SetUpElement(element4, "element4");

        var parallelElement = new ParallelElements(
            _logger.Object,
            element1.Object,
            element2.Object,
            element3.Object);

        var pipeline = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(parallelElement)
            .AddFlowElement(element4.Object)
            .Build();
        var flowData = pipeline.CreateFlowData();
        flowData.Process();

        // Act
        var elements = flowData.GetWhere(i => 
        i.Element.ElementDataKey.Equals("element1") ||
        i.Element.ElementDataKey.Equals("element2") ||
        i.Element.ElementDataKey.Equals("element3") ||
        i.Element.ElementDataKey.Equals("element4")
        );

        // Build list for testing 
        var listOfTestedElements = new List<IFlowElement>()
        {
            element1.Object,
            element2.Object,
            element3.Object,
            element4.Object
        };

        // Assert
        // Assert that the amount of elements returned is the same 
        // as the amount added
        Assert.AreEqual(pipeline.FlowElements.Count, elements.Count());

        // Collect all testing values
        var testingValues = listOfTestedElements.Select(element =>
        {
            var elementName = element.ElementDataKey;
            var elementData = flowData.Get(element.ElementDataKey);
            var elementDataValue = elementData
            .AsDictionary()
            .Keys
            .First();
            return $"{elementName}.{elementDataValue}";
        }).ToList();

        var elementKeys = elements.ToDictionary().Keys;

        // Assert that the collected tested elements' keys are equal to
        // the keys from elements
        CollectionAssert.AreEquivalent(
            testingValues.ToArray(),
            elementKeys.ToArray(),
            "The elements lists are not the same.");

    }

    /// <summary>
    /// Tests that the <see cref="FlowData.
    /// GetWhere(Func{IElementPropertyMetaData, bool})"/>
    /// returns all elements, inlcuding sub elements that are part of 
    /// Parallel elements. 
    /// </summary>
    [TestMethod]
    public void FlowData_GetWhere_NestedParallelElements()
    {
        // Arrange
        var element1 = GetMockFlowElement("element1");
        var element2 = GetMockFlowElement("element2");
        var element3 = GetMockFlowElement("element3");
        var element4 = GetMockFlowElement("element4");
        SetUpElement(element1, "element1");
        SetUpElement(element2, "element2");
        SetUpElement(element3, "element3");
        SetUpElement(element4, "element4");

        var nestedParallelElement = new ParallelElements(
            _logger.Object,
            element2.Object,
            element3.Object);

        var parallelElement = new ParallelElements(
            _logger.Object,
            element1.Object,
            nestedParallelElement);

        var pipeline = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(parallelElement)
            .AddFlowElement(element4.Object)
            .Build();
        var flowData = pipeline.CreateFlowData();
        flowData.Process();

        // Act
        var elements = flowData.GetWhere(i =>
        i.Element.ElementDataKey.Equals("element1") ||
        i.Element.ElementDataKey.Equals("element2") ||
        i.Element.ElementDataKey.Equals("element3") ||
        i.Element.ElementDataKey.Equals("element4")
        );

        // Build list for testing 
        var listOfTestedElements = new List<IFlowElement>()
        {
            element1.Object,
            element2.Object,
            element3.Object,
            element4.Object
        };

        // Assert
        // assert that the amount of elements returned is the same 
        // as the amount added
        Assert.AreEqual(pipeline.FlowElements.Count, elements.Count());

        // Collect all testing values
        var testingValues = listOfTestedElements.Select(element =>
        {
            var elementName = element.ElementDataKey;
            var elementData = flowData.Get(element.ElementDataKey);
            var elementDataValue = elementData
            .AsDictionary()
            .Keys
            .First();
            return $"{elementName}.{elementDataValue}";
        }).ToList();

        var elementKeys = elements.ToDictionary().Keys;

        // Assert that the collected tested elements' keys are equal to
        // the keys from elements
        CollectionAssert.AreEquivalent(
            testingValues.ToArray(),
            elementKeys.ToArray(),
            "The elements lists are not the same.");
    }

    /// <summary>
    /// Tests that the <see cref="FlowData.
    /// GetWhere(Func{IElementPropertyMetaData, bool})"/>
    /// returns all sub elements associated with parallel elements.
    /// </summary>
    [TestMethod]
    public void FlowData_GetWhere_MutipleParallelElements()
    {
        // Arrange
        var element1 = GetMockFlowElement("element1");
        var element2 = GetMockFlowElement("element2");
        var element3 = GetMockFlowElement("element3");
        var element4 = GetMockFlowElement("element4");
        SetUpElement(element1, "element1");
        SetUpElement(element2, "element2");
        SetUpElement(element3, "element3");
        SetUpElement(element4, "element4");

        var parallelElement = new ParallelElements(
            _logger.Object,
            element1.Object,
            element2.Object);

        var parallelElement2 = new ParallelElements(
            _logger.Object,
            element3.Object,
            element4.Object);

        var pipeline = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(parallelElement)
            .AddFlowElement(parallelElement2)
            .Build();
        var flowData = pipeline.CreateFlowData();
        flowData.Process();

        // Act
        var elements = flowData.GetWhere(i => 
        i.Element.ElementDataKey.Equals("element1") ||
        i.Element.ElementDataKey.Equals("element2") ||
        i.Element.ElementDataKey.Equals("element3") ||
        i.Element.ElementDataKey.Equals("element4")
        );

        // Build list for testing 
        var listOfTestedElements = new List<IFlowElement>()
        {
            element1.Object,
            element2.Object,
            element3.Object,
            element4.Object
        };

        // Assert
        // assert that the amount of elements returned is the same 
        // as the amount added
        Assert.AreEqual(pipeline.FlowElements.Count, elements.Count());

        // Collect all testing values
        var testingValues = listOfTestedElements.Select(element =>
        {
            var elementName = element.ElementDataKey;
            var elementData = flowData.Get(element.ElementDataKey);
            var elementDataValue = elementData
            .AsDictionary()
            .Keys
            .First();
            return $"{elementName}.{elementDataValue}";
        }).ToList();

        var elementKeys = elements.ToDictionary().Keys;

        // Assert that the collected tested elements' keys are equal to
        // the keys from elements
        CollectionAssert.AreEquivalent(
            testingValues.ToArray(),
            elementKeys.ToArray(),
            "The elements lists are not the same.");
    }
    
    /// <summary>
    /// Tests that the <see cref="FlowData.
    /// GetWhere(Func{IElementPropertyMetaData, bool})"/>
    /// returns all elements, including parallel elements
    /// and elemnts without an element data key.
    /// </summary>
    [TestMethod]
    public void FlowData_GetWhere_NoElements()
    {
        // Arrange
       // Add no elements to the pipeline

        var pipeline = new PipelineBuilder(_loggerFactory)
            .Build();
        var flowData = pipeline.CreateFlowData();
        flowData.Process();

        // Act
        var elements = flowData.GetWhere(i => i == i);

        // Assert
        Assert.IsTrue(elements.Count() == 0);
        Assert.AreEqual(pipeline.FlowElements.Count, elements.Count());
    }
    
    /// <summary>
    /// Tests that the Pipeline.FlowElements
    /// returns all public elements, including sub elements.
    /// </summary>
    [TestMethod]
    public void FlowData_AllFlowElementsReturned()
    {
        // Arrange
        var element1 = GetMockFlowElement("element1");
        var element2 = GetMockFlowElement("element2");
        var element3 = GetMockFlowElement("element3");
        var element4 = GetMockFlowElement("element4");
        var element5 = GetMockFlowElement("element4");
        SetUpElement(element1, "element1");
        SetUpElement(element2, "element2");
        SetUpElement(element3, "element3");
        SetUpElement(element4, "element4");
        SetUpElement(element5, "element4");

        var parallelElement = new ParallelElements(
            _logger.Object,
            element2.Object,
            element3.Object,
            element4.Object);

        var pipeline = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(element1.Object)
            .AddFlowElement(parallelElement)
            .AddFlowElement(element5.Object)
            .Build();
        var flowData = pipeline.CreateFlowData();

        // Build list for testing 
        var listOfTestedElements = new List<IFlowElement>()
        {
            element1.Object,
            element2.Object,
            element3.Object,
            element4.Object,
            element5.Object
        };

        // Act
        var elements = flowData.Pipeline.FlowElements;

        // Assert
        // assert that the amount of elements returned is the same 
        // as the amount added
        Assert.AreEqual(5, elements.Count);
        CollectionAssert.AreEquivalent(listOfTestedElements,
            elements.ToArray());
    }

    /// <summary>
    /// Sets up the mocked element with elementdata. It is important for 
    /// the test that element data be populated with data for the 
    /// <see cref="FlowData.GetWhere(Func{IElementPropertyMetaData, bool})"/>
    /// funtion to work.
    /// </summary>
    /// <param name="element"></param>
    /// <param name="name"></param>
    private static void SetUpElement(
        Mock<IFlowElement> element,
        string name)
    {
        element
        .Setup(e => e.Process(It.IsAny<IFlowData>()))
        .Callback((IFlowData d) =>
        {
            var tempdata = d.GetOrAdd(name, (p) => 
            new TestElementData(p, new Dictionary<string, object>()
            {
                { name, "value" }
            }));
        });
    }

    /// <summary>
    /// Creates and returns a MockFlowElement
    /// </summary>
    /// <returns></returns>
    private static Mock<IFlowElement> GetMockFlowElement(
        string elementDataKey)
    {
        var element = new Mock<IFlowElement>();
        element.SetupGet(e => e.ElementDataKey)
            .Returns(elementDataKey);
        element.Setup(e => e.Properties)
            .Returns(
            [
                new ElementPropertyMetaData(element.Object,
                elementDataKey,
                typeof(string),
                true)
            ]);
       
        return element;
    }
}
