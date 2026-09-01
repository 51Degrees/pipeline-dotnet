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

using System;
using System.Collections.Generic;
using System.Net.Http;

namespace FiftyOne.Pipeline.AgentSignature.FlowElement
{
    /// <summary>
    /// The settings the agent signature element runs with. The builder fills
    /// one of these in and hands it to the element.
    /// </summary>
    public class AgentSignatureConfiguration
    {
        /// <summary>
        /// The client the element fetches key directories and agent cards
        /// with. When this is null the element makes and owns a client of
        /// its own and disposes it with itself.
        /// </summary>
        public HttpClient HttpClient { get; set; }

        /// <summary>
        /// The number of key directories held in the cache.
        /// </summary>
        public int CacheSize { get; set; } = Constants.DEFAULT_CACHE_SIZE;

        /// <summary>
        /// How long a fetched key directory is reused for.
        /// </summary>
        public TimeSpan CacheLifetime { get; set; } =
            Constants.DEFAULT_CACHE_LIFETIME;

        /// <summary>
        /// How long a failed fetch is remembered for, so that an outage at
        /// one agent does not cause a fetch on every request.
        /// </summary>
        public TimeSpan NegativeCacheLifetime { get; set; } =
            Constants.DEFAULT_NEGATIVE_CACHE_LIFETIME;

        /// <summary>
        /// How long a request waits for a key directory fetch before it
        /// reports the Timeout status.
        /// </summary>
        public TimeSpan WaitBudget { get; set; } =
            Constants.DEFAULT_WAIT_BUDGET;

        /// <summary>
        /// The time limit on a single key directory fetch.
        /// </summary>
        public TimeSpan FetchTimeout { get; set; } =
            Constants.DEFAULT_FETCH_TIMEOUT;

        /// <summary>
        /// The tolerance on the 'created' and 'expires' signature
        /// parameters, which allows for clocks that differ a little.
        /// </summary>
        public TimeSpan ClockSkew { get; set; } =
            Constants.DEFAULT_CLOCK_SKEW;

        /// <summary>
        /// The longest a signature may be valid for. Zero, the default,
        /// places no limit, because the protocol draft only recommends one.
        /// </summary>
        public TimeSpan MaxLifetime { get; set; } =
            Constants.DEFAULT_MAX_LIFETIME;

        /// <summary>
        /// True when the bare quoted string form of the 'Signature-Agent'
        /// header, which the earlier drafts used, is accepted.
        /// </summary>
        public bool AllowLegacySignatureAgent { get; set; } =
            Constants.DEFAULT_ALLOW_LEGACY_SIGNATURE_AGENT;

        /// <summary>
        /// True when a key set carried inline in a 'data:' URI is accepted.
        /// This is off by default, because such a key set is chosen by
        /// whoever sent the request rather than published at an address the
        /// agent controls.
        /// </summary>
        public bool AllowInlineDirectory { get; set; } =
            Constants.DEFAULT_ALLOW_INLINE_DIRECTORY;

        /// <summary>
        /// The number of bytes read from a fetched document before the
        /// fetch is abandoned.
        /// </summary>
        public int MaxResponseBytes { get; set; } =
            Constants.DEFAULT_MAX_RESPONSE_BYTES;

        /// <summary>
        /// The registries of agent cards to read, each being a text
        /// document listing agent card URLs.
        /// </summary>
        public IList<string> Registries { get; } = new List<string>();

        /// <summary>
        /// The number of threads the cache is built to serve at once.
        /// </summary>
        public int Concurrency { get; set; } = Environment.ProcessorCount;

        /// <summary>
        /// The source of the current time. The tests replace it so that the
        /// signatures in the standard's test vectors, whose times are fixed,
        /// can be checked whatever today's date is.
        /// </summary>
        internal Func<DateTimeOffset> Clock { get; set; } =
            () => DateTimeOffset.UtcNow;
    }
}
