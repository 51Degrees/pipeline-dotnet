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

using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FiftyOne.Pipeline.CloudRequestEngine.Services
{
    /// <summary>
    /// <see cref="IMissingPropertyService"/> implementation tailored to
    /// cloud aspect engines. The default
    /// <see cref="MissingPropertyService"/> reasons via
    /// <c>DataTiersWherePresent</c> / <c>DataSourceTier</c>, which is a
    /// data-file concept that does not apply to cloud engines and produces
    /// misleading <see cref="MissingPropertyReason.DataFileUpgradeRequired"/>
    /// messages when a cloud request fails or returns a partial response.
    /// <para>
    /// For any engine that implements <see cref="ICloudAspectEngine"/> this
    /// service skips the data-tier branch and uses cloud-specific reasoning
    /// based on the engine's metadata: a property missing from the request's
    /// data but present in the engine's property list is reported with
    /// <see cref="MissingPropertyReason.CloudRequestFailed"/> (the resource
    /// key allows it, so the only remaining explanation is that the cloud
    /// request did not populate it for this request).
    /// </para>
    /// <para>
    /// Non-cloud engines are forwarded to the inner default service unchanged
    /// so on-premise / data-file engines retain their existing semantics.
    /// </para>
    /// </summary>
    public class CloudRequestMissingPropertyService : IMissingPropertyService
    {
        private readonly IMissingPropertyService _defaultService;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="defaultService">
        /// Service to delegate to for non-cloud engines. If null, the
        /// shared <see cref="MissingPropertyService.Instance"/> is used.
        /// </param>
        public CloudRequestMissingPropertyService(
            IMissingPropertyService defaultService = null)
        {
            _defaultService = defaultService ?? MissingPropertyService.Instance;
        }

        /// <inheritdoc/>
        public MissingPropertyResult GetMissingPropertyReason(
            string propertyName,
            IAspectEngine engine)
        {
            if (engine == null)
            {
                return _defaultService.GetMissingPropertyReason(propertyName, engine);
            }

            // Non-cloud engine: defer to the default reasoning.
            if (!(engine is ICloudAspectEngine))
            {
                return _defaultService.GetMissingPropertyReason(propertyName, engine);
            }

            // Cloud engine, but metadata not yet loaded -- defer.
            if (engine.HasLoadedProperties == false)
            {
                return _defaultService.GetMissingPropertyReason(propertyName, engine);
            }

            // Resource key grants no properties at all for this engine.
            if (engine.Properties.Count == 0)
            {
                return new MissingPropertyResult
                {
                    Reason = MissingPropertyReason.ProductNotAccessibleWithResourceKey,
                    Description = string.Format(CultureInfo.InvariantCulture,
                        "Property '{0}' is not available because the resource " +
                        "key in use does not grant access to any properties for " +
                        "engine '{1}'.",
                        propertyName, engine.ElementDataKey),
                };
            }

            // Resource key grants some properties but not this one.
            if (engine.Properties.Any(p => p.Name == propertyName) == false)
            {
                return new MissingPropertyResult
                {
                    Reason = MissingPropertyReason.PropertyNotAccessibleWithResourceKey,
                    Description = string.Format(CultureInfo.InvariantCulture,
                        "Property '{0}' is not in the list of properties " +
                        "accessible with the current resource key for engine " +
                        "'{1}'. Accessible: [{2}].",
                        propertyName, engine.ElementDataKey,
                        string.Join(",", engine.Properties.Select(p => p.Name))),
                };
            }

            // The property IS in the engine's property list (the resource
            // key allows it) but it was not populated for this request. The
            // cloud engine has no data file to upgrade, so the only
            // remaining explanation is that the cloud request did not
            // produce a value -- typically because the upstream cloud call
            // failed or returned partial data. Inspect IFlowData.Errors for
            // the underlying exception.
            return new MissingPropertyResult
            {
                Reason = MissingPropertyReason.CloudRequestFailed,
                Description = string.Format(CultureInfo.InvariantCulture,
                    "Property '{0}' is granted by the current resource key " +
                    "for engine '{1}' but was not populated for this " +
                    "request. The cloud request likely failed or returned " +
                    "partial data; check IFlowData.Errors and the " +
                    "CloudRequestEngine logs for the underlying cause.",
                    propertyName, engine.ElementDataKey),
            };
        }

        /// <inheritdoc/>
        public MissingPropertyResult GetMissingPropertyReason(
            string propertyName,
            IReadOnlyList<IAspectEngine> engines)
        {
            if (engines == null || engines.Count == 0)
            {
                return _defaultService.GetMissingPropertyReason(propertyName, engines);
            }

            // Pick the first engine that yields a known reason. This mirrors
            // the contract of MissingPropertyService for multi-engine data.
            MissingPropertyResult fallback = null;
            foreach (var engine in engines)
            {
                var result = GetMissingPropertyReason(propertyName, engine);
                if (result.Reason != MissingPropertyReason.Unknown)
                {
                    return result;
                }
                fallback = result;
            }
            return fallback ?? _defaultService.GetMissingPropertyReason(propertyName, engines);
        }
    }
}
