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

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using EnginesMessages = FiftyOne.Pipeline.Engines.Messages;

namespace FiftyOne.Pipeline.CloudRequestEngine.Services
{
    /// <summary>
    /// Cloud-aware missing-property service. Extends
    /// <see cref="MissingPropertyService"/> with a per-request check for
    /// <see cref="MissingPropertyReason.CloudRequestFailed"/>: when the
    /// aspect data carries the marker set by
    /// <c>CloudAspectEngineBase</c> for a failed upstream cloud request,
    /// this service short-circuits to <c>CloudRequestFailed</c> instead
    /// of letting the base heuristics mis-report the reason.
    /// </summary>
    /// <remarks>
    /// Cloud aspect-engine builders should wire this service in place of
    /// <see cref="MissingPropertyService"/>; otherwise property accesses
    /// on data from a failed cloud request fall through to the base
    /// heuristics and surface as <c>PropertyNotAccessibleWithResourceKey</c>
    /// (or similar) rather than the transient-failure reason.
    /// </remarks>
    public class MissingPropertyServiceCloud : MissingPropertyService
    {
        private static IMissingPropertyService _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// Singleton instance of the cloud missing-property service.
        /// </summary>
        public static new IMissingPropertyService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new MissingPropertyServiceCloud();
                        }
                    }
                }
                return _instance;
            }
        }

        private MissingPropertyServiceCloud() { }

        /// <inheritdoc/>
        public override MissingPropertyResult GetMissingPropertyReason(
            string propertyName,
            IReadOnlyList<IAspectEngine> engines,
            IAspectData aspectData)
        {
            if (IsCloudRequestFailed(aspectData))
            {
                var firstEngine = engines?.FirstOrDefault(e => e != null);
                return BuildCloudRequestFailedResult(propertyName, firstEngine);
            }
            return base.GetMissingPropertyReason(propertyName, engines, aspectData);
        }

        private static bool IsCloudRequestFailed(IAspectData aspectData)
        {
            return aspectData is AspectDataBase aspectDataBase
                && aspectDataBase.CloudRequestFailed;
        }

        private static MissingPropertyResult BuildCloudRequestFailedResult(string propertyName, IAspectEngine engine)
        {
            var elementKey = engine?.ElementDataKey ?? "Unknown";
            var description = string.Format(
                CultureInfo.InvariantCulture,
                EnginesMessages.MissingPropertyMessagePrefix,
                propertyName,
                elementKey)
                + EnginesMessages.MissingPropertyMessageCloudRequestFailed;
            return new MissingPropertyResult
            {
                Reason = MissingPropertyReason.CloudRequestFailed,
                Description = description,
            };
        }
    }
}
