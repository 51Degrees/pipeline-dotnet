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
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Data
{
    /// <summary>
    /// Element data instance for <see cref="SetHeadersElement"/>
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/pipeline-elements/set-headers-element.md#element-data">Specification</see>
    /// </summary>
    public class SetHeadersData : ElementDataBase,  ISetHeadersData
    {
        /// <summary>
        /// The key used to store the value for the 
        /// HTTP response headers in the internal data collection.
        /// </summary>
#pragma warning disable CA1707 // Identifiers should not contain underscores
        public const string RESPONSE_HEADERS_KEY = "responseheaderdictionary";
#pragma warning restore CA1707 // Identifiers should not contain underscores

        /// <inheritdoc/>
        public SetHeadersData(
            ILogger<SetHeadersData> logger,
            IPipeline pipeline)
            : base(logger, pipeline)
        { }

        /// <inheritdoc/>
        public SetHeadersData(
            ILogger<ElementDataBase> logger, 
            IPipeline pipeline, 
            IDictionary<string, object> dictionary) 
            : base(logger, pipeline, dictionary)
        {
        }

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, string> ResponseHeaderDictionary
        {
            get => this[RESPONSE_HEADERS_KEY] as IReadOnlyDictionary<string, string>;
            set => this[RESPONSE_HEADERS_KEY] = value;
        }
    }
}
