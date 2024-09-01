/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2023 51 Degrees Mobile Experts Limited, Davidson House,
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

using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("FiftyOne.Pipeline.Engines.FiftyOne.Tests")]
namespace FiftyOne.Pipeline.Engines.FiftyOne.FlowElements
{
    /// <summary>
    /// Builder class that is used to create <see cref="ShareUsageElement"/>
    /// instances.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/pipeline-elements/usage-sharing-element.md">Specification</see>
    /// </summary>
    public class ShareUsageBuilder : ShareUsageBuilderBase<ShareUsageElement>
    {
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="loggerFactory">
        /// The <see cref="ILoggerFactory"/> to use when creating loggers for
        /// a <see cref="ShareUsageElement"/>.
        /// </param>
        /// <param name="httpClient">
        /// The <see cref="HttpClient"/> that <see cref="ShareUsageElement"/>
        /// should use for sending data.
        /// </param>
        public ShareUsageBuilder(
            ILoggerFactory loggerFactory,
            HttpClient httpClient): base (loggerFactory)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger"></param>
        /// The <see cref="ILogger"/> to use for a <see cref="ShareUsageElement"/>.
        /// <param name="loggerFactory">
        /// The <see cref="ILoggerFactory"/> to use when creating loggers for
        /// a <see cref="ShareUsageElement"/>.
        /// </param>
        /// <param name="httpClient">
        /// The <see cref="HttpClient"/> that <see cref="ShareUsageElement"/>
        /// should use for sending data.
        /// </param>
        public ShareUsageBuilder(
            ILoggerFactory loggerFactory,
            ILogger logger,
            HttpClient httpClient):base(loggerFactory, logger)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// Create the <see cref="ShareUsageElement"/>
        /// </summary>
        /// <returns>
        /// The newly created <see cref="ShareUsageElement"/>
        /// </returns>
        public override ShareUsageElement Build()
        {
            return new ShareUsageElement(
                LoggerFactory.CreateLogger<ShareUsageElement>(),
                _httpClient,
                SharePercentage,
                MinimumEntriesPerMessage,
                MaximumQueueSize,
                AddTimeout,
                TakeTimeout,
                RepeatEvidenceInterval,
                TrackSession,
                ShareUsageUri.AbsoluteUri,
                BlockedHttpHeaders,
                IncludedQueryStringParameters,
                IgnoreDataEvidenceFilter,
                AspSessionCookieName,
                null,
                ShareAllEvidence);
        }
    }
}
