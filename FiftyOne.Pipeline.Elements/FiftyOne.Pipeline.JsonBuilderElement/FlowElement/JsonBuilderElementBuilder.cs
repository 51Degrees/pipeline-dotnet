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

using FiftyOne.Pipeline.JsonBuilder.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using FiftyOne.Pipeline.Core.Data;
using Newtonsoft.Json;
using System.Linq;
using FiftyOne.Pipeline.Core.Attributes;

namespace FiftyOne.Pipeline.JsonBuilder.FlowElement
{
    /// <summary>
    /// Fluent builder class for constructing <see cref="JsonBuilderElement"/>
    /// instances.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/pipeline-elements/json-builder.md">Specification</see>
    /// </summary>
    public class JsonBuilderElementBuilder
    {
        private ILoggerFactory _loggerFactory;
        private ILogger<JsonBuilderElementData> _dataLogger;

        private IEnumerable<JsonConverter> _jsonConverters = Enumerable.Empty<JsonConverter>();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="loggerFactory">
        /// The logger factory for this builder to use when creating new 
        /// instances.
        /// </param>
        public JsonBuilderElementBuilder(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _dataLogger = _loggerFactory.CreateLogger<JsonBuilderElementData>();
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="loggerFactory">
        /// The logger factory for this builder to use when creating new 
        /// instances.
        /// </param>
        /// <param name="jsonConverters">
        /// A list of additional <see cref="JsonConverter"/> instances 
        /// for the <see cref="JsonBuilderElement"/> to use when creating
        /// the JSON output.
        /// </param>
        [Obsolete("Use the 'SetJsonConverters' method instead. " +
            "This constructor will be removed in a future version.")]
        public JsonBuilderElementBuilder(ILoggerFactory loggerFactory, 
            IEnumerable<JsonConverter> jsonConverters)
        {
            _loggerFactory = loggerFactory;
            _jsonConverters = jsonConverters;
        }

        /// <summary>
        /// Set the additional <see cref="JsonConverter"/> instances
        /// to be passed to the <see cref="JsonBuilderElement"/>.
        /// </summary>
        /// <param name="jsonConverters">
        /// A list of additional <see cref="JsonConverter"/> instances 
        /// for the <see cref="JsonBuilderElement"/> to use when creating
        /// the JSON output.
        /// </param>
        /// <returns>This builder.</returns>
        [CodeConfigOnly]
        public JsonBuilderElementBuilder SetJsonConverters(IEnumerable<JsonConverter> jsonConverters)
        {
            _jsonConverters = jsonConverters;
            return this;
        }

        /// <summary>
        /// Build and return a new <see cref="JsonBuilderElement"/> with
        /// the currently configured settings.
        /// </summary>
        /// <returns>
        /// a new <see cref="JsonBuilderElement"/>
        /// </returns>
        public JsonBuilderElement Build()
        {
            return new JsonBuilderElement(
                _loggerFactory.CreateLogger<JsonBuilderElement>(),
                _jsonConverters, CreateData);
        }

        /// <summary>
        /// Factory method for creating the 
        /// <see cref="JsonBuilderElementData"/> instances that 
        /// will be populated by the <see cref="JsonBuilderElement"/>.
        /// </summary>
        /// <param name="pipeline">
        /// The pipeline that this is part of.
        /// </param>
        /// <param name="jsonBuilderElement">
        /// The <see cref="JsonBuilderElement"/> the is creating this data
        /// instance.
        /// </param>
        /// <returns>
        /// A new <see cref="JsonBuilderElementData"/> instance.
        /// </returns>
        private IJsonBuilderElementData CreateData(
            IPipeline pipeline,
            FlowElementBase<IJsonBuilderElementData, IElementPropertyMetaData> jsonBuilderElement)
        {
            return new JsonBuilderElementData(
                _dataLogger,
                pipeline);
        }

    }
}
