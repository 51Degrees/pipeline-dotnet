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

using System;
using System.Collections.Generic;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Core.TypedMap;
using Microsoft.Extensions.Logging;

namespace FiftyOne.Pipeline.IpSplitter
{
    public class IpSplitterElement : FlowElementBase<ISplitIpData, IElementPropertyMetaData>
    {
        private EvidenceKeyFilterWhitelist _evidenceKeyFilter;

        private IList<IElementPropertyMetaData> _properties =
            new List<IElementPropertyMetaData>();

        /// <summary>
        /// Typed data key used to return an ISplitIpData from a FlowData
        /// object.
        /// </summary>
        public static TypedKey<ISplitIpData> ipsplitter =
            new TypedKey<ISplitIpData>("splitip");

        /// <summary>
        /// Data key used when storing data in a FlowData object.
        /// </summary>
        public override string ElementDataKey => "splitip";

        /// <summary>
        /// Required evidence for process method.
        /// </summary>
        public List<string> EvidenceKeys = new List<string>()
        {
            Pipeline.Core.Constants.EVIDENCE_CLIENTIP_KEY
        };

        public override IEvidenceKeyFilter EvidenceKeyFilter => _evidenceKeyFilter;

        private void initProperties()
        {
            _properties.Add(new ElementPropertyMetaData(
                this,
                "clientip",
                typeof(string),
                true));
        }

        /// <summary>
        /// Construct a new instance of IpSplitterElement using the logger and
        /// data factory provided.
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="elementDataFactory">
        /// Data factory to construct new instances of SplitIpData
        /// </param>
        internal IpSplitterElement(
            ILogger<IpSplitterElement> logger,
            Func<IPipeline, FlowElementBase<ISplitIpData, IElementPropertyMetaData>, 
                ISplitIpData> elementDataFactory)
            : base(logger, elementDataFactory)
        {
            _evidenceKeyFilter = new EvidenceKeyFilterWhitelist(EvidenceKeys);
            initProperties();
        }

        /// <summary>
        /// Process the client IP address by splitting the segments into an
        /// array
        /// </summary>
        /// <param name="data">FlowData to add the result to</param>
        protected override void ProcessInternal(IFlowData data)
        {
            SplitIpData elementData = (SplitIpData)data.GetOrAdd(
                ElementDataKeyTyped,
                (f) => base.CreateElementData(f));
            string ip = ((string)data.GetEvidence()[EvidenceKeys[0]]);
            elementData.ClientIp = ip.Split('.', ':');
        }

        public override IList<IElementPropertyMetaData> Properties =>
            _properties;

        protected override void ManagedResourcesCleanup()
        {
            throw new NotImplementedException();
        }

        protected override void UnmanagedResourcesCleanup()
        {
            throw new NotImplementedException();
        }
    }
}
