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
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Core.Tests.HelperClasses
{
    /// <summary>
    /// Test element that multiplies an evidence value by 10
    /// </summary>
    public class MultiplyByTenElement : FlowElementBase<TestElementData, IElementPropertyMetaData>
    {
        public override string ElementDataKey => "muliplyByTen";

        private List<string> _evidenceKeys = new List<string>()
        {
            "value"
        };
        private EvidenceKeyFilterWhitelist _evidenceKeyFilter;

        public override IList<IElementPropertyMetaData> Properties => new List<IElementPropertyMetaData>();

        public MultiplyByTenElement() :
            base(new Mock<ILogger<MultiplyByTenElement>>().Object)
        { _evidenceKeyFilter = new EvidenceKeyFilterWhitelist(_evidenceKeys); }

        public override IEvidenceKeyFilter EvidenceKeyFilter => _evidenceKeyFilter;

        protected override void ProcessInternal(IFlowData data)
        {
            TestElementData elementData = data.GetOrAdd(
                ElementDataKeyTyped,
                (p) => new TestElementData(p));

            int value = (int)data.GetEvidence()[_evidenceKeys[0]];
            elementData.Result = value * 10;
        }

        protected override void ManagedResourcesCleanup()
        {
        }

        protected override void UnmanagedResourcesCleanup()
        {
        }
    }

}
