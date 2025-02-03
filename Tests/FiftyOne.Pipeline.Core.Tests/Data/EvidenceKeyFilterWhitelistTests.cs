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

            Assert.AreEqual(2, result.Count);
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
    }
}
