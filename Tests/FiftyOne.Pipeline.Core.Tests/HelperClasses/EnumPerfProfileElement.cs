/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2023 51 Degrees Mobile Experts Limited, Davidson House,
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
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Core.Tests.HelperClasses
{
    /// <summary>
    /// Test element that has an Enum property that can be set in configuration
    /// </summary>
    public class EnumPerfProfileElement : FlowElementBase<TestElementData, IElementPropertyMetaData>
    {
        public override string ElementDataKey => "EnumPerfProfile";

        public List<string> EvidenceKeys = new List<string>()
        {
            "value"
        };
        private EvidenceKeyFilterWhitelist _evidenceKeyFilter;

        public override IEvidenceKeyFilter EvidenceKeyFilter => _evidenceKeyFilter;

        public override IList<IElementPropertyMetaData> Properties => new List<IElementPropertyMetaData>();

        // public so we can assert its value.
        public PerformanceProfiles PerfProfile => _perfProfile;
        private PerformanceProfiles _perfProfile;

        public EnumPerfProfileElement(PerformanceProfiles profile) :
            base(new Mock<ILogger<EnumPerfProfileElement>>().Object)
        {
            _perfProfile = profile;
            _evidenceKeyFilter = new EvidenceKeyFilterWhitelist(EvidenceKeys);
        }

        protected override void ProcessInternal(IFlowData data)
        {
            TestElementData elementData = data.GetOrAdd(
                ElementDataKeyTyped,
                (p) => new TestElementData(p));
            int value = (int)data.GetEvidence()[EvidenceKeys[0]];
            elementData.Result = value;
        }

        protected override void ManagedResourcesCleanup()
        {
        }

        protected override void UnmanagedResourcesCleanup()
        {
        }
    }

}
