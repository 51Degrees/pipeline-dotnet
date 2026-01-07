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

using FiftyOne.Pipeline.Core.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FiftyOne.Pipeline.Core.Tests.Data;

[TestClass]
public class WeightedValueTests
{
    [TestMethod]
    public void WeightedValue_Weighting_1()
    {
        IWeightedValue<string> value = new WeightedValue<string>(
            ushort.MaxValue, "the only value");
        Assert.AreEqual("the only value", value.Value);
        Assert.AreEqual(1, value.Weighting());
    }
    
    [TestMethod]
    public void WeightedValue_Weighting_05()
    {
        WeightedValue<int>[] values =
        {
            new(ushort.MaxValue / 2 + 1, 5),
            new(ushort.MaxValue / 2, 13),
        };
        Assert.AreEqual(5, values[0].Value);
        Assert.AreEqual(13, values[1].Value);
        Assert.AreEqual(0.5f + 0.5f / ushort.MaxValue, values[0].Weighting());
        Assert.AreEqual(0.5f - 0.5f / ushort.MaxValue, values[1].Weighting());
    }
}