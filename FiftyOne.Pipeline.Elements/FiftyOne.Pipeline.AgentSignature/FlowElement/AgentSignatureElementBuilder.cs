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
using FiftyOne.Pipeline.Core.Attributes;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.Net.Http;

namespace FiftyOne.Pipeline.AgentSignature.FlowElement
{
    /// <summary>
    /// Builder for the <see cref="AgentSignatureElement"/>.
    /// </summary>
    /// <remarks>
    /// Most options can also be set from a pipeline configuration file, by
    /// naming the method without its 'Set' prefix as a build parameter, in
    /// the same way as the other elements in this repository. The client
    /// is the exception, because a configuration file holds text and there
    /// is no way to write an <see cref="HttpClient"/> as text, so
    /// <see cref="SetHttpClient(HttpClient)"/> is marked as code only.
    /// </remarks>
    public class AgentSignatureElementBuilder
    {
        private readonly ILogger<AgentSignatureData> _dataLogger;

        /// <summary>
        /// The settings being built up.
        /// </summary>
        protected AgentSignatureConfiguration Configuration { get; } =
            new AgentSignatureConfiguration();

        /// <summary>
        /// The logger factory, so that a builder deriving from this one
        /// can make the loggers the element it builds needs.
        /// </summary>
        protected ILoggerFactory LoggerFactory { get; }

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
            LoggerFactory = loggerFactory;
            _dataLogger = loggerFactory.CreateLogger<AgentSignatureData>();
        }

