/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2019 51 Degrees Mobile Experts Limited, 5 Charlotte Close,
 * Caversham, Reading, Berkshire, United Kingdom RG4 7BY.
 *
 * This Original Work is licensed under the European Union Public Licence (EUPL) 
 * v.1.2 and is subject to its terms as set out below.
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
using System.Text;
using FiftyOne.Pipeline.Core.Attributes;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging;

namespace FiftyOne.Pipeline.IpSplitter
{
    [AlternateName("IpSplitter")]
    public class IpSplitterElementBuilder
    {
        private ILoggerFactory _loggerFactory;

        /// <summary>
        /// Construct a new builder instance using the logger factory provided.
        /// </summary>
        /// <param name="loggerFactory">LoggerFactory instance</param>
        public IpSplitterElementBuilder(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        /// <summary>
        /// Build a new instance of IpSplitter using the logger provided.
        /// </summary>
        /// <returns>New IpSplitter instance</returns>
        public IFlowElement Build()
        {
            return new IpSplitterElement(
                _loggerFactory.CreateLogger<IpSplitterElement>(),
                CreateAspectData);
        }

        /// <summary>
        /// Create a new SplitIpData instance ready for populating with data.
        /// </summary>
        /// <param name="element">IpSplitter element</param>
        /// <returns>New SplitIpData instance</returns>
        private ISplitIpData CreateAspectData(IFlowData flowData, 
            FlowElementBase<ISplitIpData, IElementPropertyMetaData> element)
        {
            return new SplitIpData(_loggerFactory.CreateLogger<SplitIpData>(), flowData);
        }
    }
}
