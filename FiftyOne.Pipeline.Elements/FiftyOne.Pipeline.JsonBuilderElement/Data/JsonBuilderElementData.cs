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
using FiftyOne.Pipeline.JsonBuilder.FlowElement;
using Microsoft.Extensions.Logging;

namespace FiftyOne.Pipeline.JsonBuilder.Data
{
    /// <summary>
    /// JsonBuilderElement data class.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/pipeline-elements/json-builder.md#element-data">Specification</see>
    /// </summary>
    public class JsonBuilderElementData : ElementDataBase, IJsonBuilderElementData
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">
        /// The logger for this instance
        /// </param>
        /// <param name="pipeline">
        /// The pipeline this instance was created by.
        /// </param>
        public JsonBuilderElementData(
            ILogger<JsonBuilderElementData> logger,
            IPipeline pipeline)
            : base(logger, pipeline)
        { }

        /// <summary>
        /// The JSON output produced by the 
        /// <see cref="JsonBuilderElement"/>
        /// </summary>
        public string Json
        {
            get
            {
                return (string)this["json"];
            }
            set
            {
                this["json"] = value;
            }
        }
    }
}
