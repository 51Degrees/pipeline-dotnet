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
        private bool _disposing;
        private readonly DirectoryFetcher _fetcher;
        private readonly Lazy<Task<IDictionary<string, AgentCard>>>
            _cardsByKeyUrl;
        private readonly ConcurrentDictionary<string, bool>
            _sharedSecretKeysLogged =
                new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, bool>
            _pendingDirectoriesLogged =
                new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);

        /// <summary>
        /// The number of key ids remembered for the once per key warning
        /// about shared secrets, after which no more are remembered and no
        /// more warnings are written.
        /// </summary>
        private const int MAXIMUM_LOGGED_KEYS = 1000;

        /// <summary>
        /// The number of signatures carrying the Web Bot Auth tag that one
        /// request has checked. The protocol draft's reverse proxy case
        /// sends two, so three leaves room for a longer chain whilst
        /// holding the worst case cost, which is one directory wait per
        /// signature, to a few wait budgets.
        /// </summary>
        private const int MAXIMUM_SIGNATURES_CHECKED = 3;

        /// <summary>
        /// The factory handed to the flow data's GetOrAdd. Writing
        /// the method name at the call site would build a fresh delegate on
        /// every request, spending an allocation before the no signature
        /// early return, so the one delegate is made here and reused.
        /// </summary>
        private readonly Func<IPipeline, IAgentSignatureData> _createData;

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

            _evidenceKeyFilter = new AgentSignatureEvidenceKeyFilter(
                configuration.TrustForwardedEvidence);

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
            _createData = CreateElementData;
            StartReachabilityCheck();
        }

        /// <summary>
        /// Where an address to check has been configured, fetch it once in
        /// the background and say plainly in the log whether this
        /// deployment can reach an agent's keys at all.
        /// </summary>
        /// <remarks>
        /// A deployment with no outbound access answers every signed
        /// request Unverified, one request at a time, which looks like
        /// agents behaving oddly rather than like a deployment that cannot
        /// work. One line at start up says which it is.
        /// <para>
        /// The check runs in the background and its outcome changes
        /// nothing. Nothing is awaited, nothing is thrown and no request
        /// waits for it, because an element that reached the network
        /// whilst being built would stop a site starting at all when the
        /// network was down, which is the fault this repository already
        /// fixed once in issues 44 and 312.
        /// </para>
        /// </remarks>
        private void StartReachabilityCheck()
        {
            if (string.IsNullOrEmpty(_configuration.ReachabilityCheckUrl))
            {
                return;
            }
            var url = _configuration.ReachabilityCheckUrl;
            _ = Task.Run(async () =>
            {
                try
                {
                    using (var source = new CancellationTokenSource(
                        _configuration.FetchTimeout))
                    {
                        var entry = await _fetcher.FetchAsync(
                            url,
                            Constants.AGENT_TYPE_DIRECTORY,
                            source.Token).ConfigureAwait(false);
                        if (entry.Success)
                        {
                            Logger.LogInformation(string.Format(
                                CultureInfo.InvariantCulture,
                                Messages.LogReachabilityGood,
                                url));
                        }
                        // An element disposed whilst the check was still
                        // running reaches here rather than the catch, and
                        // must not raise the alarm either. The fetcher
                        // treats the cancellation and the disposed client
                        // that a shutdown produces as network failures and
                        // hands back a failed entry instead of throwing,
                        // so this branch sees an ordinary shutdown as an
                        // unreachable directory unless it checks too.
                        else if (Volatile.Read(ref _disposing) == false)
                        {
                            Logger.LogError(string.Format(
                                CultureInfo.InvariantCulture,
                                Messages.LogReachabilityBad,
                                url,
                                entry.FailureReason ??
                                    "no reason was given"));
                        }
                    }
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception exception)
#pragma warning restore CA1031
                {
                    // The catch is deliberately broad. This runs on a
                    // thread nobody is waiting on, so anything escaping it
                    // would be an unobserved failure rather than a message
                    // to whoever is reading the log.
                    //
                    // An element disposed whilst the check was still
                    // running takes the client with it, and that is an
                    // ordinary shutdown rather than a deployment that
                    // cannot reach the keys, so it must not raise the
                    // alarm that says signature checking is switched off.
                    if (Volatile.Read(ref _disposing))
                    {
                        return;
                    }
                    Logger.LogError(string.Format(
                        CultureInfo.InvariantCulture,
                        Messages.LogReachabilityBad,
                        url,
                        exception.Message));
                }
            });
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
                ElementDataKeyTyped, _createData) as AgentSignatureData;
            if (elementData == null)
            {
                return;
            }

            // Nearly every request has no signature at all, so answer that
            // case before anything is parsed or allocated.
            //
            // Where the signature came from decides where everything else
            // comes from. A caller's own Pipeline forwards its evidence
            // with the prefix taken off, so a forwarded signature sits
            // under 'query' whilst 'header' holds the call to this server,
            // and mixing the two would check part of one request against
            // part of another. Reading only the prefix the signature came
            // from means a forwarded request whose caller sent no host or
            // no request line reports that a component was unavailable,
            // which says the check could not be made, rather than
            // reporting a mismatch, which would say the agent was lying.
            var source = FindSource(data);
            var hasSignature = TryGetBySource(
                data,
                source,
                Constants.EVIDENCE_SIGNATURE_NAME,
                out var signatureValue);
            var hasInput = TryGetBySource(
                data,
                source,
                Constants.EVIDENCE_SIGNATURE_INPUT_NAME,
                out var inputValue);
            if (hasSignature == false && hasInput == false)
            {
                ApplyAbsent(elementData);
                return;
            }

            var outcome = Evaluate(data, source, signatureValue, inputValue);
            Apply(outcome, elementData);
        }

        /// <summary>
        /// Decide which prefix this request's signature arrived under, so
        /// that every part of the request is read from the same place.
        /// </summary>
        /// <remarks>
        /// A signature under the query prefix was forwarded by a caller's
        /// own Pipeline, which takes the prefix off every key it sends on,
        /// and describes the request that reached the caller. Anything
        /// under the header prefix describes the call this server
        /// received. The two are different requests, so a base built from
        /// both would be a base no agent ever signed.
        /// </remarks>
        /// <param name="data">The flow data.</param>
        /// <returns>The prefix to read this request from.</returns>
        private string FindSource(IFlowData data)
        {
            // Without this, a visitor could put a signature, a host and a
            // path in the address bar of an ordinary page, have the web
            // integration turn them into evidence under the query prefix,
            // and have those checked in place of the request that
            // actually arrived. A signature captured from a genuine agent
            // anywhere could then be replayed here and reported as
            // Verified, which is what covering the authority and the path
            // exists to prevent. Only a service that knows it receives
            // forwarded evidence may turn this on, because forwarded
            // evidence and a typed query string cannot be told apart once
            // they have arrived.
            if (_configuration.TrustForwardedEvidence &&
                (data.TryGetEvidence<object>(
                        QuerySignatureKey, out _) ||
                    data.TryGetEvidence<object>(
                        QuerySignatureInputKey, out _)))
            {
                return Core.Constants.EVIDENCE_QUERY_PREFIX;
            }
            return Core.Constants.EVIDENCE_HTTPHEADER_PREFIX;
        }

        /// <summary>
        /// The two keys that say a request's evidence was forwarded,
        /// built once rather than on every request, because this runs
        /// whether or not a request carries a signature at all.
        /// </summary>
        private static readonly string QuerySignatureKey =
            Core.Constants.EVIDENCE_QUERY_PREFIX +
            Core.Constants.EVIDENCE_SEPERATOR +
            Constants.EVIDENCE_SIGNATURE_NAME;

        private static readonly string QuerySignatureInputKey =
            Core.Constants.EVIDENCE_QUERY_PREFIX +
            Core.Constants.EVIDENCE_SEPERATOR +
            Constants.EVIDENCE_SIGNATURE_INPUT_NAME;

        /// <summary>
        /// Read a value by name from the prefix the signature came from.
        /// </summary>
        /// <param name="data">The flow data.</param>
        /// <param name="source">The prefix to read from.</param>
        /// <param name="name">The name, with no prefix.</param>
        /// <param name="value">The value found.</param>
        /// <returns>True where that prefix carried the name.</returns>
        private static bool TryGetBySource(
            IFlowData data,
            string source,
            string name,
            out string value)
        {
            value = null;
            if (data.TryGetEvidence(
                    source + Core.Constants.EVIDENCE_SEPERATOR + name,
                    out object raw) &&
                raw != null)
            {
                value = raw.ToString();
                return true;
            }
            return false;
        }

        /// <inheritdoc/>
        protected override void ManagedResourcesCleanup()
        {
            // Read by the start up check, which runs on a thread nobody
            // waits for and would otherwise report an ordinary shutdown
            // as a deployment that cannot reach the keys.
            Volatile.Write(ref _disposing, true);
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
            string source,
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
            if (TryGetBySource(
                data,
                source,
                Constants.EVIDENCE_SIGNATURE_AGENT_NAME,
                out var agentValue))
            {
                if (SignatureAgentEntry.TryParse(
                    agentValue,
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

            // Every signature carrying the Web Bot Auth tag is checked,
            // because the protocol draft has a verifier validate each one
            // independently and its reverse proxy case sends two, the
            // agent's own and the proxy's. The first that verifies answers
            // the request, and where none does the first tagged
            // signature's outcome is reported, so a request carrying one
            // signature reads exactly as before. The number checked is
            // bounded, because each one can cost a directory wait and the
            // header is written by the sender, so an unbounded loop would
            // hand the sender a way to hold a request thread through one
            // long header.
            AgentSignatureOutcome firstTagged = null;
            var checked_ = 0;
            foreach (var possible in candidates)
            {
                if (string.Equals(
                    possible.Tag,
                    Constants.TAG_WEB_BOT_AUTH,
                    StringComparison.Ordinal) == false)
                {
                    continue;
                }
                if (checked_ >= MAXIMUM_SIGNATURES_CHECKED)
                {
                    break;
                }
                checked_++;
                var attempt = new AgentSignatureOutcome
                {
                    MissingDetailMessage = Messages.NoValueDetailNotSent,
                    Agent = outcome.Agent,
                };
                var result = EvaluateCandidate(
                    data, source, possible, agents, attempt);
                if (string.Equals(
                    result.Status,
                    Constants.STATUS_VERIFIED,
                    StringComparison.Ordinal))
                {
                    return result;
                }
                if (firstTagged == null)
                {
                    firstTagged = result;
                }
            }
            if (firstTagged != null)
            {
                return firstTagged;
            }
            return Fail(
                outcome,
                Constants.STATUS_INVALID,
                Constants.REASON_TAG_MISMATCH,
                Messages.NoValueDetailWrongTag);
        }

        /// <summary>
        /// Take one signature through the checks of the decision table,
        /// from its parameters to the check against the key the agent
        /// publishes.
        /// </summary>
        /// <param name="data">The flow data the evidence is read from.</param>
        /// <param name="source">
        /// The evidence prefix this request's signature arrived under,
        /// which every other part of the request is read from too.
        /// </param>
        /// <param name="candidate">The signature being checked.</param>
        /// <param name="agents">
        /// The 'Signature-Agent' members the request carried, or null when
        /// the header was not sent.
        /// </param>
        /// <param name="outcome">The outcome being filled in.</param>
        /// <returns>The outcome.</returns>
        private AgentSignatureOutcome EvaluateCandidate(
            IFlowData data,
            string source,
            SignatureCandidate candidate,
            IList<SignatureAgentEntry> agents,
            AgentSignatureOutcome outcome)
        {
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

            var resolver = new FlowDataComponentResolver(data, source);
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
            // The first Timeout for each directory is written at Warning,
            // so that a struggling or unreachable agent directory shows in
            // production logs the way the cloud service surfaces its
            // entitlement lookups timing out. Every occurrence after that
            // is Debug, because one line per request would drown the log
            // the moment a popular agent's directory slowed down. The set
            // of directories remembered shares the bound used for the
            // shared secret warning.
            if (Logger.IsEnabled(LogLevel.Warning) &&
                _pendingDirectoriesLogged.Count < MAXIMUM_LOGGED_KEYS &&
                _pendingDirectoriesLogged.TryAdd(
                    keyUrl ?? string.Empty, true))
            {
                Logger.LogWarning(string.Format(
                    CultureInfo.InvariantCulture,
                    Messages.LogDirectoryPending,
                    keyUrl,
                    _configuration.WaitBudget.TotalMilliseconds));
                return;
            }
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
        /// for every header rather than a fixed list. The derived
        /// components are built from those headers and from the request
        /// line, being the method, the path and the query string. Where an
        /// integration supplies no request line, a signature covering
        /// '@target-uri', '@method', '@path' or '@query' cannot be rebuilt
        /// and reads Unverified with the ComponentUnavailable reason.
        /// </remarks>
        private sealed class FlowDataComponentResolver : IComponentResolver
        {
            private readonly IFlowData _data;

            private readonly string _source;

            public FlowDataComponentResolver(IFlowData data, string source)
            {
                _data = data;
                _source = source;
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
                        GetHeader(
                            "protocol",
                            out var scheme);
                        value = SignatureBase.BuildAuthority(
                            host, scheme?.Trim().ToLowerInvariant());
                        return value != null;
                    case "@scheme":
                        if (GetHeader(
                            "protocol",
                            out var protocol) == false)
                        {
                            return false;
                        }
                        value = protocol.Trim().ToLowerInvariant();
                        return true;
                    case "@method":
                        // RFC 9421 section 2.2.1 takes the method as the
                        // request carried it. The published text is
                        // explicit that "no transformation to the input
                        // method value's case is performed", where an
                        // earlier draft had said to upper case it, so a
                        // request whose method arrived in lower case is
                        // signed and checked in lower case.
                        if (GetRequestLineValue(
                            Core.Constants.EVIDENCE_REQUEST_METHOD_KEY,
                            out var method) == false)
                        {
                            return false;
                        }
                        value = method;
                        return value.Length > 0;
                    case "@path":
                        // RFC 9421 section 2.2.6. An empty path is the
                        // single slash.
                        if (GetRequestLineValue(
                            Core.Constants.EVIDENCE_REQUEST_PATH_KEY,
                            out var path) == false)
                        {
                            return false;
                        }
                        value = path.Length == 0 ? "/" : path;
                        return true;
                    case "@query":
                        // RFC 9421 section 2.2.7. The query carries its
                        // leading question mark, and a request with no
                        // query at all still has the question mark on its
                        // own, so the two cases cannot be confused.
                        if (GetRequestLineValue(
                            Core.Constants.EVIDENCE_REQUEST_QUERY_KEY,
                            out var query) == false)
                        {
                            return false;
                        }
                        value = "?" + query;
                        return true;
                    case "@target-uri":
                        // RFC 9421 section 2.2.2, being the whole address
                        // the request was made to, which is the scheme, the
                        // authority, the path and the query joined.
                        return TryResolveTargetUri(out value);
                    default:
                        return false;
                }
            }

            /// <summary>
            /// Build the '@target-uri' component from the parts that make
            /// it up. Every part has to be there, because a target address
            /// missing its path or its query is a different address and
            /// would produce a signature base the agent never signed.
            /// </summary>
            private bool TryResolveTargetUri(out string value)
            {
                value = null;
                if (GetHeader(
                        "protocol",
                        out var scheme) == false ||
                    GetHeader("host", out var host) == false ||
                    GetRequestLineValue(
                        Core.Constants.EVIDENCE_REQUEST_PATH_KEY,
                        out var path) == false ||
                    GetRequestLineValue(
                        Core.Constants.EVIDENCE_REQUEST_QUERY_KEY,
                        out var query) == false)
                {
                    return false;
                }
                var authority = SignatureBase.BuildAuthority(
                    host, scheme.Trim().ToLowerInvariant());
                if (authority == null)
                {
                    return false;
                }
                // A request that carried no query at all and one that
                // carried an empty query, being a target ending in a bare
                // '?', both arrive here as an empty string, because the
                // evidence keys hold the query without its leading '?'
                // and there is nothing left to tell the two apart. The
                // common case by far is no query at all, and its target
                // address carries no '?', so that is what is built here.
                //
                // A request for '/search?' therefore resolves to
                // 'https://host/search' whilst '@query' resolves to '?'
                // for the same request, which is right for '@query' under
                // RFC 9421 section 2.2.7. An agent that signs over both
                // components of such a target reads as a mismatch rather
                // than as unverifiable, which is the one answer this
                // element otherwise avoids giving a genuine agent.
                //
                // Telling the two apart needs the evidence to carry the
                // '?' where the request had one, which is a change to the
                // Pipeline specification and so to every language, not
                // something to do here alone. Raised as issue #391.
                value = scheme.Trim().ToLowerInvariant() + "://" +
                    authority +
                    (path.Length == 0 ? "/" : path) +
                    (query.Length == 0 ? string.Empty : "?" + query);
                return true;
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
                // RFC 9421 section 2.1.2 puts the strict serialisation of
                // the member value on the signature base line, not the
                // text as the sender wrote it, so a member written
                // without a value goes out as '?1' and legal spacing
                // differences disappear.
                value = StructuredFieldSerializer.Serialize(member);
                return true;
            }

            /// <summary>
            /// Read a header by name from the prefix the signature came
            /// from, and from no other.
            /// </summary>
            /// <remarks>
            /// A forwarded signature describes the request that reached
            /// the caller, and the headers under the header prefix
            /// describe the call the caller then made to this server.
            /// They are two different requests, so taking one part from
            /// each would build a base no agent ever signed, and the
            /// signature would read as a mismatch, which says the agent
            /// was lying. Reading only the one source means a forwarded
            /// request whose caller sent no host reports instead that a
            /// component was unavailable, which says only that the check
            /// could not be made.
            /// </remarks>
            private bool GetHeader(string name, out string value)
            {
                return GetEvidence(
                    _source + Core.Constants.EVIDENCE_SEPERATOR + name,
                    out value);
            }

            /// <summary>
            /// Read one of the request line values from the prefix the
            /// signature came from, for the same reason as a header. A
            /// request that arrived here directly carries them under the
            /// server prefix, and a forwarded one under the query prefix
            /// with the server prefix taken off.
            /// </summary>
            private bool GetRequestLineValue(string key, out string value)
            {
                if (string.Equals(
                    _source,
                    Core.Constants.EVIDENCE_HTTPHEADER_PREFIX,
                    StringComparison.Ordinal))
                {
                    return GetEvidence(key, out value);
                }
                var name = key.Substring(key.IndexOf(
                    Core.Constants.EVIDENCE_SEPERATOR,
                    StringComparison.Ordinal) + 1);
                return GetEvidence(
                    _source + Core.Constants.EVIDENCE_SEPERATOR + name,
                    out value);
            }

            private bool GetEvidence(string key, out string value)
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
        /// name any request header, so the element cannot write the list
        /// down in advance. It accepts every header instead, together with
        /// the protocol, which '@authority' and '@scheme' are built from,
        /// and the three request line values, which '@method', '@path',
        /// '@query' and '@target-uri' are built from. A fixed list of
        /// headers would leave a signature covering any other header unable
        /// to be rebuilt, because a web integration only puts evidence into
        /// the request that some element has asked for.
        /// <para>
        /// The 'query' prefix is accepted as well as 'header', because a
        /// caller's own Pipeline sends its evidence on to the cloud service
        /// with the prefix taken off, so a header that started as
        /// 'header.signature' reaches the cloud as 'query.signature'.
        /// Accepting both is what lets one element serve a request that
        /// arrived directly and one that was forwarded.
        /// </para>
        /// <para>
        /// The class derives from the whitelist filter, holding the keys
        /// this element cannot work without, so that anything reading the
        /// whitelist can say what to send. The cloud service builds the
        /// list of evidence it accepts that way, and a prefix rule alone
        /// cannot be written down in such a list, so a signature forwarded
        /// to the cloud would carry none of its headers.
        /// </para>
        /// </remarks>
        private sealed class AgentSignatureEvidenceKeyFilter
            : EvidenceKeyFilterWhitelist
        {
            private static readonly string _headerPrefix =
                Core.Constants.EVIDENCE_HTTPHEADER_PREFIX +
                Core.Constants.EVIDENCE_SEPERATOR;

            private static readonly string _queryPrefix =
                Core.Constants.EVIDENCE_QUERY_PREFIX +
                Core.Constants.EVIDENCE_SEPERATOR;

            /// <summary>
            /// The keys carrying the request line, which '@method',
            /// '@path', '@query' and '@target-uri' are built from.
            /// </summary>
            private static readonly string[] RequestLineKeys = new[]
            {
                Core.Constants.EVIDENCE_REQUEST_METHOD_KEY,
                Core.Constants.EVIDENCE_REQUEST_PATH_KEY,
                Core.Constants.EVIDENCE_REQUEST_QUERY_KEY,
            };

            private readonly bool _trustForwarded;

            public AgentSignatureEvidenceKeyFilter(bool trustForwarded)
                : base(NamedKeys())
            {
                _trustForwarded = trustForwarded;
            }

            /// <summary>
            /// The keys this element cannot do without, named under the
            /// prefix a request carries them in when it arrives here
            /// directly, and never under the query prefix. The list is
            /// the same whether or not forwarded evidence is trusted,
            /// because what is published to callers must never depend on
            /// how this deployment reads what it receives.
            /// </summary>
            /// <remarks>
            /// Anything reading the whitelist, such as the cloud
            /// service's published list of accepted evidence, sees these
            /// and only these. That list decides what a caller's own
            /// Pipeline collects and forwards, and a caller collects a
            /// query string value only where the list names it. Naming
            /// the query forms here would therefore have every caller
            /// collect a signature typed into a visitor's address bar
            /// and forward it as though it were a header their site had
            /// received, which is how a visitor could have been reported
            /// as a verified agent. Naming only the header forms means a
            /// signature reaches the wire only where the site actually
            /// received one.
            /// </remarks>
            private static List<string> NamedKeys()
            {
                var names = new[]
                {
                    Constants.EVIDENCE_SIGNATURE_NAME,
                    Constants.EVIDENCE_SIGNATURE_INPUT_NAME,
                    Constants.EVIDENCE_SIGNATURE_AGENT_NAME,
                    "host",
                    "protocol",
                };
                var keys = new List<string>();
                foreach (var name in names)
                {
                    keys.Add(_headerPrefix + name);
                }
                foreach (var key in RequestLineKeys)
                {
                    keys.Add(key);
                }
                return keys;
            }

            /// <inheritdoc/>
            /// <remarks>
            /// Any header at all is accepted beyond the named keys,
            /// because a signature may cover one this element cannot know
            /// in advance. The protocol, which '@scheme' and the port in
            /// '@authority' are built from, is itself written as
            /// 'header.protocol', so the test on the prefix covers it too.
            /// </remarks>
            public override bool Include(string key)
            {
                if (key == null)
                {
                    return false;
                }
                return key.StartsWith(
                        _headerPrefix, StringComparison.OrdinalIgnoreCase) ||
                    (_trustForwarded &&
                        key.StartsWith(
                            _queryPrefix,
                            StringComparison.OrdinalIgnoreCase)) ||
                    base.Include(key);
            }

            /// <inheritdoc/>
            public override int? Order(string key)
            {
                return Include(key) ? 100 : (int?)null;
            }
        }
    }
}
