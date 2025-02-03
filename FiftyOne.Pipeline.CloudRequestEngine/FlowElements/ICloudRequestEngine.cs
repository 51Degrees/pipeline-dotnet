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

using FiftyOne.Pipeline.CloudRequestEngine.Data;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.CloudRequestEngine.FlowElements
{
    /// <summary>
    /// The interface for <see cref="CloudRequestEngine"/>
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/pipeline-elements/cloud-request-engine.md">Specification</see> 
    /// </summary>
    public interface ICloudRequestEngine : IAspectEngine<CloudRequestData, IAspectPropertyMetaData>
    {
        /// <summary>
        /// A collection containing the meta-data for the properties that 
        /// the cloud service will return values for when a request is
        /// made using the supplied resource key.
        /// Note that this is distinct from the 
        /// <see cref="IAspectEngine.Properties"/> collection, which returns
        /// the meta-data for the properties that are populated by 
        /// this engine in the <see cref="IFlowData"/>.
        /// The key is the 'product name' and is equivalent to the 
        /// <see cref="IFlowElement.ElementDataKey"/>. For example, 'device'.
        /// </summary>
        IReadOnlyDictionary<string, ProductMetaData> PublicProperties { get; }
    }
}
