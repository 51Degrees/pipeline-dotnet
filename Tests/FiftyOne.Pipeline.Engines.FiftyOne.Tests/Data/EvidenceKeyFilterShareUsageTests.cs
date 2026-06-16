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

using FiftyOne.Pipeline.Engines.FiftyOne.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Tests.Data
{
    /// <summary>
    /// Tests that the caller-supplied secrets used to derive hashed-email
    /// identifiers (the 'id.email' and 'id.salt' keys) are never shared,
    /// regardless of the share-all setting.
    /// </summary>
    [TestClass]
    public class EvidenceKeyFilterShareUsageTests
    {
        [TestMethod]
        public void ShareAll_IncludesEverythingExceptTheEmailAndSaltKeys()
        {
            var filter = new EvidenceKeyFilterShareUsage();

            Assert.IsFalse(filter.Include("query.id.email"));
            Assert.IsFalse(filter.Include("header.id.email"));
            Assert.IsFalse(filter.Include("query.id.salt"));
            Assert.IsFalse(filter.Include("header.id.salt"));
            Assert.IsTrue(filter.Include("query.id.usage"));
            Assert.IsTrue(filter.Include("header.user-agent"));
        }

        [TestMethod]
        public void ShareAll_NeverSharedKeys_AreCaseInsensitive()
        {
            var filter = new EvidenceKeyFilterShareUsage();

            Assert.IsFalse(filter.Include("QUERY.ID.EMAIL"));
            Assert.IsFalse(filter.Include("QUERY.ID.SALT"));
        }

        [TestMethod]
        public void ShareAll_SuffixIsAnchoredToASegmentBoundary()
        {
            // 'valid.email' ends with the literal string 'id.email' but the
            // segment-boundary match prevents it being treated as a
            // never-shared key.
            var filter = new EvidenceKeyFilterShareUsage();

            Assert.IsTrue(filter.Include("query.valid.email"));
        }

        [TestMethod]
        public void ShareAll_NeverSharedKeys_MatchWithoutACategoryPrefix()
        {
            // The secret keys must be excluded even when supplied as a bare
            // segment with no 'query.'/'header.' category prefix.
            var filter = new EvidenceKeyFilterShareUsage();

            Assert.IsFalse(filter.Include("id.email"));
            Assert.IsFalse(filter.Include("id.salt"));
        }

        [TestMethod]
        public void Filtered_NeverSharedKeys_ExcludedBeforeOtherRules()
        {
            var filter = new EvidenceKeyFilterShareUsage(
                blockedHttpHeaders: new List<string>(),
                includedQueryStringParams: null,
                includeSession: false,
                aspSessionCookieName: "asp.net_sessionid");

            Assert.IsFalse(filter.Include("query.id.email"));
            Assert.IsFalse(filter.Include("query.id.salt"));
            Assert.IsTrue(filter.Include("query.id.usage"));
            Assert.IsTrue(filter.Include("header.user-agent"));
        }
    }
}
