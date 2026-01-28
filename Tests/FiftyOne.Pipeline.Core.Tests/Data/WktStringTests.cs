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

using System;
using System.Collections.Generic;
using FiftyOne.Pipeline.Core.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FiftyOne.Pipeline.Core.Tests.Data;

[TestClass]
public class WktStringTests
{
    private const string WktPoint = "POINT(2 4)";
    private const string WktPolygon = "POLYGON((10 10,10 20,20 20,20 15,10 10))";

    private static IEnumerable<string[]> RawStrings => new[]
    {
        new[] { WktPoint, },
        new[] { WktPolygon, },
    };
    
    [TestMethod]
    [DynamicData(nameof(RawStrings))]
    public void WktString_Value_NewImplicit(string rawString)
    {
        WktString wktString = rawString;
        Assert.AreEqual(rawString, wktString.Value);
    }
    
    [TestMethod]
    [DynamicData(nameof(RawStrings))]
    public void WktString_Value_NewExplicit(string rawString)
    {
        WktString wktString = new WktString(rawString);
        Assert.AreEqual(rawString, wktString.Value);
    }
    
    [TestMethod]
    [DynamicData(nameof(RawStrings))]
    public void WktString_GetHashCode(string rawString)
    {
        WktString wktString = rawString;
        Assert.AreEqual(rawString.GetHashCode(), wktString.GetHashCode());
    }
    
    [TestMethod]
    [DynamicData(nameof(RawStrings))]
    public void WktString_ToString_Method(string rawString)
    {
        WktString wktString = rawString;
        string theString = wktString.ToString();
        Assert.AreEqual(rawString, theString);
    }
    
    [TestMethod]
    [DynamicData(nameof(RawStrings))]
    public void WktString_ToString_Implicit(string rawString)
    {
        WktString wktString = rawString;
        string theString = wktString;
        Assert.AreEqual(rawString, theString);
    }
    
    [TestMethod]
    [DynamicData(nameof(RawStrings))]
    public void WktString_ToString_Equals_WktExplicit(string rawString)
    {
        WktString wktString = rawString;
        WktString wktString2 = rawString;
        Assert.IsTrue(wktString.Equals(wktString2));
        Assert.IsTrue(wktString2.Equals(wktString));
    }
    
    [TestMethod]
    [DynamicData(nameof(RawStrings))]
    public void WktString_ToString_Equals_StringExplicit(string rawString)
    {
        WktString wktString = rawString;
        Assert.IsTrue(wktString.Equals(rawString));
    }

    private static IEnumerable<object[]> EqualityTestData =>
    [
        [WktPoint, typeof(string), true],
        [new WktString(WktPoint), typeof(WktString), true],
        [WktPolygon, typeof(string), false],
        [new WktString(WktPolygon), typeof(WktString), false],
    ];
    
    [TestMethod]
    [DynamicData(nameof(EqualityTestData))]
    public void WktString_ToString_Equals_ObjectMethod(object obj, Type type, bool expected)
    {
        WktString wktString = WktPoint;
        Assert.AreEqual(type, obj.GetType());
        Assert.AreEqual(expected, wktString.Equals(obj));
    }
    
    [TestMethod]
    public void WktString_ToString_Equals_WktOperator()
    {
        WktString wktString = WktPoint;
        WktString wktString2 = WktPoint;
        WktString wktString3 = WktPolygon;
        
        Assert.IsTrue(wktString == wktString2);
        Assert.IsTrue(wktString2 == wktString);
        Assert.IsFalse(wktString != wktString2);
        Assert.IsFalse(wktString2 != wktString);
        
        Assert.IsFalse(wktString == wktString3);
        Assert.IsFalse(wktString3 == wktString);
        Assert.IsTrue(wktString != wktString3);
        Assert.IsTrue(wktString3 != wktString);
    }
    
    [TestMethod]
    public void WktString_ToString_Equals_StringOperator()
    {
        WktString wktString = WktPoint;
        
        Assert.IsTrue(wktString == WktPoint);
        Assert.IsTrue(WktPoint == wktString);
        Assert.IsFalse(wktString != WktPoint);
        Assert.IsFalse(WktPoint != wktString);
        
        Assert.IsFalse(wktString == WktPolygon);
        Assert.IsFalse(WktPolygon == wktString);
        Assert.IsTrue(wktString != WktPolygon);
        Assert.IsTrue(WktPolygon != wktString);
    }
}