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
using FiftyOne.Pipeline.Engines.FiftyOne.Data;
using Microsoft.Extensions.Logging;

namespace FiftyOne.Pipeline.Engines.FiftyOne.FlowElements
{
    /// <summary>
    /// Fluent builder for <see cref="SetHeadersElement"/> instances.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/pipeline-elements/set-headers-element.md">Specification</see>
    /// </summary>
    public class SetHeadersElementBuilder
    {
        private ILoggerFactory _loggerFactory;
        private ILogger<SetHeadersData> _dataLogger;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="loggerFactory">
        /// The logger factory for this builder to use when creating new 
        /// instances.
        /// </param>
        public SetHeadersElementBuilder(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _dataLogger = _loggerFactory.CreateLogger<SetHeadersData>();
        }

        /// <summary>
        /// Create a new <see cref="SetHeadersElement"/> instance.
        /// </summary>
        /// <returns></returns>
        public SetHeadersElement Build()
        {
            return new SetHeadersElement(
                _loggerFactory.CreateLogger<SetHeadersElement>(),
                CreateData);
        }

        /// <summary>
        /// Factory method for creating the 
        /// <see cref="SetHeadersData"/> instances that 
        /// will be populated by the <see cref="SetHeadersElement"/>.
        /// </summary>
        /// <param name="pipeline">
        /// The pipeline that this is part of.
        /// </param>
        /// <param name="setHeadersElement">
        /// The <see cref="SetHeadersElement"/> the is creating this data
        /// instance.
        /// </param>
        /// <returns>
        /// A new <see cref="SetHeadersElement"/> instance.
        /// </returns>
        private ISetHeadersData CreateData(
            IPipeline pipeline,
            FlowElementBase<ISetHeadersData, IElementPropertyMetaData> setHeadersElement)
        {
            return new SetHeadersData(
                _dataLogger,
                pipeline);
        }
    }
}