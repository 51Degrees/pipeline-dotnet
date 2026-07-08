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

using FiftyOne.Pipeline.Engines.Data.Readers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FiftyOne.Pipeline.Engines.Tests.Data.Readers;

/// <summary>
/// Tests for <see cref="JsonLoader{T}"/>. These pin down the deserialization
/// behaviour that callers relied on under Newtonsoft.Json so that the
/// System.Text.Json implementation stays compatible: case-insensitive
/// property matching, lenient number/enum parsing and null handling, loaded
/// from both a file and a stream.
/// </summary>
[TestClass]
public class JsonLoaderTests
{
    public enum Colour { Unknown, Red, Green }

    public class TestModel
    {
        public string Name { get; set; }
        public int Count { get; set; }
        public Colour Colour { get; set; }
        public List<string> Tags { get; set; }
        public TestModel Child { get; set; }
    }

    private static MemoryStream ToStream(string json) =>
        new(Encoding.UTF8.GetBytes(json));

    /// <summary>
    /// Loading from a stream returns a fully populated instance, including
    /// nested objects and collections.
    /// </summary>
    [TestMethod]
    public void JsonLoader_LoadFromStream_NestedAndCollections()
    {
        var json =
            @"{ ""Name"": ""root"", ""Count"": 2, ""Tags"": [ ""a"", ""b"" ],
                    ""Child"": { ""Name"": ""leaf"", ""Count"": 5 } }";

        var result = new JsonLoader<TestModel>().LoadData(ToStream(json));

        Assert.IsNotNull(result);
        Assert.AreEqual("root", result.Name);
        Assert.AreEqual(2, result.Count);
        CollectionAssert.AreEqual(new[] { "a", "b" }, result.Tags);
        Assert.IsNotNull(result.Child);
        Assert.AreEqual("leaf", result.Child.Name);
        Assert.AreEqual(5, result.Child.Count);
    }

    /// <summary>
    /// Loading from a file path produces the same result as from a stream.
    /// </summary>
    [TestMethod]
    public void JsonLoader_LoadFromFile()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, @"{ ""Name"": ""fromfile"", ""Count"": 7 }");

            var result = new JsonLoader<TestModel>().LoadData(path);

            Assert.IsNotNull(result);
            Assert.AreEqual("fromfile", result.Name);
            Assert.AreEqual(7, result.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Property matching must be case-insensitive, matching Newtonsoft's
    /// default. The JSON here uses lower-case keys for Pascal-case members.
    /// </summary>
    [TestMethod]
    public void JsonLoader_PropertyMatchingIsCaseInsensitive()
    {
        var json = @"{ ""name"": ""lower"", ""count"": 9 }";

        var result = new JsonLoader<TestModel>().LoadData(ToStream(json));

        Assert.AreEqual("lower", result.Name);
        Assert.AreEqual(9, result.Count);
    }

    /// <summary>
    /// Enums must be readable from their string name, matching Newtonsoft.
    /// </summary>
    [TestMethod]
    public void JsonLoader_EnumFromStringName()
    {
        var json = @"{ ""Colour"": ""Green"" }";

        var result = new JsonLoader<TestModel>().LoadData(ToStream(json));

        Assert.AreEqual(Colour.Green, result.Colour);
    }

    /// <summary>
    /// Numbers encoded as strings must still parse into numeric members,
    /// matching Newtonsoft's lenient coercion.
    /// </summary>
    [TestMethod]
    public void JsonLoader_NumberFromString()
    {
        var json = @"{ ""Count"": ""42"" }";

        var result = new JsonLoader<TestModel>().LoadData(ToStream(json));

        Assert.AreEqual(42, result.Count);
    }

    /// <summary>
    /// The literal JSON value "null" deserializes to a null reference, the
    /// same as <c>JsonConvert.DeserializeObject</c> did.
    /// </summary>
    [TestMethod]
    public void JsonLoader_NullLiteral_ReturnsNull()
    {
        var result = new JsonLoader<TestModel>().LoadData(ToStream("null"));

        Assert.IsNull(result);
    }
}
