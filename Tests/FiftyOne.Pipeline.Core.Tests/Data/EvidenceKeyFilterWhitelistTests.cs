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
using System;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.Pipeline.Core.Tests.Data
{
    [TestClass]
    public class EvidenceKeyFilterWhitelistTests
    {
        private EvidenceKeyFilterWhitelist _filter;

        /// <summary>
        /// Check that the Include method works as expected for a simple 
        /// white list filter.
        /// </summary>
        [TestMethod]
        public void EvidenceKeyFilterWhitelist_Include_CheckKeys()
        {
            _filter = new EvidenceKeyFilterWhitelist(new List<string>() { "key1", "key2" });

            Assert.IsTrue(_filter.Include("key1"));
            Assert.IsTrue(_filter.Include("key2"));
            Assert.IsFalse(_filter.Include("key3"));
        }

        /// <summary>
        /// Check that the List property works as expected for a simple
        /// white list filter.
        /// </summary>
        [TestMethod]
        public void EvidenceKeyFilterWhitelist_List()
        {
            _filter = new EvidenceKeyFilterWhitelist(new List<string>() { "key1", "key2" });

            var result = _filter.Whitelist;

            Assert.HasCount(2, result);
            Assert.IsTrue(result.Keys.Contains("key1"));
            Assert.IsTrue(result.Keys.Contains("key2"));
        }

        /// <summary>
        /// Check that filter is case-insensitive by default.
        /// </summary>
        [TestMethod]
        public void EvidenceKeyFilterWhitelist_CaseInsensitive()
        {
            _filter = new EvidenceKeyFilterWhitelist(
                new List<string>() { "key1" });

            Assert.IsTrue(_filter.Include("key1"));
            Assert.IsTrue(_filter.Include("Key1"));
            Assert.IsTrue(_filter.Include("KEY1"));
        }

        /// <summary>
        /// Check that case-sensitive comparison with a custom comparer 
        /// works as expected.
        /// </summary>
        [TestMethod]
        public void EvidenceKeyFilterWhitelist_CaseSensitive()
        {
            _filter = new EvidenceKeyFilterWhitelist(
                new List<string>() { "key1" }, 
                StringComparer.Ordinal);

            Assert.IsTrue(_filter.Include("key1"));
            Assert.IsFalse(_filter.Include("Key1"));
            Assert.IsFalse(_filter.Include("KEY1"));
        }

        /// <summary>
        /// Two capitalisations of one key are one key to a case-insensitive
        /// filter, so building one from both must not throw.
        /// </summary>
        /// <remarks>Regression test for pipeline-dotnet#393.</remarks>
        [TestMethod]
        public void EvidenceKeyFilterWhitelist_List_KeysDifferingInCase()
        {
            var combined = new List<string>() { "header.Host" }
                .Concat(new List<string>() { "header.host" })
                .Distinct(StringComparer.Ordinal)
                .ToList();

            _filter = new EvidenceKeyFilterWhitelist(combined);

            Assert.HasCount(1, _filter.Whitelist);
            Assert.IsTrue(
                _filter.Whitelist.Keys.Contains(
                    "header.Host", StringComparer.Ordinal),
                "The first spelling seen is the one kept.");
            Assert.IsTrue(_filter.Include("header.host"));
            Assert.IsTrue(_filter.Include("header.Host"));
            Assert.AreEqual(0, _filter.Order("header.host"));
        }

        /// <summary>
        /// When two capitalisations of one key carry different orders of
        /// precedence, the higher precedence (the lower value) is kept,
        /// whichever spelling it came with.
        /// </summary>
        /// <remarks>
        /// The spelling kept is not checked: dictionary enumeration order is
        /// not guaranteed.
        /// </remarks>
        [TestMethod]
        [DataRow(5, 1)]
        [DataRow(1, 5)]
        public void EvidenceKeyFilterWhitelist_Dictionary_KeysDifferingInCase(
            int firstOrder,
            int secondOrder)
        {
            var inclusionList = new Dictionary<string, int>(
                StringComparer.Ordinal)
            {
                { "header.host", firstOrder },
                { "header.Host", secondOrder },
            };

            _filter = new EvidenceKeyFilterWhitelist(inclusionList);

            Assert.HasCount(1, _filter.Whitelist);
            Assert.AreEqual(1, _filter.Order("header.host"));
            Assert.AreEqual(1, _filter.Order("header.Host"));
        }

        /// <summary>
        /// A case-sensitive filter still treats two capitalisations as two
        /// keys, because to its comparer they are.
        /// </summary>
        [TestMethod]
        public void EvidenceKeyFilterWhitelist_CaseSensitive_KeysDifferingInCase()
        {
            _filter = new EvidenceKeyFilterWhitelist(
                new List<string>() { "header.Host", "header.host" },
                StringComparer.Ordinal);

            Assert.HasCount(2, _filter.Whitelist);
            Assert.IsTrue(_filter.Include("header.Host"));
            Assert.IsTrue(_filter.Include("header.host"));
            Assert.IsFalse(_filter.Include("HEADER.HOST"));
        }
    }
}
