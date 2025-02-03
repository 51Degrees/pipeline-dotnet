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
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Engines.TestHelpers
{
    public class GenericEngine<T> : AspectEngineBase<T, IAspectPropertyMetaData>
        where T : IAspectData
    {
        public override string DataSourceTier { get; }

        public override string ElementDataKey { get; }

        public override IEvidenceKeyFilter EvidenceKeyFilter => new EvidenceKeyFilterWhitelist(new List<string>());

        public override IList<IAspectPropertyMetaData> Properties { get; }

        public GenericEngine(
            ILogger<GenericEngine<T>> logger,
            string tier,
            string dataKey,
            IList<IAspectPropertyMetaData> properties) :
            base(logger, (p, e) => default(T))
        {
            DataSourceTier = tier;
            ElementDataKey = dataKey;
            Properties = properties;
        }

        protected override void ProcessEngine(IFlowData data, T aspectData)
        {
        }

        protected override void UnmanagedResourcesCleanup()
        {
        }
    }
}