        /// <summary>
        /// Check that a period setting is one the element can work with.
        /// A negative period, or one longer than a year, would otherwise
        /// reach the request path and throw there, where the request that
        /// happened to arrive first would carry the blame for a setting
        /// made at start up.
        /// </summary>
        /// <param name="value">The period given.</param>
        /// <param name="name">The parameter name to report.</param>
        /// <returns>The period.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the period is negative or longer than a year.
        /// </exception>
        private static TimeSpan CheckPeriod(TimeSpan value, string name)
        {
            if (value < TimeSpan.Zero ||
                value > Constants.MAXIMUM_PERIOD)
            {
                throw new ArgumentOutOfRangeException(
                    name,
                    value,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        Messages.ExceptionPeriodOutOfRange,
                        Constants.MAXIMUM_PERIOD));
            }
            return value;
        }

        /// <summary>
        /// Set the client the element fetches key directories and agent
        /// cards with. By default the element makes a client of its own and
        /// disposes it with itself. A client set here is not disposed by the
        /// element, because whoever supplied it owns it.
        /// </summary>
        /// <param name="httpClient">The client.</param>
        /// <returns>This builder.</returns>
        [CodeConfigOnly]
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
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the size is less than one.
        /// </exception>
        [DefaultValue(Constants.DEFAULT_CACHE_SIZE)]
        public AgentSignatureElementBuilder SetCacheSize(int size)
        {
            if (size < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(size), size, Messages.ExceptionCacheSizeTooSmall);
            }
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
        [DefaultValue("1.00:00:00")]
        public AgentSignatureElementBuilder SetCacheLifetime(
            TimeSpan lifetime)
        {
            Configuration.CacheLifetime =
                CheckPeriod(lifetime, nameof(lifetime));
            return this;
        }

        /// <summary>
        /// Set how long a failed fetch is remembered for, so that an outage
        /// at one agent does not cause a fetch on every request.
        /// </summary>
        /// <param name="lifetime">The period.</param>
        /// <returns>This builder.</returns>
        [DefaultValue("00:05:00")]
        public AgentSignatureElementBuilder SetNegativeCacheLifetime(
            TimeSpan lifetime)
        {
            Configuration.NegativeCacheLifetime =
                CheckPeriod(lifetime, nameof(lifetime));
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
        [DefaultValue("00:00:00.350")]
        public AgentSignatureElementBuilder SetWaitBudget(
            TimeSpan waitBudget)
        {
            Configuration.WaitBudget =
                CheckPeriod(waitBudget, nameof(waitBudget));
            return this;
        }

        /// <summary>
        /// Set the time limit on a single key directory fetch.
        /// </summary>
        /// <param name="fetchTimeout">The period.</param>
        /// <returns>This builder.</returns>
        [DefaultValue("00:00:05")]
        public AgentSignatureElementBuilder SetFetchTimeout(
            TimeSpan fetchTimeout)
        {
            Configuration.FetchTimeout =
                CheckPeriod(fetchTimeout, nameof(fetchTimeout));
            return this;
        }

        /// <summary>
        /// Set the tolerance on the 'created' and 'expires' signature
        /// parameters, which allows for clocks that differ a little.
        /// </summary>
        /// <param name="clockSkew">The tolerance.</param>
        /// <returns>This builder.</returns>
        [DefaultValue("00:01:00")]
        public AgentSignatureElementBuilder SetClockSkew(TimeSpan clockSkew)
        {
            Configuration.ClockSkew =
                CheckPeriod(clockSkew, nameof(clockSkew));
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
        [DefaultValue("00:00:00")]
        public AgentSignatureElementBuilder SetMaxLifetime(
            TimeSpan maxLifetime)
        {
            Configuration.MaxLifetime =
                CheckPeriod(maxLifetime, nameof(maxLifetime));
            return this;
        }

        /// <summary>
        /// Add a registry of agent cards, being a text document that lists
        /// one agent card URL per line. Call this more than once to add more
        /// than one registry. No registry is read by default.
        /// </summary>
        /// <remarks>
        /// The registries are read once for the life of the process, so a
        /// registry or card that cannot be fetched at that read, for any
        /// reason including a network fault, is not tried again until the
        /// process restarts. This is unlike a key directory fetch, which is
        /// retried and recovers on its own. A missing card never changes a
        /// signature status, only the card properties reported alongside
        /// one.
        /// </remarks>
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
        /// Set the address of a key directory to fetch once at start up,
        /// so that the log says whether this deployment can reach the keys
        /// agents publish at all. No check is made by default.
        /// </summary>
        /// <remarks>
        /// A deployment with no outbound access answers every signed
        /// request Unverified, one request at a time, which reads as
        /// agents behaving oddly rather than as a deployment that cannot
        /// do the work. Point this at a directory that is expected to be
        /// reachable, and one line at start up says which it is. The check
        /// is made in the background, changes nothing about how requests
        /// are answered, and never stops the element being built, because
        /// an element that reached the network whilst being built would
        /// stop a site starting at all when the network was down.
        /// </remarks>
        /// <param name="url">The address, or null for no check.</param>
        /// <returns>This builder.</returns>
        public AgentSignatureElementBuilder SetReachabilityCheckUrl(
            string url)
        {
            Configuration.ReachabilityCheckUrl = url;
            return this;
        }

        /// <summary>
        /// Set whether the bare quoted string form of the 'Signature-Agent'
        /// header, which the earlier drafts used, is accepted.
        /// </summary>
        /// <param name="allow">True to accept it.</param>
        /// <returns>This builder.</returns>
        [DefaultValue(Constants.DEFAULT_ALLOW_LEGACY_SIGNATURE_AGENT)]
        public AgentSignatureElementBuilder SetAllowLegacySignatureAgent(
            bool allow)
        {
            Configuration.AllowLegacySignatureAgent = allow;
            return this;
        }

        /// <summary>
        /// Set whether a key set carried inline in a 'data:' URI is
        /// accepted. This is off by default and should stay off wherever
        /// requests arrive from the public internet. A key set sent in the
        /// header is chosen by whoever sent the request, so a signature
        /// that checks out against it shows only that the sender holds the
        /// matching private key and says nothing about which agent sent
        /// the request. Turn it on only where every caller is already
        /// trusted, such as a test harness.
        /// </summary>
        /// <param name="allow">True to accept an inline key set.</param>
        /// <returns>This builder.</returns>
        [DefaultValue(Constants.DEFAULT_ALLOW_INLINE_DIRECTORY)]
        public AgentSignatureElementBuilder SetAllowInlineDirectory(
            bool allow)
        {
            Configuration.AllowInlineDirectory = allow;
            return this;
        }

        /// <summary>
        /// Set how many bytes are read from a key directory, an agent card
        /// or a registry before the fetch is abandoned. The address fetched
        /// is chosen by whoever sent the request, so the limit stops one
        /// request asking the element to read an endless document.
        /// </summary>
        /// <param name="bytes">The limit.</param>
        /// <returns>This builder.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the limit is less than one.
        /// </exception>
        [DefaultValue(Constants.DEFAULT_MAX_RESPONSE_BYTES)]
        public AgentSignatureElementBuilder SetMaxResponseBytes(int bytes)
        {
            if (bytes < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bytes),
                    bytes,
                    Messages.ExceptionMaxResponseBytesTooSmall);
            }
            Configuration.MaxResponseBytes = bytes;
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
        public virtual AgentSignatureElement Build()
        {
            return new AgentSignatureElement(
                LoggerFactory.CreateLogger<AgentSignatureElement>(),
                CreateData,
                Configuration);
        }

        /// <summary>
        /// Make this element's data. A builder deriving from this one
        /// hands the same factory to whatever element it builds, so that
        /// the data type does not have to be repeated.
        /// </summary>
        /// <param name="pipeline">The pipeline the element sits in.</param>
        /// <param name="element">The element.</param>
        /// <returns>The element data.</returns>
        protected IAgentSignatureData CreateData(
            IPipeline pipeline,
            FlowElementBase<IAgentSignatureData, IElementPropertyMetaData>
                element)
        {
            return new AgentSignatureData(_dataLogger, pipeline);
        }
    }
}
