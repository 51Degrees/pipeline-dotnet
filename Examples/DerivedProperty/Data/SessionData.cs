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
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging;

namespace Examples.DerivedProperty.Data
{
    /// <summary>
    /// Holds the values the example's source element publishes. A value is
    /// written only where the request carried evidence for it, so a
    /// property with no evidence is not in the dictionary at all and the
    /// derived property element sees the property as absent.
    /// </summary>
    internal class SessionData : ElementDataBase, ISessionData
    {
        /// <summary>
        /// The key PagesViewed is held under.
        /// </summary>
        public const string PagesViewedKey = "PagesViewed";

        /// <summary>
        /// The key SecondsSincePageLoad is held under.
        /// </summary>
        public const string SecondsSincePageLoadKey =
            "SecondsSincePageLoad";

        /// <summary>
        /// The key PointerMoved is held under.
        /// </summary>
        public const string PointerMovedKey = "PointerMoved";

        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="logger">The logger for the new instance.</param>
        /// <param name="pipeline">The pipeline the data belongs to.</param>
        public SessionData(
            ILogger<ElementDataBase> logger,
            IPipeline pipeline)
            : base(logger, pipeline)
        {
        }

        /// <inheritdoc/>
        public int? PagesViewed
        {
            get { return ReadInt(PagesViewedKey); }
            set { base[PagesViewedKey] = value; }
        }

        /// <inheritdoc/>
        public int? SecondsSincePageLoad
        {
            get { return ReadInt(SecondsSincePageLoadKey); }
            set { base[SecondsSincePageLoadKey] = value; }
        }

        /// <inheritdoc/>
        public bool? PointerMoved
        {
            get { return ReadBool(PointerMovedKey); }
            set { base[PointerMovedKey] = value; }
        }

        private int? ReadInt(string key)
        {
            return TryGet(key, out var value) ? (int?)value : null;
        }

        private bool? ReadBool(string key)
        {
            return TryGet(key, out var value) ? (bool?)value : null;
        }
    }
}
