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

using Examples.DerivedProperty.Data;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Examples.DerivedProperty.FlowElements
{
    /// <summary>
    /// The element that supplies the source properties the example's
    /// script reads. A real pipeline puts device detection or IP
    /// intelligence in this position, and this small element stands in for
    /// one of those so that the example runs with no resource key, no data
    /// file and no network connection.
    ///
    /// The element reads three items of evidence and publishes each one it
    /// finds as a property under the element data key session. Evidence
    /// that is not supplied leaves the matching property absent, which is
    /// how the example shows a script working with less evidence.
    /// </summary>
    public class SessionElement :
        FlowElementBase<ISessionData, IElementPropertyMetaData>
    {
        /// <summary>
        /// The evidence key holding how many pages the visitor has seen.
        /// </summary>
        public const string PagesViewedEvidenceKey = "query.pagesviewed";

        /// <summary>
        /// The evidence key holding how long the page has been open.
        /// </summary>
        public const string SecondsEvidenceKey = "query.secondssincepageload";

        /// <summary>
        /// The evidence key holding whether the pointer has moved.
        /// </summary>
        public const string PointerMovedEvidenceKey = "query.pointermoved";

        private readonly IEvidenceKeyFilter _evidenceKeyFilter =
            new EvidenceKeyFilterWhitelist(new List<string>()
            {
                PagesViewedEvidenceKey,
                SecondsEvidenceKey,
                PointerMovedEvidenceKey
            });

        private readonly IList<IElementPropertyMetaData> _properties;

        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="logger">The logger for the new instance.</param>
        /// <param name="elementDataFactory">
        /// How the element makes its element data.
        /// </param>
        public SessionElement(
            ILogger<FlowElementBase<ISessionData, IElementPropertyMetaData>>
                logger,
            Func<IPipeline,
                FlowElementBase<ISessionData, IElementPropertyMetaData>,
                ISessionData> elementDataFactory)
            : base(logger, elementDataFactory)
        {
            // The derived property element checks at build time that some
            // element earlier in the pipeline says it supplies each source
            // property a script needs, so the names below must match the
            // names the script uses after the dot.
            _properties = new List<IElementPropertyMetaData>()
            {
                new ElementPropertyMetaData(
                    this,
                    SessionData.PagesViewedKey,
                    typeof(int),
                    true,
                    "Example"),
                new ElementPropertyMetaData(
                    this,
                    SessionData.SecondsSincePageLoadKey,
                    typeof(int),
                    true,
                    "Example"),
                new ElementPropertyMetaData(
                    this,
                    SessionData.PointerMovedKey,
                    typeof(bool),
                    true,
                    "Example")
            };
        }

        /// <summary>
        /// The key the values are published under, which is the half of
        /// session.PagesViewed before the dot.
        /// </summary>
        public override string ElementDataKey => "session";

        /// <inheritdoc/>
        public override IEvidenceKeyFilter EvidenceKeyFilter =>
            _evidenceKeyFilter;

        /// <inheritdoc/>
        public override IList<IElementPropertyMetaData> Properties =>
            _properties;

        /// <inheritdoc/>
        protected override void ProcessInternal(IFlowData data)
        {
            var session = (SessionData)data.GetOrAdd(
                ElementDataKey,
                CreateElementData);

            // A property is written only where the request carried the
            // evidence for it. A property that is never written stays
            // absent, and the script decides what absence means.
            if (data.TryGetEvidence(
                PagesViewedEvidenceKey, out int pagesViewed))
            {
                session.PagesViewed = pagesViewed;
            }
            if (data.TryGetEvidence(
                SecondsEvidenceKey, out int seconds))
            {
                session.SecondsSincePageLoad = seconds;
            }
            if (data.TryGetEvidence(
                PointerMovedEvidenceKey, out bool pointerMoved))
            {
                session.PointerMoved = pointerMoved;
            }
        }

        /// <inheritdoc/>
        protected override void ManagedResourcesCleanup()
        {
            // Nothing to clean up here.
        }

        /// <inheritdoc/>
        protected override void UnmanagedResourcesCleanup()
        {
            // Nothing to clean up here.
        }
    }
}
