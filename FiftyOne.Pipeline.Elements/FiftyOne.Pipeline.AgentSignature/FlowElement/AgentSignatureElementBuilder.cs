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

using FiftyOne.Pipeline.AgentSignature.Data;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;

namespace FiftyOne.Pipeline.AgentSignature.FlowElement
{
    /// <summary>
    /// Builder for the <see cref="AgentSignatureElement"/>.
    /// </summary>
    /// <remarks>
    /// Every option can also be set from a pipeline configuration file, by
    /// naming the method without its 'Set' prefix as a build parameter, in
    /// the same way as the other elements in this repository.
    /// </remarks>
    public class AgentSignatureElementBuilder
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<AgentSignatureData> _dataLogger;

        /// <summary>
        /// The settings being built up.
        /// </summary>
        protected AgentSignatureConfiguration Configuration { get; } =
            new AgentSignatureConfiguration();

        /// <summary>
        /// Construct a builder.
        /// </summary>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the logger factory is null.
        /// </exception>
        public AgentSignatureElementBuilder(ILoggerFactory loggerFactory)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }
            _loggerFactory = loggerFactory;
            _dataLogger = loggerFactory.CreateLogger<AgentSignatureData>();
        }

        /// <summary>
        /// Set the client the element fetches key directories and agent
        /// cards with. By default the element makes a client of its own and
        /// disposes it with itself. A client set here is not disposed by the
        /// element, because whoever supplied it owns it.
        /// </summary>
        /// <param name="httpClient">The client.</param>
        /// <returns>This builder.</returns>
        public AgentSignatureElementBuilder SetHttpClient(
            HttpClient httpClient)
        {
            Configuration.HttpClient = httpClient;
            return this;
        }

        /// <summary>
        /// Set the number of key directories held in the cache.
        /// </summary>
        /// <param name="size">The number of directories.</param>
        /// <returns>This builder.</returns>
        public AgentSignatureElementBuilder SetCacheSize(int size)
        {
            Configuration.CacheSize = size;
            return this;
        }

        /// <summary>
        /// Set how long a fetched key directory is reused for. Once a
        /// directory is older than this, the next request that needs it
        /// starts a fresh fetch and is answered from the copy already held
        /// whilst that runs.
        /// </summary>
        /// <param name="lifetime">The period.</param>
        /// <returns>This builder.</returns>
        public AgentSignatureElementBuilder SetCacheLifetime(
            TimeSpan lifetime)
        {
            Configuration.CacheLifetime = lifetime;
            return this;
        }

        /// <summary>
        /// Set how long a failed fetch is remembered for, so that an outage
        /// at one agent does not cause a fetch on every request.
        /// </summary>
        /// <param name="lifetime">The period.</param>
        /// <returns>This builder.</returns>
        public AgentSignatureElementBuilder SetNegativeCacheLifetime(
            TimeSpan lifetime)
        {
            Configuration.NegativeCacheLifetime = lifetime;
            return this;
        }

        /// <summary>
        /// Set how long a request waits for a key directory fetch before it
        /// reports the Timeout status. The fetch keeps running when the
        /// budget runs out, so the next request from that agent finds the
        /// result.
        /// </summary>
        /// <param name="waitBudget">The period.</param>
        /// <returns>This builder.</returns>
        public AgentSignatureElementBuilder SetWaitBudget(
            TimeSpan waitBudget)
        {
            Configuration.WaitBudget = waitBudget;
            return this;
        }

        /// <summary>
        /// Set the time limit on a single key directory fetch.
        /// </summary>
        /// <param name="fetchTimeout">The period.</param>
        /// <returns>This builder.</returns>
        public AgentSignatureElementBuilder SetFetchTimeout(
            TimeSpan fetchTimeout)
        {
            Configuration.FetchTimeout = fetchTimeout;
            return this;
        }

        /// <summary>
        /// Set the tolerance on the 'created' and 'expires' signature
        /// parameters, which allows for clocks that differ a little.
        /// </summary>
        /// <param name="clockSkew">The tolerance.</param>
        /// <returns>This builder.</returns>
        public AgentSignatureElementBuilder SetClockSkew(TimeSpan clockSkew)
        {
            Configuration.ClockSkew = clockSkew;
            return this;
        }

        /// <summary>
        /// Set the longest a signature may be valid for. A signature valid
        /// for longer than this reports Invalid with the Expired reason.
        /// Zero, the default, places no limit, because the protocol draft
        /// only recommends one.
        /// </summary>
        /// <param name="maxLifetime">The limit, or zero for no limit.</param>
        /// <returns>This builder.</returns>
        public AgentSignatureElementBuilder SetMaxLifetime(
            TimeSpan maxLifetime)
        {
            Configuration.MaxLifetime = maxLifetime;
            return this;
        }

        /// <summary>
        /// Add a registry of agent cards, being a text document that lists
        /// one agent card URL per line. Call this more than once to add more
        /// than one registry. No registry is read by default.
        /// </summary>
        /// <param name="url">The registry URL.</param>
        /// <returns>This builder.</returns>
        public AgentSignatureElementBuilder SetRegistry(string url)
        {
            if (string.IsNullOrEmpty(url) == false)
            {
                Configuration.Registries.Add(url);
            }
            return this;
        }

        /// <summary>
        /// Set whether the bare quoted string form of the 'Signature-Agent'
        /// header, which the earlier drafts used, is accepted.
        /// </summary>
        /// <param name="allow">True to accept it.</param>
        /// <returns>This builder.</returns>
        public AgentSignatureElementBuilder SetAllowLegacySignatureAgent(
            bool allow)
        {
            Configuration.AllowLegacySignatureAgent = allow;
            return this;
        }

        /// <summary>
        /// Set the source of the current time, so that a test can check
        /// signatures whose times are fixed.
        /// </summary>
        /// <param name="clock">The source of the current time.</param>
        /// <returns>This builder.</returns>
        internal AgentSignatureElementBuilder SetClock(
            Func<DateTimeOffset> clock)
        {
            Configuration.Clock = clock;
            return this;
        }

        /// <summary>
        /// Build the element.
        /// </summary>
        /// <returns>The element.</returns>
        public AgentSignatureElement Build()
        {
            return new AgentSignatureElement(
                _loggerFactory.CreateLogger<AgentSignatureElement>(),
                CreateData,
                Configuration);
        }

        private IAgentSignatureData CreateData(
            IPipeline pipeline,
            FlowElementBase<IAgentSignatureData, IElementPropertyMetaData>
                element)
        {
            return new AgentSignatureData(_dataLogger, pipeline);
        }
    }
}
