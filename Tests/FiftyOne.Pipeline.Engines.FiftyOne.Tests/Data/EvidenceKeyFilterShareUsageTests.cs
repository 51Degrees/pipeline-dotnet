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

        /// <summary>
        /// The method is shared like the other values under the 'server'
        /// prefix. It names no address and carries nothing a site put
        /// there, so neither reason for holding the path and the query
        /// back applies to it.
        /// </summary>
        [TestMethod]
        public void ShareAll_IncludesTheMethod()
        {
            var filter = new EvidenceKeyFilterShareUsage();

            Assert.IsTrue(filter.Include("server.request-method"));
            Assert.IsTrue(filter.Include("server.client-ip"));
        }

        /// <summary>
        /// The whole query string is never shared, in either mode.
        /// </summary>
        /// <remarks>
        /// This class withholds query string parameters by default and only
        /// shares the ones a caller names through the constructor, so
        /// 'query.email' is withheld unless it was asked for. The whole
        /// query string carries every one of those parameters at once, so
        /// without this the same value would be withheld under its 'query'
        /// prefix and shared under its 'server' prefix, undoing a choice
        /// the caller had made without anyone deciding to.
        /// </remarks>
        /// <summary>
        /// The path is never shared, in either mode.
        /// </summary>
        /// <remarks>
        /// <see cref="EvidenceKeyFilterShareUsageTracker"/> derives from
        /// this class, so anything shared is also part of the key usage
        /// sharing de-duplicates on. Sharing the path would make every
        /// address a visitor opens look like a different session, so one
        /// visitor moving through thirty pages would send thirty records
        /// where the tracker is meant to send one. The path is also the
        /// part of a URL most likely to carry a name or an identifier a
        /// site has put in it.
        /// </remarks>
        [TestMethod]
        public void ThePathIsNeverShared()
        {
            var shareAll = new EvidenceKeyFilterShareUsage();

            Assert.IsFalse(shareAll.Include("server.request-path"));
            Assert.IsFalse(shareAll.Include("SERVER.REQUEST-PATH"));

            var configured = new EvidenceKeyFilterShareUsage(
                new List<string>(),
                new List<string>(),
                false,
                "sessionid");

            Assert.IsFalse(configured.Include("server.request-path"));

            // The tracker inherits the rule, which is the point of it.
            var tracker = new EvidenceKeyFilterShareUsageTracker();

            Assert.IsFalse(tracker.Include("server.request-path"));
            Assert.IsFalse(tracker.Include("server.request-query"));
        }

        [TestMethod]
        public void TheWholeQueryStringIsNeverShared()
        {
            var shareAll = new EvidenceKeyFilterShareUsage();

            Assert.IsFalse(shareAll.Include("server.request-query"));
            Assert.IsFalse(shareAll.Include("SERVER.REQUEST-QUERY"));

            var configured = new EvidenceKeyFilterShareUsage(
                new List<string>(),
                new List<string>(),
                false,
                "sessionid");

            Assert.IsFalse(configured.Include("server.request-query"));

            // Naming a parameter for sharing does not bring the whole query
            // string with it.
            var namedParameter = new EvidenceKeyFilterShareUsage(
                new List<string>(),
                new List<string>() { "utm_source" },
                false,
                "sessionid");

            Assert.IsTrue(namedParameter.Include("query.utm_source"));
            Assert.IsFalse(namedParameter.Include("server.request-query"));
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
