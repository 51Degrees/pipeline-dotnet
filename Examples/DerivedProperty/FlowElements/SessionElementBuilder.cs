/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2026 51 Degrees Mobile Experts Limited, Davidson House,
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

using Examples.DerivedProperty.Data;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging;

namespace Examples.DerivedProperty.FlowElements
{
    /// <summary>
    /// Builds a <see cref="SessionElement"/>. A builder is what lets the
    /// element be named in a configuration file, because the pipeline
    /// finds a builder by name and calls its Build method.
    /// </summary>
    public class SessionElementBuilder
    {
        private readonly ILoggerFactory _loggerFactory;

        private readonly ILogger<ElementDataBase> _dataLogger;

        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="loggerFactory">
        /// How the element and its data make their loggers.
        /// </param>
        public SessionElementBuilder(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _dataLogger = loggerFactory.CreateLogger<ElementDataBase>();
        }

        /// <summary>
        /// Build the element.
        /// </summary>
        /// <returns>The element.</returns>
        public SessionElement Build()
        {
            return new SessionElement(
                _loggerFactory.CreateLogger<SessionElement>(),
                CreateData);
        }

        private ISessionData CreateData(
            IPipeline pipeline,
            FlowElementBase<ISessionData, IElementPropertyMetaData> element)
        {
            return new SessionData(_dataLogger, pipeline);
        }
    }
}
