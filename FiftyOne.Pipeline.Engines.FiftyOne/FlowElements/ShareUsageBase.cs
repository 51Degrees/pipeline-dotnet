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

using FiftyOne.Caching;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.FiftyOne.Data;
using FiftyOne.Pipeline.Engines.FiftyOne.Trackers;
using FiftyOne.Pipeline.Engines.Trackers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

[assembly: InternalsVisibleTo("FiftyOne.Pipeline.Engines.FiftyOne.Tests")]
namespace FiftyOne.Pipeline.Engines.FiftyOne.FlowElements
{
    /// <summary>
    /// Abstract base class for ShareUsage elements. 
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/pipeline-elements/usage-sharing-element.md">Specification</see>
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", 
        "CA1054:Uri parameters should not be strings", 
        Justification = "We do not wish to make this breaking change at this time.")]
    public abstract class ShareUsageBase : 
        FlowElementBase<IElementData, IElementPropertyMetaData>
    {
        /// <summary>
        /// The HttpClient to use when sending the data.
        /// </summary>
        private HttpClient _httpClient;

        /// <summary>
        /// The HttpClient to use when sending the data.
        /// </summary>
        protected HttpClient HttpClient => _httpClient;

        /// <summary>
        /// Inner class that is used to store details of data in memory
        /// prior to it being sent to 51Degrees.
        /// </summary>
        protected class ShareUsageData
        {
            /// <summary>
            /// The Pipeline session id for this event.
            /// </summary>
            public string SessionId { get; set; }
            /// <summary>
            /// The sequence number for this event.
            /// This is incremented as requests are made using the associated
            /// session id.
            /// </summary>
            public int Sequence { get; set; }
            /// <summary>
            /// The source IP address for this event.
            /// </summary>
            public string ClientIP { get; set; }
            /// <summary>
            /// The evidence data from this event.
            /// The dictionary key is the first part of the evidence key.
            /// The value is another dictionary containing the rest of 
            /// the evidence key and the evidence value.
            /// For example, the evidence "header.user-agent"="ABC123"
            /// would become:
            /// <code>
            /// { Key = "header", Value = { Key = "user-agent" Value = "ABC123" }}
            /// </code>
            /// </summary>
            public Dictionary<string, Dictionary<string, string>> EvidenceData { get; private set; } =
                new Dictionary<string, Dictionary<string, string>>();
        }

        /// <summary>
        /// IP Addresses of local host device.
        /// </summary>
        private static readonly IPAddress[] LOCALHOSTS = new IPAddress[]
        {
            IPAddress.Parse("127.0.0.1"),
            IPAddress.Parse("::1")
        };

        /// <summary>
        /// Queue used to store entries in memory prior to them being sent
        /// to 51Degrees.
        /// </summary>
        protected BlockingCollection<ShareUsageData> EvidenceCollection { get; }

        /// <summary>
        /// The current task sending data to the remote service.
        /// </summary>
        private volatile Task _sendDataTask = null;

        /// <summary>
        /// Lock to use when starting a new send data task.
        /// </summary>
        private volatile object _lock = new object();

        /// <summary>
        /// Timeout to use when adding to the queue.
        /// </summary>
        private int _addTimeout;

        /// <summary>
        /// Timeout to use when taking from the queue.
        /// </summary>
        protected int TakeTimeout { get; }

        private Random _rng = new Random();

        /// <summary>
        /// The tracker to use to determine if a <see cref="FlowData"/>
        /// instance should be shared or not.
        /// </summary>
        private ITracker _tracker;

        /// <summary>
        /// The minimum number of request entries per message sent to 51Degrees.
        /// </summary>
        protected int MinEntriesPerMessage { get; } = Constants.SHARE_USAGE_DEFAULT_MIN_ENTRIES_PER_MESSAGE;

        /// <summary>
        /// The interval is a timespan which is used to determine if a piece
        /// of repeated evidence should be considered new evidence to share.
        /// If the evidence from a request matches that in the tracker but this
        /// interval has elapsed then the tracker will track it as new evidence.
        /// </summary>
        private TimeSpan _interval = new TimeSpan(0, Constants.SHARE_USAGE_DEFAULT_REPEAT_EVIDENCE_INTERVAL, 0);

        /// <summary>
        /// The approximate proportion of requests to be shared.
        /// 1 = 100%, 0.5 = 50%, etc.
        /// </summary>
        private double _sharePercentage = Constants.SHARE_USAGE_DEFAULT_SHARE_PERCENTAGE;

        // Used to store the part of the xml message that will not change.
        private string _staticXml = null;
        private object _staticXmlLock = new object();


        private List<string> _flowElements = null;

        /// <summary>
        /// Return a list of <see cref="IFlowElement"/> in the pipeline. 
        /// If the list is null then populate from the pipeline.
        /// If there are multiple or no pipelines then log a warning.
        /// </summary>
        private List<string> FlowElements
        {
            get
            {
                if (_flowElements == null)
                {
                    IPipeline pipeline = null;
                    if (Pipelines.Count == 1)
                    {
                        pipeline = Pipelines.Single();
                        _flowElements = new List<string>(pipeline.FlowElements
                            .Select(e => e.GetType().FullName));
                    }
                    else
                    {
                        // This element has somehow been registered to too 
                        // many (or zero) pipelines.
                        // This means we cannot know the flow elements that
                        // make up the pipeline so a warning is logged
                        // but otherwise, the system can continue as normal.
                        Logger.LogWarning(Pipelines.Count == 0 ? 
                            Messages.MessageShareUsageNoPipelines :
                            string.Format(CultureInfo.InvariantCulture,
                                Messages.MessageShareUsageTooManyPipelines,
                                Pipelines.Count));
                        _flowElements = new List<string>();
                    }
                }
                return _flowElements;
            }
        }

        private string _osVersion = "";
        private string _languageVersion = "";
        private string _coreVersion = "";
        private string _enginesVersion = "";

        /// <summary>
        /// The base URL to send usage data to.
        /// </summary>
        [Obsolete("Use the ShareUsageUri property instead. " +
            "This property could be removed in a future version.")]
#pragma warning disable CA1056 // Uri properties should not be strings
        protected string ShareUsageUrl => ShareUsageUri.AbsoluteUri;
#pragma warning restore CA1056 // Uri properties should not be strings

        /// <summary>
        /// The base URL to send usage data to.
        /// </summary>
        protected Uri ShareUsageUri { get; }

        /// <summary>
        /// The settings to use when creating an XML payload to send
        /// to the usage sharing web service.
        /// </summary>
        protected XmlWriterSettings WriterSettings { get; } = new XmlWriterSettings()
        {
            ConformanceLevel = ConformanceLevel.Document,
            Encoding = Encoding.UTF8,
            CheckCharacters = true,
            NewLineHandling = NewLineHandling.None,
            CloseOutput = true,
        };

        /// <summary>
        /// The default element data key that will be used for this element. 
        /// </summary>
#pragma warning disable CA1707 // Identifiers should not contain underscores
        public const string DEFAULT_ELEMENT_DATA_KEY = "shareusage";
#pragma warning restore CA1707 // Identifiers should not contain underscores

        /// <summary>
        /// The data key for this element
        /// </summary>
        public override string ElementDataKey => DEFAULT_ELEMENT_DATA_KEY;

        private IEvidenceKeyFilter _evidenceKeyFilter;

        /// <summary>
        /// Get the evidence key filter for this element.
        /// </summary>
        public override IEvidenceKeyFilter EvidenceKeyFilter
        {
            get { return _evidenceKeyFilter; }
        }

        private IEvidenceKeyFilter _evidenceKeyFilterExclSession;

        private List<KeyValuePair<string, string>> _ignoreDataEvidenceFilter;

        private string _hostAddress;
        private IList<IElementPropertyMetaData> _properties;

        /// <summary>
        /// Get the IP address of the machine that this code is running on.
        /// </summary>
        protected string HostAddress
        {
            get
            {
                if (_hostAddress == null)
                {
                    IPAddress[] addresses = Dns.GetHostAddresses(Dns.GetHostName());

                    var address = addresses.FirstOrDefault(a => !IsLocalHost(a) &&
                        a.AddressFamily == AddressFamily.InterNetwork);
                    if (address == null)
                    {
                        address = addresses.FirstOrDefault(a => !IsLocalHost(a));
                    }

                    _hostAddress = address == null ? "" : address.ToString();
                }
                return _hostAddress;
            }
        }

        /// <summary>
        /// Get a list of the meta-data relating to the properties 
        /// that this flow element will populate.
        /// For this share usage element, the list will always be empty
        /// as it does not populate any properties.
        /// </summary>
        public override IList<IElementPropertyMetaData> Properties
        {
            get { return _properties; }
        }

        /// <summary>
        /// Indicates whether share usage has been canceled as a result of an
        /// error.
        /// </summary>
        protected internal bool IsCanceled { get; set; } = false;

        /// <summary>
        /// True if there is a task running to send usage data to the remote
        /// service.
        /// </summary>
        internal bool IsRunning
        {
            get => SendDataTask != null &&
                SendDataTask.IsCompleted == false &&
                SendDataTask.IsFaulted == false;
        }

        /// <summary>
        /// Currently running task which is sending the usage data to the
        /// remote service.
        /// </summary>
        internal Task SendDataTask
        {
            get => _sendDataTask;
            private set => _sendDataTask = value;
        }


        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">
        /// The logger to use.
        /// </param>
        /// <param name="httpClient">
        /// The <see cref="HttpClient"/> to use when sending request data.
        /// </param>
        /// <param name="sharePercentage">
        /// The approximate proportion of requests to share. 
        /// 1 = 100%, 0.5 = 50%, etc.
        /// </param>
        /// <param name="minimumEntriesPerMessage">
        /// The minimum number of request entries per message sent to 51Degrees.
        /// </param>
        /// <param name="maximumQueueSize">
        /// The maximum number of items to hold in the queue at one time. This
        /// must be larger than minimum entries.
        /// </param>
        /// <param name="addTimeout">
        /// The timeout in milliseconds to allow when attempting to add an
        /// item to the queue. If this timeout is exceeded then usage sharing
        /// will be disabled.
        /// </param>
        /// <param name="takeTimeout">
        /// The timeout in milliseconds to allow when attempting to take an
        /// item to the queue.
        /// </param>
        /// <param name="repeatEvidenceIntervalMinutes">
        /// The interval (in minutes) which is used to decide if repeat 
        /// evidence is old enough to consider a new session.
        /// </param>
        /// <param name="trackSession">
        /// Set if the tracker should consider sessions in share usage.
        /// </param>
        /// <param name="shareUsageUrl">
        /// The URL to send data to
        /// </param>
        /// <param name="blockedHttpHeaders">
        /// A list of the names of the HTTP headers that share usage should
        /// not send to 51Degrees.
        /// </param>
        /// <param name="includedQueryStringParameters">
        /// A list of the names of query string parameters that share 
        /// usage should send to 51Degrees.
        /// If this value is null, all query string parameters are shared.
        /// </param>
        /// <param name="ignoreDataEvidenceFilter"></param>
        /// <param name="aspSessionCookieName">
        /// The name of the cookie that contains the asp.net session id.
        /// </param>
        protected ShareUsageBase(
            ILogger<ShareUsageBase> logger,
            HttpClient httpClient,
            double sharePercentage,
            int minimumEntriesPerMessage,
            int maximumQueueSize,
            int addTimeout,
            int takeTimeout,
            int repeatEvidenceIntervalMinutes,
            bool trackSession,
            string shareUsageUrl,
            List<string> blockedHttpHeaders,
            List<string> includedQueryStringParameters,
            List<KeyValuePair<string, string>> ignoreDataEvidenceFilter,
            string aspSessionCookieName = Engines.Constants.DEFAULT_ASP_COOKIE_NAME)
            : this(logger,
                  httpClient,
                  sharePercentage,
                  minimumEntriesPerMessage,
                  maximumQueueSize,
                  addTimeout,
                  takeTimeout,
                  repeatEvidenceIntervalMinutes,
                  trackSession,
                  shareUsageUrl,
                  blockedHttpHeaders,
                  includedQueryStringParameters,
                  ignoreDataEvidenceFilter,
                  aspSessionCookieName,
                  null,
                  false)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">
        /// The logger to use.
        /// </param>
        /// <param name="httpClient">
        /// The <see cref="HttpClient"/> to use when sending request data.
        /// </param>
        /// <param name="sharePercentage">
        /// The approximate proportion of requests to share. 
        /// 1 = 100%, 0.5 = 50%, etc.
        /// </param>
        /// <param name="minimumEntriesPerMessage">
        /// The minimum number of request entries per message sent to 51Degrees.
        /// </param>
        /// <param name="maximumQueueSize">
        /// The maximum number of items to hold in the queue at one time. This
        /// must be larger than minimum entries.
        /// </param>
        /// <param name="addTimeout">
        /// The timeout in milliseconds to allow when attempting to add an
        /// item to the queue. If this timeout is exceeded then usage sharing
        /// will be disabled.
        /// </param>
        /// <param name="takeTimeout">
        /// The timeout in milliseconds to allow when attempting to take an
        /// item to the queue.
        /// </param>
        /// <param name="repeatEvidenceIntervalMinutes">
        /// The interval (in minutes) which is used to decide if repeat 
        /// evidence is old enough to consider a new session.
        /// </param>
        /// <param name="trackSession">
        /// Set if the tracker should consider sessions in share usage.
        /// </param>
        /// <param name="shareUsageUrl">
        /// The URL to send data to
        /// </param>
        /// <param name="blockedHttpHeaders">
        /// A list of the names of the HTTP headers that share usage should
        /// not send to 51Degrees.
        /// </param>
        /// <param name="includedQueryStringParameters">
        /// A list of the names of query string parameters that share 
        /// usage should send to 51Degrees.
        /// If this value is null, all query string parameters are shared.
        /// </param>
        /// <param name="ignoreDataEvidenceFilter"></param>
        /// <param name="aspSessionCookieName">
        /// The name of the cookie that contains the asp.net session id.
        /// </param>
        /// <param name="tracker">
        /// The <see cref="ITracker"/> to use to determine if a given 
        /// <see cref="IFlowData"/> instance should be shared or not.
        /// </param>
        protected ShareUsageBase(
            ILogger<ShareUsageBase> logger,
            HttpClient httpClient,
            double sharePercentage,
            int minimumEntriesPerMessage,
            int maximumQueueSize,
            int addTimeout,
            int takeTimeout,
            int repeatEvidenceIntervalMinutes,
            bool trackSession,
            string shareUsageUrl,
            List<string> blockedHttpHeaders,
            List<string> includedQueryStringParameters,
            List<KeyValuePair<string, string>> ignoreDataEvidenceFilter,
            string aspSessionCookieName,
            ITracker tracker)
            : this(logger,
                  httpClient,
                  sharePercentage,
                  minimumEntriesPerMessage,
                  maximumQueueSize,
                  addTimeout,
                  takeTimeout,
                  repeatEvidenceIntervalMinutes,
                  trackSession,
                  shareUsageUrl,
                  blockedHttpHeaders,
                  includedQueryStringParameters,
                  ignoreDataEvidenceFilter,
                  aspSessionCookieName,
                  tracker,
                  false)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">
        /// The logger to use.
        /// </param>
        /// <param name="httpClient">
        /// <see cref="HttpClient"/> to use when sending request data.
        /// </param>
        /// <param name="sharePercentage">
        /// The approximate proportion of requests to share. 
        /// 1 = 100%, 0.5 = 50%, etc.
        /// </param>
        /// <param name="minimumEntriesPerMessage">
        /// The minimum number of request entries per message sent to 51Degrees.
        /// </param>
        /// <param name="maximumQueueSize">
        /// The maximum number of items to hold in the queue at one time. This
        /// must be larger than minimum entries.
        /// </param>
        /// <param name="addTimeout">
        /// The timeout in milliseconds to allow when attempting to add an
        /// item to the queue. If this timeout is exceeded then usage sharing
        /// will be disabled.
        /// </param>
        /// <param name="takeTimeout">
        /// The timeout in milliseconds to allow when attempting to take an
        /// item to the queue.
        /// </param>
        /// <param name="repeatEvidenceIntervalMinutes">
        /// The interval (in minutes) which is used to decide if repeat 
        /// evidence is old enough to consider a new session.
        /// </param>
        /// <param name="trackSession">
        /// Set if the tracker should consider sessions in share usage.
        /// </param>
        /// <param name="shareUsageUrl">
        /// The URL to send data to
        /// </param>
        /// <param name="blockedHttpHeaders">
        /// A list of the names of the HTTP headers that share usage should
        /// not send to 51Degrees.
        /// </param>
        /// <param name="includedQueryStringParameters">
        /// A list of the names of query string parameters that share 
        /// usage should send to 51Degrees.
        /// If this value is null, all query string parameters are shared.
        /// </param>
        /// <param name="ignoreDataEvidenceFilter"></param>
        /// <param name="aspSessionCookieName">
        /// The name of the cookie that contains the asp.net session id.
        /// </param>
        /// <param name="tracker">
        /// The <see cref="ITracker"/> to use to determine if a given 
        /// <see cref="IFlowData"/> instance should be shared or not.
        /// </param>
        /// <param name="shareAllEvidence">
        /// If true, all evidence will be shared and  
        /// the blockedHttpHeaders, includedQueryStringParameters and
        /// ignoreDataEvidenceFilter parameters will be ignored.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if certain arguments are null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if certain argument values are invalid.
        /// </exception>
        protected ShareUsageBase(
            ILogger<ShareUsageBase> logger,
            HttpClient httpClient,
            double sharePercentage,
            int minimumEntriesPerMessage,
            int maximumQueueSize,
            int addTimeout,
            int takeTimeout,
            int repeatEvidenceIntervalMinutes,
            bool trackSession,
            string shareUsageUrl,
            List<string> blockedHttpHeaders,
            List<string> includedQueryStringParameters,
            List<KeyValuePair<string, string>> ignoreDataEvidenceFilter,
            string aspSessionCookieName,
            ITracker tracker,
            bool shareAllEvidence)
            : base(logger)
        {
            if (blockedHttpHeaders == null)
            {
                throw new ArgumentNullException(nameof(blockedHttpHeaders));
            }
            if (minimumEntriesPerMessage > maximumQueueSize)
            {
                throw new ArgumentException(Messages.ExceptionShareUsageMinimumEntriesTooLarge);
            }

            // Make sure the cookie headers are ignored.
            if (!blockedHttpHeaders.Contains(Constants.EVIDENCE_HTTPHEADER_COOKIE_SUFFIX))
            {
                blockedHttpHeaders.Add(Constants.EVIDENCE_HTTPHEADER_COOKIE_SUFFIX);
            }

            _httpClient = httpClient;

            EvidenceCollection = new BlockingCollection<ShareUsageData>(maximumQueueSize);

            _addTimeout = addTimeout;
            TakeTimeout = takeTimeout;
            _sharePercentage = sharePercentage;
            MinEntriesPerMessage = minimumEntriesPerMessage;
            _interval = TimeSpan.FromMinutes(repeatEvidenceIntervalMinutes);
            ShareUsageUri = new Uri(shareUsageUrl);

            // Some data is going to stay the same on all requests so we can 
            // gather that now.
            _languageVersion = Environment.Version.ToString();
            _osVersion = Environment.OSVersion.VersionString;

            _enginesVersion = GetType().Assembly
                .GetCustomAttribute<AssemblyFileVersionAttribute>().Version;
            _coreVersion = typeof(IPipeline).Assembly
                .GetCustomAttribute<AssemblyFileVersionAttribute>().Version;

            IEvidenceKeyFilter trackerEvidenceFiler = null;

            if (shareAllEvidence == false)
            {
                // Create evidence filters for the configured evidence
                // sharing settings.
                if (includedQueryStringParameters != null)
                {
                    includedQueryStringParameters.Add(Constants.EVIDENCE_SESSIONID_SUFFIX);
                    includedQueryStringParameters.Add(Constants.EVIDENCE_SEQUENCE_SUFIX);
                }

                _evidenceKeyFilter = new EvidenceKeyFilterShareUsage(
                    blockedHttpHeaders, includedQueryStringParameters, true, aspSessionCookieName);
                _evidenceKeyFilterExclSession = new EvidenceKeyFilterShareUsage(
                    blockedHttpHeaders, includedQueryStringParameters, false, aspSessionCookieName);

                _ignoreDataEvidenceFilter = ignoreDataEvidenceFilter;

                 trackerEvidenceFiler = new EvidenceKeyFilterShareUsageTracker(
                     blockedHttpHeaders, 
                     includedQueryStringParameters, 
                     trackSession, 
                     aspSessionCookieName);
            }
            else
            {
                // Create evidence filters what will allow all 
                // evidence to be shared
                _evidenceKeyFilter = new EvidenceKeyFilterShareUsage();
                _evidenceKeyFilterExclSession = new EvidenceKeyFilterShareUsage();
                trackerEvidenceFiler = new EvidenceKeyFilterShareUsageTracker();
            }

            _tracker = tracker;
            // If no tracker was supplied then create the default one.
            if (_tracker == null)
            {
                _tracker = new ShareUsageTracker(new CacheConfiguration()
                {
                    Builder = new LruPutCacheBuilder(),
                    Size = 1000
                },
                _interval,
               trackerEvidenceFiler);
            }

            _properties = new List<IElementPropertyMetaData>();
        }

        /// <summary>
        /// Add 
        /// </summary>
        /// <param name="pipeline"></param>
        public override void AddPipeline(IPipeline pipeline)
        {
            if (Pipelines.Count > 0)
            {
                throw new PipelineException(Messages.ExceptionShareUsageSinglePipeline);
            }
            base.AddPipeline(pipeline);
        }

        /// <summary>
        /// Process the data
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> instance that provides the evidence
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied data instance is null
        /// </exception>
        protected override void ProcessInternal(IFlowData data)
        {
            if(data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            bool ignoreData = false;
            var evidence = data.GetEvidence().AsDictionary();

            if (_ignoreDataEvidenceFilter != null)
            {
                foreach (var kvp in _ignoreDataEvidenceFilter)
                {
                    if (evidence.ContainsKey(kvp.Key))
                    {
                        if (evidence[kvp.Key].ToString() == kvp.Value)
                        {
                            ignoreData = true;
                            break;
                        }
                    }
                }
            }

            if (IsCanceled == false && ignoreData == false)
            {
                ProcessData(data);
            }
        }

        /// <summary>
        /// Send any data which has built up locally and not yet been sent to
        /// the remote service.
        /// </summary>
        protected override void ManagedResourcesCleanup()
        {
            TrySendData();
            if (IsRunning)
            {
                SendDataTask.Wait();
            }
        }

        /// <summary>
        /// Clean up any unmanaged resources.
        /// </summary>
        protected override void UnmanagedResourcesCleanup()
        {
        }

        /// <summary>
        /// Returns true if the request is from the local host IP address.
        /// </summary>
        /// <param name="address">
        /// The IP address to be checked.
        /// </param>
        /// <returns>
        /// True if from the local host IP address.
        /// </returns>
        private static bool IsLocalHost(IPAddress address)
        {
            return LOCALHOSTS.Any(host => host.Equals(address));
        }

        /// <summary>
        /// Process the supplied request data
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> instance that provides the evidence
        /// </param>
        private void ProcessData(IFlowData data)
        {
            if (_rng.NextDouble() <= _sharePercentage)
            {
                // Check if the tracker will allow sharing of this data
                if (_tracker.Track(data))
                {
                    // Extract the data we want from the evidence and add
                    // it to the collection.
                    if (EvidenceCollection.TryAdd(
                        GetDataFromEvidence(data.GetEvidence()),
                        _addTimeout) == true)
                    {
                        // If the collection has enough entries then start
                        // taking data from it to be sent.
                        if (EvidenceCollection.Count >= MinEntriesPerMessage)
                        {
                            TrySendData();
                        }
                    }
                    else
                    {
                        IsCanceled = true;
                        Logger.LogError(Messages.MessageShareUsageFailedToAddData);

                    }
                }
            }
        }

        /// <summary>
        /// Extract the desired data from the evidence.
        /// In order to avoid problems with the evidence data being disposed 
        /// before it is sent, the data placed into a new object rather 
        /// than being a reference to the existing evidence instance.
        /// </summary>
        /// <param name="evidence">
        /// An <see cref="IEvidence"/> instance that contains the data to be
        /// extracted.
        /// </param>
        /// <returns>
        /// A <see cref="ShareUsageData"/> instance populated with data from
        /// the evidence.
        /// </returns>
        private ShareUsageData GetDataFromEvidence(IEvidence evidence)
        {
            ShareUsageData data = new ShareUsageData();

            foreach (var entry in evidence.AsDictionary())
            {
                if (entry.Key.Equals(Core.Constants.EVIDENCE_CLIENTIP_KEY, 
                    StringComparison.OrdinalIgnoreCase))
                {
                    // The client IP is dealt with separately for backwards
                    // compatibility purposes.
                    data.ClientIP = entry.Value.ToString();
                }
                else if (entry.Key.Equals(Constants.EVIDENCE_SESSIONID,
                    StringComparison.OrdinalIgnoreCase))
                {
                    // The SessionID is dealt with separately.
                    data.SessionId = entry.Value.ToString();
                }
                else if (entry.Key.Equals(Constants.EVIDENCE_SEQUENCE,
                    StringComparison.OrdinalIgnoreCase))
                {
                    // The Sequence is dealt with separately.
                    var sequence = 0;
                    if (int.TryParse(entry.Value.ToString(), out sequence))
                    {
                        data.Sequence = sequence;
                    }
                }
                else
                {
                    // Check if we can send this piece of evidence
                    bool addToData = _evidenceKeyFilterExclSession.Include(entry.Key);

                    if (addToData)
                    {
                        // Get the category and field names from the evidence key.
                        string category = "";
                        string field = entry.Key;

                        int firstSeperator = entry.Key.IndexOf(
                            Core.Constants.EVIDENCE_SEPERATOR,
                            StringComparison.OrdinalIgnoreCase);
                        if (firstSeperator > 0)
                        {
                            category = entry.Key.Remove(firstSeperator);
                            field = entry.Key.Substring(firstSeperator + 1);
                        }

                        // Get the evidence value.
                        string evidenceValue = entry.Value.ToString();
                        // If the value is longer than the permitted length 
                        // then truncate it.
                        if (evidenceValue.Length > Constants.SHARE_USAGE_MAX_EVIDENCE_LENGTH)
                        {
                            evidenceValue = "[TRUNCATED BY USAGE SHARING] " +
                                evidenceValue.Remove(Constants.SHARE_USAGE_MAX_EVIDENCE_LENGTH);
                        }

                        // Add the evidence to the dictionary.
                        Dictionary<string, string> categoryDict;
                        if (data.EvidenceData.TryGetValue(category, out categoryDict) == false)
                        {
                            categoryDict = new Dictionary<string, string>();
                            data.EvidenceData.Add(category, categoryDict);
                        }
                        categoryDict.Add(field, evidenceValue);
                    }
                }
            }

            return data;
        }

        /// <summary>
        /// Attempt to send the data to the remote service. This only happens
        /// if there is not a task already running.
        /// 
        /// If any error occurs while sending the data, then usage sharing is
        /// stopped.
        /// </summary>
        /// <returns></returns>
        protected void TrySendData()
        {
            if (IsCanceled == false &&
                IsRunning == false)
            {
                lock (_lock)
                {
                    if (IsRunning == false)
                    {
                        SendDataTask = Task.Run(() =>
                        {
                            BuildAndSendXml();
                        }).ContinueWith(t =>
                        {
                            if(t.Exception != null)
                            {
                                Logger.LogError(
                                    t.Exception,
                                    Messages.MessageShareUsageUnexpectedFailure);
                            }
                        }, TaskScheduler.Default);
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        protected abstract void BuildAndSendXml();

        /// <summary>
        /// Virtual method to be overridden in extending usage share elements.
        /// Write the specified data using the specified writer.
        /// </summary>
        /// <param name="writer">
        /// The <see cref="XmlWriter"/> to use.
        /// </param>
        /// <param name="data">
        /// The <see cref="ShareUsageData"/> to write.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied arguments are null
        /// </exception>
        protected virtual void WriteData(XmlWriter writer, ShareUsageData data)
        {
            if(writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.WriteStartElement("Device");

            WriteDeviceData(writer, data);

            writer.WriteEndElement();
        }

        /// <summary>
        /// Write the specified device data using the specified writer.
        /// </summary>
        /// <param name="writer">
        /// The <see cref="XmlWriter"/> to use.
        /// </param>
        /// <param name="data">
        /// The <see cref="ShareUsageData"/> to write.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied arguments are null
        /// </exception>
        protected void WriteDeviceData(XmlWriter writer, ShareUsageData data)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            // Used to record whether the evidence within a flow data contains invalid XML
            // characters such as control characters.
            var flagBadSchema = false;

            // The SessionID used to track a series of requests
            writer.WriteElementString("SessionId", data.SessionId);
            // The sequence number of the request in a series of requests.
            writer.WriteElementString("Sequence", data.Sequence.ToString(
                CultureInfo.InvariantCulture));
            // The UTC date/time this entry was written
            writer.WriteElementString("DateSent", DateTime.UtcNow.ToString(
                "yyyy-MM-ddTHH:mm:ss", 
                CultureInfo.InvariantCulture));
            // The client IP of the request
            writer.WriteElementString("ClientIP", data.ClientIP);
            writer.WriteRaw(GetStaticXml());

            // Write all other evidence data that has been included.
            foreach (var category in data.EvidenceData)
            {
                foreach (var entry in category.Value)
                {
                    if (category.Key.Length > 0)
                    {
                        writer.WriteStartElement(category.Key);
                        writer.WriteAttributeString("Name", EncodeInvalidXMLChars(entry.Key, ref flagBadSchema));
                        writer.WriteCData(EncodeInvalidXMLChars(entry.Value, ref flagBadSchema));
                        writer.WriteEndElement();
                    }
                    else
                    {
                        writer.WriteElementString(EncodeInvalidXMLChars(entry.Key, ref flagBadSchema),
                            EncodeInvalidXMLChars(entry.Value, ref flagBadSchema));
                    }
                }
            }
            // If any written value contained invalid characters, add the 'BadSchema' element.
            if (flagBadSchema)
            {
                writer.WriteElementString("BadSchema", "true");
            }
        }

        /// <summary>
        /// Get the part of the xml message that will be the same for every usage 
        /// sharing event on this machine.
        /// This is written once to a string in memory and then re-used for
        /// future messages.
        /// </summary>
        /// <returns></returns>
        private string GetStaticXml()
        {
            if (_staticXml == null)
            {
                lock (_staticXmlLock)
                {
                    if (_staticXml == null)
                    {
                        var result = new StringBuilder();
                        var settings = new XmlWriterSettings()
                        {
                            OmitXmlDeclaration = true,
                            WriteEndDocumentOnClose = false,
                            ConformanceLevel = ConformanceLevel.Fragment
                        };
                        using (var writer = XmlWriter.Create(result, settings))
                        {
                            // The version number of the Pipeline API
                            writer.WriteElementString("Version", _coreVersion);
                            // Write Pipeline information
                            WritePipelineInfo(writer);
                            // The software language
                            writer.WriteElementString("Language", "dotnet");
                            // The software language version
                            writer.WriteElementString("LanguageVersion", _languageVersion);
                            // The IP of this server
                            writer.WriteElementString("ServerIP", HostAddress);
                            // The OS name and version
                            writer.WriteElementString("Platform", _osVersion);
                        }

                        _staticXml = result.ToString();
                    }

                    // If it's still null for some reason, set it to the empty string 
                    // so that we don't keep hitting the lock every time evidence is 
                    // processed.
                    if (_staticXml == null) { _staticXml = string.Empty; }
                }
            }
            return _staticXml;
        }

        /// <summary>
        /// Virtual method to write details about the pipeline.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied writer is null
        /// </exception>
        protected virtual void WritePipelineInfo(XmlWriter writer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            // The product name
            writer.WriteElementString("Product", "Pipeline");
            // The flow elements in the current pipeline
            foreach (var flowElement in FlowElements)
            {
                writer.WriteElementString("FlowElement", flowElement);
            }
        }

        /// <summary>
        /// encodes any unusual characters into their hex representation
        /// </summary>
        /// <param name="text">
        /// The text to encode
        /// </param>
        /// <param name="flagBadSchema">
        /// A flag storing whether this usage message inculdes any invalid characters 
        /// that we have had to encod.
        /// </param>
        /// <returns>
        /// The encoded version of <paramref name="text"/>
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied text is null
        /// </exception>
        public string EncodeInvalidXMLChars(string text, ref bool flagBadSchema)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            // Validate characters in string. If not valid check chars 
            // individually and build new string with encoded chars. Set _flag 
            // to add "bad schema" element into usage data.

            try
            {
                return XmlConvert.VerifyXmlChars(text);
            }
            catch (XmlException)
            {
                flagBadSchema = true;
                var tmp = new StringBuilder();
                foreach (var c in text)
                {
                    if (XmlConvert.IsXmlChar(c))
                    {
                        tmp.Append(c);
                    }
                    else
                    {
                        tmp.Append("\\x" + Convert.ToByte(c).ToString("x4", 
                            CultureInfo.InvariantCulture));
                    }
                };

                return tmp.ToString();
            }
        }
    }
}
