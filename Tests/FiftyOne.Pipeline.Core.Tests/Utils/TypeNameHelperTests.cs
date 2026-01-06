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
using System.Collections;
using System.Collections.Generic;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static FiftyOne.Pipeline.Core.Utils.TypeNameHelper;

namespace FiftyOne.Pipeline.Core.Tests.Utils;

[TestClass]
public class TypeNameHelperTests
{
    [TestMethod]
    public void TypeNameHelper_GetPrettyTypeName_Int()
    {
        Type t = typeof(int);
        Assert.AreEqual("Int32", GetPrettyTypeName(t));
        Assert.AreEqual(t.Name, GetPrettyTypeName(t));
    }
    
    [TestMethod]
    public void TypeNameHelper_GetPrettyTypeName_String()
    {
        Type t = typeof(string);
        Assert.AreEqual("String", GetPrettyTypeName(t));
        Assert.AreEqual(t.Name, GetPrettyTypeName(t));
    }
    
    [TestMethod]
    public void TypeNameHelper_GetPrettyTypeName_IEnumerable()
    {
        Type t = typeof(IEnumerable);
        Assert.AreEqual("IEnumerable", GetPrettyTypeName(t));
        Assert.AreEqual(t.Name, GetPrettyTypeName(t));
    }
    
    [TestMethod]
    public void TypeNameHelper_GetPrettyTypeName_IEnumerable_String()
    {
        Type t = typeof(IEnumerable<string>);
        Assert.AreEqual("IEnumerable<String>", GetPrettyTypeName(t));
        Assert.AreEqual($"{nameof(IEnumerable<string>)}<{typeof(string).Name}>", GetPrettyTypeName(t));
    }
    
    [TestMethod]
    public void TypeNameHelper_GetPrettyTypeName_IDictionary_IReadOnlyList()
    {
        Type t = typeof(IDictionary<float, IReadOnlyList<IDisposable>>);
        Assert.AreEqual("IDictionary<Single, IReadOnlyList<IDisposable>>", GetPrettyTypeName(t));
        Assert.AreEqual(
            (nameof(IDictionary<float, IReadOnlyList<IDisposable>>)
             + "<"
             + typeof(float).Name
             + ", "
             + nameof(IReadOnlyList<IDisposable>)
             + "<"
             + nameof(IDisposable)
             + ">>"),
            GetPrettyTypeName(t));
    }
    
    [TestMethod]
    public void TypeNameHelper_GetPrettyTypeName_IAspectPropertyValue_String()
    {
        Type t = typeof(IAspectPropertyValue<string>);
        Assert.AreEqual(
            (nameof(IAspectPropertyValue<string>)
             + "<"
             + typeof(string).Name
             + ">"),
            GetPrettyTypeName(t));
    }
    
    [TestMethod]
    public void TypeNameHelper_GetPrettyTypeName_Func3()
    {
        Type t = typeof(
            Func<
                KeyValuePair<string, float>,
                IDictionary<
                    IEquatable<string>,
                    IEnumerable<IAspectPropertyValue<IReadOnlyList<int>>>
                >,
                double
            >);
        Assert.AreEqual(
            ("Func<KeyValuePair<String, Single>, IDictionary<IEquatable<String>, "
             + $"IEnumerable<{nameof(IAspectPropertyValue)}<IReadOnlyList<Int32>>>>, Double>"),
            GetPrettyTypeName(t));
    }
}