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
using FiftyOne.Pipeline.AgentSignature.Keys;
using FiftyOne.Pipeline.AgentSignature.Parsing;
using FiftyOne.Pipeline.AgentSignature.Verification;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.AgentSignature.FlowElement
{
    /// <summary>
    /// Reads the three HTTP headers an automated agent sends when it signs
    /// its request under the IETF Web Bot Auth protocol, checks the
    /// signature against the public key the agent publishes, and reports
    /// what the signature proves.
    /// </summary>
    /// <remarks>
    /// A request with no signature reports the Absent status, which is the
    /// normal case, because only a handful of agents sign today. An absent
    /// signature is never evidence against a request.
    /// </remarks>
    public class AgentSignatureElement :
        FlowElementBase<IAgentSignatureData, IElementPropertyMetaData>
    {
        private readonly IEvidenceKeyFilter _evidenceKeyFilter;
        private readonly IList<IElementPropertyMetaData> _properties;
        private readonly AgentSignatureConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly bool _ownsHttpClient;
        private readonly DirectoryCache _cache;
        private readonly DirectoryFetcher _fetcher;
        private readonly Lazy<Task<IDictionary<string, AgentCard>>>
            _cardsByKeyUrl;
        private readonly ConcurrentDictionary<string, bool>
            _sharedSecretKeysLogged =
                new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);

        /// <summary>
        /// The number of key ids remembered for the once per key warning
        /// about shared secrets, after which no more are remembered and no
        /// more warnings are written.
        /// </summary>
        private const int MAXIMUM_LOGGED_KEYS = 1000;

        /// <summary>
        /// Construct an element.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="elementDataFactory">
        /// The factory that makes the element data.
        /// </param>
        /// <param name="configuration">
        /// The settings the element runs with.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the configuration is null.
        /// </exception>
        public AgentSignatureElement(
            ILogger<AgentSignatureElement> logger,
            Func<IPipeline,
                FlowElementBase<IAgentSignatureData, IElementPropertyMetaData>,
                IAgentSignatureData> elementDataFactory,
            AgentSignatureConfiguration configuration)
            : base(logger, elementDataFactory)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }
            _configuration = configuration;

            _evidenceKeyFilter = new AgentSignatureEvidenceKeyFilter();

            _properties = new List<IElementPropertyMetaData>()
            {
                Text(Constants.PROPERTY_STATUS),
                Text(Constants.PROPERTY_REASON),
                Text(Constants.PROPERTY_AGENT),
                Text(Constants.PROPERTY_KEY_ID),
                Text(Constants.PROPERTY_ALGORITHM),
                Time(Constants.PROPERTY_CREATED),
                Time(Constants.PROPERTY_EXPIRES),
                Text(Constants.PROPERTY_NONCE),
                Text(Constants.PROPERTY_PURPOSE),
                Text(Constants.PROPERTY_NAME),
                Text(Constants.PROPERTY_PRODUCT_TOKEN),
                Text(Constants.PROPERTY_CARD_URL),
            };

            _ownsHttpClient = configuration.HttpClient == null;
            // A client the element makes for itself does not follow
            // redirects. The address fetched is chosen by whoever sent the
            // request, and a redirect would move the fetch somewhere the
            // checks made before the request never saw. A client supplied
            // through the builder is used as it was given, so whoever
            // supplies one decides that for themselves.
            _httpClient = configuration.HttpClient ??
                new HttpClient(new HttpClientHandler()
                {
                    AllowAutoRedirect = false,
                });
            _fetcher = new DirectoryFetcher(
                _httpClient,
                logger,
                configuration.Clock,
                configuration.MaxResponseBytes);
            _cache = new DirectoryCache(
                _fetcher,
                configuration.Clock,
                configuration.CacheSize,
                configuration.CacheLifetime,
                configuration.NegativeCacheLifetime,
                configuration.WaitBudget,
                configuration.FetchTimeout,
                configuration.Concurrency);
            _cardsByKeyUrl =
                new Lazy<Task<IDictionary<string, AgentCard>>>(LoadCards);
        }

        /// <inheritdoc/>
        public override string ElementDataKey =>
            Constants.DEFAULT_ELEMENT_DATA_KEY;

        /// <inheritdoc/>
        public override IEvidenceKeyFilter EvidenceKeyFilter =>
            _evidenceKeyFilter;

        /// <inheritdoc/>
        public override IList<IElementPropertyMetaData> Properties =>
            _properties;

        /// <summary>
        /// The number of key directory fetches that have been started. This
        /// is here so that the tests can check that a burst of requests from
        /// one agent causes one fetch.
        /// </summary>
        internal int FetchCount => _cache.FetchCount;

        /// <inheritdoc/>
        protected override void ProcessInternal(IFlowData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            var elementData = data.GetOrAdd(
                ElementDataKeyTyped, CreateElementData) as AgentSignatureData;
            if (elementData == null)
            {
                return;
            }

            // Nearly every request has no signature at all, so answer that
            // case before anything is parsed or allocated.
            var hasSignature = data.TryGetEvidence(
                Constants.EVIDENCE_SIGNATURE_KEY, out object signatureValue);
            var hasInput = data.TryGetEvidence(
                Constants.EVIDENCE_SIGNATURE_INPUT_KEY, out object inputValue);
            if (hasSignature == false && hasInput == false)
            {
                ApplyAbsent(elementData);
                return;
            }

            var outcome = Evaluate(
                data,
                hasSignature ? signatureValue?.ToString() : null,
                hasInput ? inputValue?.ToString() : null);
            Apply(outcome, elementData);
        }

        /// <inheritdoc/>
        protected override void ManagedResourcesCleanup()
        {
            _cache.Dispose();
            if (_ownsHttpClient)
            {
                _httpClient.Dispose();
            }
        }

        /// <inheritdoc/>
        protected override void UnmanagedResourcesCleanup()
        {
        }

        private AgentSignatureOutcome Evaluate(
            IFlowData data,
            string signatureHeader,
            string inputHeader)
        {
            var outcome = new AgentSignatureOutcome
            {
                MissingDetailMessage = Messages.NoValueDetailNotSent,
            };

            if (signatureHeader == null ||
                inputHeader == null ||
                StructuredFieldParser.TryParseDictionary(
                    inputHeader, out var input) == false ||
                StructuredFieldParser.TryParseDictionary(
                    signatureHeader, out var signature) == false ||
                SignatureCandidate.TryBuild(
                    input, signature, out var candidates) == false)
            {
                return Fail(
                    outcome,
                    Constants.STATUS_INVALID,
                    Constants.REASON_MALFORMED,
                    Messages.NoValueDetailMalformed);
            }

            IList<SignatureAgentEntry> agents = null;
            if (data.TryGetEvidence(
                Constants.EVIDENCE_SIGNATURE_AGENT_KEY,
                out object agentValue))
            {
                if (SignatureAgentEntry.TryParse(
                    agentValue?.ToString(),
                    _configuration.AllowLegacySignatureAgent,
                    out agents) == false)
                {
                    return Fail(
                        outcome,
                        Constants.STATUS_INVALID,
                        Constants.REASON_MALFORMED,
                        Messages.NoValueDetailMalformed);
                }
                if (agents.Count > 0)
                {
                    outcome.Agent = agents[0].Value;
                }
            }

            // The protocol draft says to work with the first signature that
            // carries the Web Bot Auth tag and to discard the others.
            SignatureCandidate candidate = null;
            foreach (var possible in candidates)
            {
                if (string.Equals(
                    possible.Tag,
                    Constants.TAG_WEB_BOT_AUTH,
                    StringComparison.Ordinal))
                {
                    candidate = possible;
                    break;
                }
            }
            if (candidate == null)
            {
                return Fail(
                    outcome,
                    Constants.STATUS_INVALID,
                    Constants.REASON_TAG_MISMATCH,
                    Messages.NoValueDetailWrongTag);
            }

            outcome.KeyId = candidate.KeyId;
            outcome.Nonce = candidate.Nonce;
            outcome.Algorithm = candidate.Algorithm;
            // A 'created' or 'expires' outside the range of times the
            // framework can hold is not a time at all, so the signature
            // parameters cannot be read. Carrying such a value forward as
            // the earliest time there is would make a signature claiming a
            // created far in the future read as one made long ago, and it
            // would then pass the check below rather than failing it.
            if (candidate.Created.HasValue)
            {
                if (TryFromUnixSeconds(
                    candidate.Created.Value, out var created) == false)
                {
                    return Fail(
                        outcome,
                        Constants.STATUS_INVALID,
                        Constants.REASON_MALFORMED,
                        Messages.NoValueDetailMalformed);
                }
                outcome.Created = created;
            }
            if (candidate.Expires.HasValue)
            {
                if (TryFromUnixSeconds(
                    candidate.Expires.Value, out var expires) == false)
                {
                    return Fail(
                        outcome,
                        Constants.STATUS_INVALID,
                        Constants.REASON_MALFORMED,
                        Messages.NoValueDetailMalformed);
                }
                outcome.Expires = expires;
            }

            if (candidate.Created.HasValue == false ||
                candidate.Expires.HasValue == false ||
                string.IsNullOrEmpty(candidate.KeyId))
            {
                return Fail(
                    outcome,
                    Constants.STATUS_INVALID,
                    Constants.REASON_MISSING_PARAMETER,
                    Messages.NoValueDetailNotSent);
            }

            var timeFailure = CheckTimes(outcome);
            if (timeFailure != null)
            {
                return Fail(
                    outcome,
                    Constants.STATUS_INVALID,
                    timeFailure,
                    Messages.NoValueDetailNotSent);
            }

            var agent = FindAgent(candidate, agents);
            if (agent == null)
            {
                return Fail(
                    outcome,
                    Constants.STATUS_UNVERIFIED,
                    Constants.REASON_NO_AGENT,
                    Messages.NoValueDetailNoAgent);
            }
            outcome.Agent = agent.Value;

            // A key set carried in the header is chosen by whoever sent
            // the request, so a signature that checks out against it shows
            // only that the sender holds the matching private key. That is
            // not what this element reports, so the key set is refused
            // unless the caller has said their traffic is already trusted.
            if (agent.InlineDirectory != null &&
                _configuration.AllowInlineDirectory == false)
            {
                return Fail(
                    outcome,
                    Constants.STATUS_UNVERIFIED,
                    Constants.REASON_INLINE_DIRECTORY,
                    Messages.NoValueDetailInlineDirectory);
            }

            // The protocol draft has an agent cover '@authority' or
            // '@target-uri', so that the signature says something about
            // the request it arrived on. A signature covering neither
            // would check out just as well against a request to any other
            // site, so one captured anywhere could be replayed here.
            if (CoversTheRequest(candidate) == false)
            {
                return Fail(
                    outcome,
                    Constants.STATUS_INVALID,
                    Constants.REASON_UNBOUND_SIGNATURE,
                    Messages.NoValueDetailUnbound);
            }

            var resolver = new FlowDataComponentResolver(data);
            if (SignatureBase.TryBuild(
                candidate.CoveredComponents,
                candidate.SignatureParams,
                resolver,
                out var signatureBase) == false)
            {
                return Fail(
                    outcome,
                    Constants.STATUS_UNVERIFIED,
                    Constants.REASON_COMPONENT_UNAVAILABLE,
                    Messages.NoValueDetailNotSent);
            }

            var lookup = _cache.Lookup(
                agent, data.GetStopToken(), out var entry);
            if (lookup == DirectoryLookupOutcome.Pending)
            {
                LogDirectoryPending(agent.KeyUrl);
                return Fail(
                    outcome,
                    Constants.STATUS_TIMEOUT,
                    Constants.REASON_DIRECTORY_PENDING,
                    Messages.NoValueDetailNotSent);
            }
            if (entry.Success == false)
            {
                return Fail(
                    outcome,
                    Constants.STATUS_UNVERIFIED,
                    Constants.REASON_DIRECTORY_UNAVAILABLE,
                    Messages.NoValueDetailNotSent);
            }

            outcome.DirectoryWasRead = true;
            outcome.Purpose = entry.Directory.Purpose;
            ApplyCard(outcome, entry, agent, data.GetStopToken());

            var key = entry.Directory.FindKey(candidate.KeyId);
            if (key == null)
            {
                return Fail(
                    outcome,
                    Constants.STATUS_INVALID,
                    Constants.REASON_UNKNOWN_KEY,
                    Messages.NoValueDetailNotSent);
            }
            if (key.IsValidAt(outcome.Created.Value) == false)
            {
                return Fail(
                    outcome,
                    Constants.STATUS_INVALID,
                    Constants.REASON_KEY_EXPIRED,
                    Messages.NoValueDetailNotSent);
            }

            var algorithm = SignatureVerifier.ResolveAlgorithm(
                key, candidate.Algorithm);
            outcome.Algorithm = algorithm.Name;
            if (algorithm.Supported == false)
            {
                ReportUnsupportedAlgorithm(algorithm.Name, candidate.KeyId);
                return Fail(
                    outcome,
                    Constants.STATUS_UNVERIFIED,
                    Constants.REASON_UNSUPPORTED_ALGORITHM,
                    Messages.NoValueDetailNotSent);
            }

            if (SignatureVerifier.Verify(
                algorithm.Name,
                key,
                Encoding.ASCII.GetBytes(signatureBase),
                candidate.Signature) == false)
            {
                return Fail(
                    outcome,
                    Constants.STATUS_INVALID,
                    Constants.REASON_SIGNATURE_MISMATCH,
                    Messages.NoValueDetailNotSent);
            }

            outcome.Status = Constants.STATUS_VERIFIED;
            outcome.Reason = Constants.REASON_VERIFIED;
            return outcome;
        }

        private string CheckTimes(AgentSignatureOutcome outcome)
        {
            // The comparisons are made on ticks rather than on whole
            // DateTimeOffset values. A signature may claim a 'created' or
            // an 'expires' far outside the range the framework can hold,
            // which arrives here as the smallest or largest value it can
            // hold, and adding or subtracting the skew from either of
            // those throws. The builder holds the skew and the maximum
            // lifetime to a range that keeps the arithmetic below inside
            // what a long can carry.
            var now = _configuration.Clock().UtcTicks;
            var skew = _configuration.ClockSkew.Ticks;
            var created = outcome.Created.Value.UtcTicks;
            var expires = outcome.Expires.Value.UtcTicks;
            if (created - skew > now)
            {
                return Constants.REASON_NOT_YET_VALID;
            }
            if (expires + skew < now)
            {
                return Constants.REASON_EXPIRED;
            }
            if (_configuration.MaxLifetime > TimeSpan.Zero &&
                expires - created > _configuration.MaxLifetime.Ticks)
            {
                return Constants.REASON_EXPIRED;
            }
            return null;
        }

        /// <summary>
        /// Whether the signature covers something that ties it to the
        /// request it arrived on. The protocol draft has an agent cover
        /// '@authority' or '@target-uri', and a signature covering neither
        /// would check out against a request sent to any other site, so
        /// one captured elsewhere could be replayed here.
        /// </summary>
        /// <param name="candidate">The signature being read.</param>
        /// <returns>True when the signature is tied to the request.</returns>
        private static bool CoversTheRequest(SignatureCandidate candidate)
        {
            foreach (var component in candidate.CoveredComponents)
            {
                if (component.Value is string name &&
                    (string.Equals(
                        name, "@authority", StringComparison.Ordinal) ||
                    string.Equals(
                        name, "@target-uri", StringComparison.Ordinal)))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Find the 'Signature-Agent' member that the signature covers. A
        /// member the signature does not cover says nothing about the agent,
        /// because anyone can add a header to a request, so a signature that
        /// covers none is treated as naming no agent at all.
        /// </summary>
        private static SignatureAgentEntry FindAgent(
            SignatureCandidate candidate,
            IList<SignatureAgentEntry> agents)
        {
            if (agents == null || agents.Count == 0)
            {
                return null;
            }
            foreach (var component in candidate.CoveredComponents)
            {
                if ((component.Value is string name) == false ||
                    string.Equals(
                        name,
                        "signature-agent",
                        StringComparison.Ordinal) == false)
                {
                    continue;
                }
                var label = component.GetStringParameter("key");
                if (label == null)
                {
                    // The whole header is covered, so it has to name one
                    // agent for there to be no doubt which one signed.
                    return agents.Count == 1 ? agents[0] : null;
                }
                foreach (var agent in agents)
                {
                    if (string.Equals(
                        agent.Label, label, StringComparison.Ordinal))
                    {
                        return agent;
                    }
                }
                return null;
            }
            return null;
        }

        private void ApplyCard(
            AgentSignatureOutcome outcome,
            DirectoryEntry entry,
            SignatureAgentEntry agent,
            CancellationToken stopToken)
        {
            var card = entry.Card ??
                FindCardInRegistries(agent.KeyUrl, stopToken);
            if (card == null)
            {
                return;
            }
            outcome.Name = card.ClientName;
            outcome.ProductToken = card.ProductToken;
            outcome.CardUrl = card.ClientId;
            if (string.IsNullOrEmpty(outcome.Purpose))
            {
                outcome.Purpose = card.Purpose;
            }
        }

        private AgentCard FindCardInRegistries(
            string keyUrl,
            CancellationToken stopToken)
        {
            if (_configuration.Registries.Count == 0 || keyUrl == null)
            {
                return null;
            }
            try
            {
                var task = _cardsByKeyUrl.Value;
                // The registries are read once, in the background. A
                // request that arrives before that finishes simply gets no
                // agent card, because a card never changes whether a
                // signature is valid. The stop token is honoured here as
                // it is on the directory wait, so a request whose caller
                // has already gone does not sit out the budget.
                if (task.Wait(
                    (int)Math.Min(
                        _configuration.WaitBudget.TotalMilliseconds,
                        int.MaxValue),
                    stopToken) == false)
                {
                    return null;
                }
                return task.Result.TryGetValue(keyUrl, out var card)
                    ? card
                    : null;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception)
#pragma warning restore CA1031
            {
                // An agent card only adds description to a result that is
                // already decided, so nothing here is worth failing a
                // request for, whether the wait was cancelled or the load
                // threw where it was not expected to.
                return null;
            }
        }

        private async Task<IDictionary<string, AgentCard>> LoadCards()
        {
            var result = new Dictionary<string, AgentCard>(
                StringComparer.Ordinal);
            try
            {
                using (var source = new CancellationTokenSource(
                    _configuration.FetchTimeout))
                {
                    // The whole load shares one time limit, and the cards
                    // are fetched one after another, so both loops stop
                    // once it runs out. Without the checks every card left
                    // in a long registry would still be asked for with a
                    // token that has already fired, throwing and being
                    // logged once each for no result.
                    foreach (var registry in _configuration.Registries)
                    {
                        if (source.Token.IsCancellationRequested)
                        {
                            break;
                        }
                        var cardUrls = await _fetcher
                            .FetchRegistryAsync(registry, source.Token)
                            .ConfigureAwait(false);
                        foreach (var cardUrl in cardUrls)
                        {
                            if (source.Token.IsCancellationRequested)
                            {
                                break;
                            }
                            var card = await _fetcher
                                .FetchCardAsync(cardUrl, source.Token)
                                .ConfigureAwait(false);
                            if (card != null &&
                                string.IsNullOrEmpty(card.JwksUri) == false)
                            {
                                result[card.JwksUri] = card;
                            }
                        }
                    }
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception exception)
#pragma warning restore CA1031
            {
                // The catch is deliberately broad, for the same reason as the
                // one in the directory cache. A request reads this task with
                // Result, and an agent card never changes whether a signature
                // is valid, so a registry that cannot be read must cost the
                // request nothing more than the card properties.
                Logger.LogWarning(string.Format(
                    CultureInfo.InvariantCulture,
                    Messages.LogRegistryFetchFailed,
                    string.Join(", ", _configuration.Registries),
                    exception.Message));
            }
            return result;
        }

        private static AgentSignatureOutcome Fail(
            AgentSignatureOutcome outcome,
            string status,
            string reason,
            string missingDetailMessage)
        {
            outcome.Status = status;
            outcome.Reason = reason;
            outcome.MissingDetailMessage = missingDetailMessage;
            return outcome;
        }

        /// <summary>
        /// Read a signature time, given as a count of seconds since the
        /// start of 1970.
        /// </summary>
        /// <param name="seconds">The count of seconds.</param>
        /// <param name="value">The time.</param>
        /// <returns>
        /// False when the count names a time outside the range the
        /// framework can hold, which the caller reports as a signature it
        /// could not read.
        /// </returns>
        private static bool TryFromUnixSeconds(
            long seconds,
            out DateTimeOffset value)
        {
            try
            {
                value = DateTimeOffset.FromUnixTimeSeconds(seconds);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                value = default;
                return false;
            }
        }

        private void ReportUnsupportedAlgorithm(string algorithm, string keyId)
        {
            // The checks are made in this order on purpose. Logging is
            // asked about first, so that a key id is not marked as already
            // logged when nothing would have been written and the line is
            // then lost if logging is turned up later. The count is checked
            // next, because the key id is chosen by whoever holds the
            // directory, so without a bound the set of remembered key ids
            // would grow for as long as the process runs. Once the bound is
            // reached the fault has been reported plenty of times already.
            if (string.Equals(
                    algorithm,
                    Constants.ALGORITHM_HMAC_SHA256,
                    StringComparison.Ordinal) &&
                Logger.IsEnabled(LogLevel.Warning) &&
                _sharedSecretKeysLogged.Count < MAXIMUM_LOGGED_KEYS &&
                _sharedSecretKeysLogged.TryAdd(keyId ?? string.Empty, true))
            {
                // A shared secret arriving from an agent points at a
                // misconfigured agent, which is worth an operator's eye, but
                // only once for each key rather than on every request.
                Logger.LogWarning(string.Format(
                    CultureInfo.InvariantCulture,
                    Messages.LogSharedSecretAlgorithm,
                    algorithm,
                    keyId));
            }
        }

        private void LogDirectoryPending(string keyUrl)
        {
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug(string.Format(
                    CultureInfo.InvariantCulture,
                    Messages.LogDirectoryPending,
                    keyUrl,
                    _configuration.WaitBudget.TotalMilliseconds));
            }
        }

        private static void ApplyAbsent(AgentSignatureData elementData)
        {
            elementData.AgentSignature = SharedValues.StatusAbsent;
            elementData.AgentSignatureReason = SharedValues.ReasonNoSignature;
            elementData.AgentSignatureAgent = SharedValues.AbsentText;
            elementData.AgentSignatureKeyId = SharedValues.AbsentText;
            elementData.AgentSignatureAlgorithm = SharedValues.AbsentText;
            elementData.AgentSignatureCreated = SharedValues.AbsentTime;
            elementData.AgentSignatureExpires = SharedValues.AbsentTime;
            elementData.AgentSignatureNonce = SharedValues.AbsentText;
            elementData.AgentSignaturePurpose = SharedValues.PurposeNotRead;
            elementData.AgentSignatureName = SharedValues.NoCard;
            elementData.AgentSignatureProductToken = SharedValues.NoCard;
            elementData.AgentSignatureCardUrl = SharedValues.NoCard;
        }

        private static void Apply(
            AgentSignatureOutcome outcome,
            AgentSignatureData elementData)
        {
            var missing = outcome.MissingDetailMessage;
            elementData.AgentSignature =
                new AspectPropertyValue<string>(outcome.Status);
            elementData.AgentSignatureReason =
                new AspectPropertyValue<string>(outcome.Reason);
            elementData.AgentSignatureAgent = Text(outcome.Agent, missing);
            elementData.AgentSignatureKeyId = Text(outcome.KeyId, missing);
            elementData.AgentSignatureAlgorithm =
                Text(outcome.Algorithm, missing);
            elementData.AgentSignatureCreated =
                Time(outcome.Created, missing);
            elementData.AgentSignatureExpires =
                Time(outcome.Expires, missing);
            elementData.AgentSignatureNonce = Text(outcome.Nonce, missing);
            elementData.AgentSignaturePurpose = string.IsNullOrEmpty(
                outcome.Purpose)
                ? (outcome.DirectoryWasRead
                    ? SharedValues.PurposeNotStated
                    : SharedValues.PurposeNotRead)
                : new AspectPropertyValue<string>(outcome.Purpose);
            elementData.AgentSignatureName = Card(outcome.Name);
            elementData.AgentSignatureProductToken =
                Card(outcome.ProductToken);
            elementData.AgentSignatureCardUrl = Card(outcome.CardUrl);
        }

        private static IAspectPropertyValue<string> Text(
            string value,
            string missingMessage)
        {
            return string.IsNullOrEmpty(value)
                ? SharedValues.NoText(missingMessage)
                : new AspectPropertyValue<string>(value);
        }

        private static IAspectPropertyValue<DateTimeOffset> Time(
            DateTimeOffset? value,
            string missingMessage)
        {
            return value.HasValue
                ? new AspectPropertyValue<DateTimeOffset>(value.Value)
                : SharedValues.NoTime(missingMessage);
        }

        private static IAspectPropertyValue<string> Card(string value)
        {
            return string.IsNullOrEmpty(value)
                ? SharedValues.NoCard
                : new AspectPropertyValue<string>(value);
        }

        private IElementPropertyMetaData Text(string name)
        {
            return new ElementPropertyMetaData(
                this, name, typeof(IAspectPropertyValue<string>), true);
        }

        private IElementPropertyMetaData Time(string name)
        {
            return new ElementPropertyMetaData(
                this,
                name,
                typeof(IAspectPropertyValue<DateTimeOffset>),
                true);
        }

        /// <summary>
        /// Rebuilds the covered components of a request signature from the
        /// evidence the pipeline holds.
        /// </summary>
        /// <remarks>
        /// A signature may cover any request header, so the element asks
        /// for every header rather than a fixed list. Of the derived
        /// components the web integration puts in enough for '@authority'
        /// and '@scheme' only, so a signature that covers '@target-uri',
        /// '@method', '@path' or '@query' cannot be rebuilt and reads
        /// Unverified with the ComponentUnavailable reason.
        /// </remarks>
        private sealed class FlowDataComponentResolver : IComponentResolver
        {
            private readonly IFlowData _data;

            public FlowDataComponentResolver(IFlowData data)
            {
                _data = data;
            }

            public bool TryResolve(
                string name,
                SfItem component,
                out string value)
            {
                value = null;
                if (name.Length == 0)
                {
                    return false;
                }
                if (name[0] == '@')
                {
                    return TryResolveDerived(name, component, out value);
                }
                // A component of a related request rather than of this one,
                // which only makes sense when signing a response.
                if (component.TryGetParameter("req", out _))
                {
                    return false;
                }
                if (GetHeader(name, out var header) == false)
                {
                    return false;
                }
                var label = component.GetStringParameter("key");
                if (label != null)
                {
                    return TryResolveDictionaryMember(
                        header, label, out value);
                }
                // The strict and byte sequence forms would need the header
                // re-serialised, which this element does not do.
                if (component.TryGetParameter("sf", out _) ||
                    component.TryGetParameter("bs", out _) ||
                    component.TryGetParameter("tr", out _))
                {
                    return false;
                }
                value = header.Trim();
                return true;
            }

            private bool TryResolveDerived(
                string name,
                SfItem component,
                out string value)
            {
                value = null;
                if (component.TryGetParameter("req", out _))
                {
                    return false;
                }
                switch (name)
                {
                    case "@authority":
                        if (GetHeader("host", out var host) == false)
                        {
                            return false;
                        }
                        GetHeaderByKey(
                            Core.Constants.EVIDENCE_PROTOCOL,
                            out var scheme);
                        value = SignatureBase.BuildAuthority(
                            host, scheme?.Trim().ToLowerInvariant());
                        return value != null;
                    case "@scheme":
                        if (GetHeaderByKey(
                            Core.Constants.EVIDENCE_PROTOCOL,
                            out var protocol) == false)
                        {
                            return false;
                        }
                        value = protocol.Trim().ToLowerInvariant();
                        return true;
                    default:
                        // '@target-uri', '@method', '@path' and '@query'
                        // need the request line, which the web integration
                        // does not put into evidence today.
                        return false;
                }
            }

            private static bool TryResolveDictionaryMember(
                string header,
                string label,
                out string value)
            {
                value = null;
                if (StructuredFieldParser.TryParseDictionary(
                    header, out var dictionary) == false)
                {
                    return false;
                }
                if (dictionary.TryGetValue(label, out var member) == false)
                {
                    return false;
                }
                value = member.Raw;
                return true;
            }

            private bool GetHeader(string name, out string value)
            {
                return GetHeaderByKey(
                    Core.Constants.EVIDENCE_HTTPHEADER_PREFIX +
                        Core.Constants.EVIDENCE_SEPERATOR +
                        name,
                    out value);
            }

            private bool GetHeaderByKey(string key, out string value)
            {
                value = null;
                if (_data.TryGetEvidence(key, out object raw) == false ||
                    raw == null)
                {
                    return false;
                }
                value = raw.ToString();
                return true;
            }
        }

        /// <summary>
        /// The evidence this element asks for.
        /// </summary>
        /// <remarks>
        /// A signature names the parts of the request it covers, and it may
        /// name any request header, so the element cannot write down the
        /// list in advance. It asks for every header instead, together with
        /// the protocol, which '@authority' and '@scheme' are built from.
        /// A fixed list would leave a signature covering any other header
        /// unable to be rebuilt, because the web integration only puts
        /// evidence into the request that some element has asked for.
        /// </remarks>
        private sealed class AgentSignatureEvidenceKeyFilter
            : IEvidenceKeyFilter
        {
            private readonly string _headerPrefix =
                Core.Constants.EVIDENCE_HTTPHEADER_PREFIX +
                Core.Constants.EVIDENCE_SEPERATOR;

            /// <inheritdoc/>
            /// <remarks>
            /// The protocol, which '@scheme' and the port in '@authority'
            /// are built from, is itself written as 'header.protocol', so
            /// the one test on the prefix covers it as well.
            /// </remarks>
            public bool Include(string key)
            {
                return key != null &&
                    key.StartsWith(
                        _headerPrefix,
                        StringComparison.OrdinalIgnoreCase);
            }

            /// <inheritdoc/>
            public int? Order(string key)
            {
                return Include(key) ? 100 : (int?)null;
            }
        }
    }
}
