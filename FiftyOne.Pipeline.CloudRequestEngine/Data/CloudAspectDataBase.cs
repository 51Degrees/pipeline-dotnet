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
using FiftyOne.Pipeline.Engines;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Globalization;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("FiftyOne.Pipeline.CloudRequestEngine.Tests")]

namespace FiftyOne.Pipeline.CloudRequestEngine.Data
{
    /// <summary>
    /// Base class for aspect data produced by a cloud aspect engine.
    /// Adds detection of per-request cloud-call failure so that a missing
    /// property is reported with reason
    /// <see cref="MissingPropertyReason.CloudRequestFailed"/> rather than a
    /// misleading data-tier or resource-key reason from
    /// <see cref="IMissingPropertyService"/>. Non-cloud aspect data
    /// continues to use <see cref="AspectDataBase"/> directly and is
    /// unaffected.
    /// </summary>
    public abstract class CloudAspectDataBase : AspectDataBase
    {
        private IFlowData _flowData;

        /// <summary>
        /// Constructor.
        /// </summary>
        public CloudAspectDataBase(
            ILogger<AspectDataBase> logger,
            IPipeline pipeline,
            IAspectEngine engine)
            : base(logger, pipeline, engine)
        { }

        /// <summary>
        /// Constructor.
        /// </summary>
        public CloudAspectDataBase(
            ILogger<AspectDataBase> logger,
            IPipeline pipeline,
            IAspectEngine engine,
            IMissingPropertyService missingPropertyService)
            : base(logger, pipeline, engine, missingPropertyService)
        { }

        /// <summary>
        /// Constructor.
        /// </summary>
        public CloudAspectDataBase(
            ILogger<AspectDataBase> logger,
            IPipeline pipeline,
            IAspectEngine engine,
            IMissingPropertyService missingPropertyService,
            IDictionary<string, object> dictionary)
            : base(logger, pipeline, engine, missingPropertyService, dictionary)
        { }

        /// <summary>
        /// Record the <see cref="IFlowData"/> instance this data belongs to,
        /// so the missing-property heuristic can inspect upstream errors.
        /// Set per-request by the cloud aspect engine after the data instance
        /// is created or located on the flow data.
        /// </summary>
        internal void SetFlowData(IFlowData flowData)
        {
            _flowData = flowData;
        }

        /// <inheritdoc/>
        protected override T GetAs<T>(string key)
        {
            try
            {
                return base.GetAs<T>(key);
            }
            catch (PropertyMissingException)
            {
                ThrowIfCloudRequestFailed(key);
                throw;
            }
        }

        /// <summary>
        /// If this aspect data was never populated for the current request
        /// AND any upstream element recorded an error on the flow data, the
        /// missing property is attributable to that per-request failure --
        /// not to the user's data tier or resource key. Throw with reason
        /// <see cref="MissingPropertyReason.CloudRequestFailed"/> so callers
        /// do not log misleading data-tier / resource-key messages for
        /// transient cloud failures. No-op otherwise.
        /// </summary>
        private void ThrowIfCloudRequestFailed(string key)
        {
            if (_flowData == null ||
                _flowData.Errors == null ||
                _flowData.Errors.Count == 0 ||
                AsDictionary().Count != 0)
            {
                return;
            }

            var firstError = _flowData.Errors[0];
            var sourceName = firstError.FlowElement == null
                ? "Unknown"
                : firstError.FlowElement.GetType().Name;
            var sourceMessage = firstError.ExceptionData == null
                ? string.Empty
                : firstError.ExceptionData.Message;
            var description = string.Format(CultureInfo.InvariantCulture,
                "Property '{0}' is not available for this request " +
                "because an upstream error occurred in '{1}': {2}",
                key, sourceName, sourceMessage);

            throw new PropertyMissingException(
                MissingPropertyReason.CloudRequestFailed,
                key, description);
        }
    }
}
