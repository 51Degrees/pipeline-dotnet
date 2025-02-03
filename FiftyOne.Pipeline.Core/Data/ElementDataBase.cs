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

using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Core.Data
{
    /// <summary>
    /// This class stores property values that have been determined by a 
    /// specific FlowElement based on the supplied evidence.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/conceptual-overview.md#element-data">Specification</see>
    /// </summary>
    public abstract class ElementDataBase : DataBase, IElementData
    {
        /// <summary>
        /// The <see cref="IPipeline"/> instance this element data will
        /// be associated with.
        /// </summary>
        public IPipeline Pipeline { get; set; }

        /// <summary>
        /// Constructor
        /// Creates an <see cref="ElementDataBase"/> instance with a 
        /// non-thread-safe, case-insensitive dictionary.
        /// </summary>
        /// <param name="logger">
        /// Used for logging
        /// </param>
        /// <param name="pipeline">
        /// The <see cref="IPipeline"/> instance this element data will
        /// be associated with.
        /// </param>
        public ElementDataBase(ILogger<ElementDataBase> logger, IPipeline pipeline) :
            this(logger, pipeline, new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase))
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">
        /// Used for logging
        /// </param>
        /// <param name="pipeline">
        /// The <see cref="IPipeline"/> instance this element data will
        /// be associated with.
        /// </param>
        /// <param name="dictionary">
        /// The dictionary instance to use internally when storing data values.
        /// </param>
        public ElementDataBase(ILogger<ElementDataBase> logger,
            IPipeline pipeline,
            IDictionary<string, object> dictionary)
            : base(logger, dictionary)
        {
            Pipeline = pipeline;
        }
    }
}
