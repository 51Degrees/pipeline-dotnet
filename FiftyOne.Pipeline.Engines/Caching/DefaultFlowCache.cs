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
using FiftyOne.Pipeline.Engines.Configuration;

namespace FiftyOne.Pipeline.Engines.Caching
{
    /// <summary>
    /// The default 'flow cache' implementation.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/caching.md">Specification</see>
    /// </summary>
    public class DefaultFlowCache : 
        DataKeyedCacheBase<IElementData>, IFlowCache
    {
        /// <summary>
        /// The <see cref="IFlowElement"/> that this cache is associated with
        /// </summary>
        private IFlowElement _flowElement;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="configuration">
        /// The cache configuration to use when creating the internal cache.
        /// </param>
        public DefaultFlowCache(CacheConfiguration configuration) :
            base(configuration)
        {
        }

        /// <summary>
        /// Get/set the <see cref="IFlowElement"/> that this cache is 
        /// associated with.
        /// </summary>
        public virtual IFlowElement FlowElement
        {
            get
            {
                return _flowElement;
            }
            set
            {
                _flowElement = value;
            }
        }

        /// <summary>
        /// The evidence key filter that is used to create
        /// a <see cref="DataKey"/> from an <see cref="IFlowData"/> instance.
        /// </summary>
        /// <returns>
        /// The <see cref="IEvidenceKeyFilter"/> to use.
        /// </returns>
        protected override IEvidenceKeyFilter GetFilter()
        {
            return _flowElement.EvidenceKeyFilter;
        }
    }
}
