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

using FiftyOne.Pipeline.Core.Attributes;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("FiftyOne.Pipeline.Engines.FiftyOne.Tests")]
namespace FiftyOne.Pipeline.Engines.FiftyOne.FlowElements
{
    /// <summary>
    /// Abstract base class for ShareUsageElement builders.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/pipeline-elements/usage-sharing-element.md">Specification</see>
    /// </summary>
    /// <typeparam name="T">Element type</typeparam>
    public abstract class ShareUsageBuilderBase<T>
    {
        /// <summary>
        /// The logger factory used by this builder
        /// </summary>
        protected ILoggerFactory LoggerFactory { get; private set; }

        /// <summary>
        /// The logger to be used by this builder
        /// </summary>
        protected ILogger Logger { get; private set; }

        /// <summary>
        /// Where a set of evidence values exactly matches a previously seen
        /// set of evidence values, it will not be shared if that situation 
        /// occurs within this time interval. (in minutes)
        /// </summary>
        protected int RepeatEvidenceInterval { get; private set; } = Constants.SHARE_USAGE_DEFAULT_REPEAT_EVIDENCE_INTERVAL;
        /// <summary>
        /// The approximate proportion of events to share.
        /// Specified as a floating point number from 0 to 1.
        /// </summary>
        protected double SharePercentage { get; private set; } = Constants.SHARE_USAGE_DEFAULT_SHARE_PERCENTAGE;
        /// <summary>
        /// The minimum number of entries to be present in the XML
        /// PAyload before it is sent to the usage sharing endpoint.
        /// </summary>
        protected int MinimumEntriesPerMessage { get; private set; } = Constants.SHARE_USAGE_DEFAULT_MIN_ENTRIES_PER_MESSAGE;
        /// <summary>
        /// Set the maximum number of entries to be stored in the queue to be
        /// sent. This must be more than the minimum entries per message.
        /// By default, the value is calculated automatically based on the 
        /// MinimumEntriesPerMessage setting.
        /// </summary>
        protected int MaximumQueueSize
        {
            get
            {
                int result = _maximumQueueSize;
                if(result == 0)
                {
                    result = Constants.SHARE_USAGE_DEFAULT_MAX_QUEUE_SIZE;
                    var calc = MinimumEntriesPerMessage * 10;
                    if(calc > result) { result = calc; }
                }
                return result;
            }
            private set
            {
                _maximumQueueSize = value;
            }
        }
        private int _maximumQueueSize = 0;
        /// <summary>
        /// The timeout in milliseconds to allow when attempting to add an
        /// item to the queue. If this timeout is exceeded then usage sharing
        /// will be disabled.
        /// </summary>
        protected int AddTimeout { get; private set; } = Constants.SHARE_USAGE_DEFAULT_ADD_TIMEOUT;
        /// <summary>
        /// The timeout in milliseconds to allow when attempting to take an
        /// item from the queue in order to send to the remote service.
        /// </summary>
        protected int TakeTimeout { get; private set; } = Constants.SHARE_USAGE_DEFAULT_TAKE_TIMEOUT;
        /// <summary>
        /// The remote endpoint to send usage data to. 
        /// </summary>
        [Obsolete("Use the ShareUsageUri property instead. This property may be removed in the future.")]
#pragma warning disable CA1056 // Uri properties should not be strings
        protected string ShareUsageUrl => ShareUsageUri.AbsoluteUri;
#pragma warning restore CA1056 // Uri properties should not be strings
        /// <summary>
        /// The remote endpoint to send usage data to. 
        /// </summary>
        protected Uri ShareUsageUri { get; private set; } = new Uri(Constants.SHARE_USAGE_DEFAULT_URL);
        /// <summary>
        /// The name of the cookie that contains the asp.net session id.
        /// This is used to help prevent the same usage data being shared
        /// multiple times.
        /// </summary>
        protected string AspSessionCookieName { get; private set; } = Engines.Constants.DEFAULT_ASP_COOKIE_NAME;
        /// <summary>
        /// A list of HTTP headers that should not be shared.
        /// </summary>
        protected List<string> BlockedHttpHeaders { get; private set; } = new List<string>();
        /// <summary>
        /// A list of query string parameters to be shared.
        /// </summary>
        protected List<string> IncludedQueryStringParameters { get; private set; } = new List<string>();
        /// <summary>
        /// A collection of evidence keys and values which, if present,
        /// cause the event to be ignored for the purposes of usage sharing.
        /// </summary>
        protected List<KeyValuePair<string, string>> IgnoreDataEvidenceFilter { get; private set; } = new List<KeyValuePair<string, string>>();
        /// <summary>
        /// Controls whether session tracking is enabled or disabled.
        /// If enabled, requests from a single user session will only be 
        /// shared once.
        /// </summary>
        protected bool TrackSession { get; private set; } = Constants.SHARE_USAGE_DEFAULT_TRACK_SESSION;
        /// <summary>
        /// If set to true then all evidence values will be shared.
        /// </summary>
        protected bool ShareAllEvidence { get; private set; } = Constants.SHARE_USAGE_DEFAULT_SHARE_ALL_EVIDENCE;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="loggerFactory">
        /// The <see cref="ILoggerFactory"/> to use when creating loggers for
        /// a <see cref="ShareUsageElement"/>.
        /// </param>
        public ShareUsageBuilderBase(
            ILoggerFactory loggerFactory)
        {
            LoggerFactory = loggerFactory;
            Logger = LoggerFactory.CreateLogger<ShareUsageBuilder>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="loggerFactory"></param>
        /// <param name="logger"></param>
        public ShareUsageBuilderBase(
            ILoggerFactory loggerFactory,
            ILogger logger)
        {
            LoggerFactory = loggerFactory;
            Logger = logger;
        }

        /// <summary>
        /// By default query string and HTTP form parameters are not shared 
        /// unless prefixed with '51D_'.
        /// This setting allows you to share these parameters with 51Degrees
        /// if needed.
        /// </summary>
        /// <param name="queryStringParameterNames">
        /// The (case insensitive) names of the query string parameters to include.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the parameter is null
        /// </exception>
        [DefaultValue("No sharing unless the query/form parameter starts with 51D_")]
        [CodeConfigOnly]
        public ShareUsageBuilderBase<T> SetIncludedQueryStringParameters(List<string> queryStringParameterNames)
        {
            if (queryStringParameterNames == null)
            {
                throw new ArgumentNullException(nameof(queryStringParameterNames));
            }

            foreach (var name in queryStringParameterNames)
            {
                IncludedQueryStringParameters.Add(name);
            }
            return this;
        }

        /// <summary>
        /// By default query string and HTTP form parameters are not shared 
        /// unless prefixed with '51D_'.
        /// This setting allows you to share these parameters with 51Degrees
        /// if needed.
        /// </summary>
        /// <param name="queryStringParameterNames">
        /// A comma separated list of the (case insensitive) names of 
        /// the query string parameters to include.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the parameter is null
        /// </exception>
        [DefaultValue("No sharing unless the query/form parameter starts with 51D_")]
        public ShareUsageBuilderBase<T> SetIncludedQueryStringParameters(string queryStringParameterNames)
        {
            if (queryStringParameterNames == null)
            {
                throw new ArgumentNullException(nameof(queryStringParameterNames));
            }

            return SetIncludedQueryStringParameters(
                new List<string>(queryStringParameterNames.Split(',')));
        }

        /// <summary>
        /// By default query string and HTTP form parameters are not shared 
        /// unless prefixed with '51D_'.
        /// This setting allows you to share these parameters with 51Degrees
        /// if needed.
        /// </summary>
        /// <param name="queryStringParameterName">
        /// The (case insensitive) name of the query string parameter to 
        /// include.
        /// </param>
        [DefaultValue("No sharing unless the query/form parameter starts with 51D_")]
        public ShareUsageBuilderBase<T> SetIncludedQueryStringParameter(string queryStringParameterName)
        {
            IncludedQueryStringParameters.Add(queryStringParameterName);
            return this;
        }

        /// <summary>
        /// Configure the usage sharing element to share all query string
        /// and HTTP form parameters.
        /// </summary>
        /// <param name="shareAll">
        /// If set to true then all query string parameters will be shared
        /// </param>
        /// <returns>
        /// This builder instance
        /// </returns>
        [DefaultValue(false)]
        public ShareUsageBuilderBase<T> SetShareAllQueryStringParameters(bool shareAll)
        {
            if (shareAll)
            {
                IncludedQueryStringParameters = null;
            }
            else if (IncludedQueryStringParameters == null)
            {
                IncludedQueryStringParameters = new List<string>();
            }
            return this;
        }

        /// <summary>
        /// Configure the usage sharing element to share all evidence.
        /// This will override all the other evidence filtering settings.
        /// </summary>
        /// <param name="shareAll">
        /// If set to true then all evidence will be shared
        /// </param>
        /// <returns>
        /// This builder instance
        /// </returns>
        [DefaultValue(Constants.SHARE_USAGE_DEFAULT_SHARE_ALL_EVIDENCE)]
        public ShareUsageBuilderBase<T> SetShareAllEvidence(bool shareAll)
        {
            ShareAllEvidence = shareAll;
            return this;
        }

        /// <summary>
        /// By default, all HTTP headers (excluding a few such as 'cookies')
        /// are shared. Individual headers can be excluded from sharing by 
        /// adding them to this list.
        /// </summary>
        /// <param name="blockedHeaders">
        /// The (case insensitive) names of the headers to block.
        /// </param>
        [DefaultValue("All HTTP Headers are shared except cookies that do not start with 51D_")]
        public ShareUsageBuilderBase<T> SetBlockedHttpHeaders(List<string> blockedHeaders)
        {
            BlockedHttpHeaders = blockedHeaders;
            return this;
        }

        /// <summary>
        /// By default, all HTTP headers (excluding a few such as 'cookies')
        /// are shared. Individual headers can be excluded from sharing by 
        /// adding them to this list.
        /// </summary>
        /// <param name="blockedHeader">
        /// The (case insensitive) name of the header to block.
        /// </param>
        [DefaultValue("All HTTP Headers are shared except cookies that do not start with 51D_")]
        public ShareUsageBuilderBase<T> SetBlockedHttpHeader(string blockedHeader)
        {
            BlockedHttpHeaders.Add(blockedHeader);
            return this;
        }

        /// <summary>
        /// This setting can be used to stop the usage sharing element 
        /// from sharing anything about specific requests.
        /// For example, if you wanted to stop sharing any details from 
        /// requests where the user-agent header was 'ABC', you would 
        /// set this to "header.user-agent:ABC"
        /// </summary>
        /// <param name="evidenceFilter">
        /// Comma separated string containing entries in the format 
        /// <code>[evidenceKey]:[evidenceValue]</code>.
        /// Any requests with evidence matching these entries will
        /// not be shared.
        /// </param>
        /// <returns></returns>
        [DefaultValue("All values are shared")]
        public ShareUsageBuilderBase<T> SetIgnoreFlowDataEvidenceFilter(string evidenceFilter)
        {
            if (string.IsNullOrWhiteSpace(evidenceFilter) == false)
            {
                foreach (var kvpString in evidenceFilter.Split(','))
                {
                    if (kvpString.Contains(":"))
                    {
                        KeyValuePair<string, string> kvp =
                            new KeyValuePair<string, string>(kvpString.Split(':')[0], kvpString.Split(':')[1]);
                        IgnoreDataEvidenceFilter.Add(kvp);
                    }
                    else
                    {
                        string msg = string.Format(CultureInfo.InvariantCulture,
                            Messages.MessageShareUsageInvalidConfig,
                            "IgnoreFlowDataEvidenceFilter",
                            kvpString);
                        Logger.LogWarning(msg);
                    }
                }
            }
            else
            {
                string msg = string.Format(CultureInfo.InvariantCulture,
                    Messages.MessageShareUsageInvalidConfig,
                    "IgnoreFlowDataEvidenceFilter", "");
                Logger.LogWarning(msg);
            }
            return this;
        }

        /// <summary>
        /// Set the percentage of data that the <see cref="ShareUsageElement"/>
        /// should be sharing.
        /// </summary>
        /// <param name="sharePercentage">
        /// The proportion of events sent to the pipeline that should be
        /// shared to 51Degrees.
        /// 1 = 100%, 0.5 = 50%, etc.
        /// </param>
        [DefaultValue(Constants.SHARE_USAGE_DEFAULT_SHARE_PERCENTAGE)]
        public ShareUsageBuilderBase<T> SetSharePercentage(double sharePercentage)
        {
            SharePercentage = sharePercentage;
            return this;
        }


        /// <summary>
        /// The usage element will group data into single requests before
        /// sending it.
        /// This setting controls the minimum number of entries before
        /// data is sent.
        /// If you are sharing large amounts of data, increasing this 
        /// value is recommended in order to reduce the overhead of
        /// sending HTTP messages.
        /// For example, the 51Degrees cloud service uses a value of 
        /// 2500.
        /// </summary>
        /// <param name="minimumEntriesPerMessage">
        /// The minimum number of entries to be aggregated by the
        /// <see cref="ShareUsageElement"/> before they are sent to the
        /// remote service.
        /// </param>
        [DefaultValue(Constants.SHARE_USAGE_DEFAULT_MIN_ENTRIES_PER_MESSAGE)]
        public ShareUsageBuilderBase<T> SetMinimumEntriesPerMessage(int minimumEntriesPerMessage)
        {
            MinimumEntriesPerMessage = minimumEntriesPerMessage;
            return this;
        }

        /// <summary>
        /// Set the maximum number of entries to be stored in the queue to be
        /// sent. This must be more than MinimumEntriesPerMessage.      
        /// By default, the value is calculated automatically based on the
        /// MinimumEntriesPerMessage setting.
        /// </summary>
        /// <param name="size">Size to set</param>
        [DefaultValue(Constants.SHARE_USAGE_DEFAULT_MAX_QUEUE_SIZE)]
        public ShareUsageBuilderBase<T> SetMaximumQueueSize(int size)
        {
            MaximumQueueSize = size;
            return this;
        }

        /// <summary>
        /// Set the timeout in milliseconds to allow when attempting to add an
        /// item to the queue. If this timeout is exceeded then usage sharing
        /// will be disabled.
        /// </summary>
        /// <param name="milliseconds">Timeout to set</param>
        [DefaultValue(Constants.SHARE_USAGE_DEFAULT_ADD_TIMEOUT)]
        public ShareUsageBuilderBase<T> SetAddTimeout(int milliseconds)
        {
            AddTimeout = milliseconds;
            return this;
        }

        /// <summary>
        /// Set the timeout in milliseconds to allow when attempting to take an
        /// item from the queue in order to send to the remote service.
        /// </summary>
        /// <param name="milliseconds">Timeout to set</param>
        [DefaultValue(Constants.SHARE_USAGE_DEFAULT_TAKE_TIMEOUT)]
        public ShareUsageBuilderBase<T> SetTakeTimeout(int milliseconds)
        {
            TakeTimeout = milliseconds;
            return this;
        }

        /// <summary>
        /// Set the URL to use when sharing usage data.
        /// </summary>
        /// <param name="shareUsageUrl">
        /// The URL to use when sharing usage data.
        /// </param>
        [DefaultValue(Constants.SHARE_USAGE_DEFAULT_URL)]
        public ShareUsageBuilderBase<T> SetShareUsageUrl(string shareUsageUrl)
        {
            ShareUsageUri = new Uri(shareUsageUrl);
            return this;
        }

        /// <summary>
        /// Set the URL to use when sharing usage data.
        /// </summary>
        /// <param name="shareUsageUrl">
        /// The URL to use when sharing usage data.
        /// </param>
        [DefaultValue(Constants.SHARE_USAGE_DEFAULT_URL)]
        [CodeConfigOnly]
        public ShareUsageBuilderBase<T> SetShareUsageUrl(Uri shareUsageUrl)
        {
            ShareUsageUri = shareUsageUrl;
            return this;
        }

        /// <summary>
        /// Set the name of the cookie that contains the asp.net session id.
        /// This setting has no effect if TrackSession is false.
        /// </summary>
        /// <seealso cref="SetTrackSession(bool)"/>
        /// <param name="cookieName">
        /// The name of the cookie that contains the asp.net session id.
        /// </param>
        [DefaultValue(Engines.Constants.DEFAULT_ASP_COOKIE_NAME)]
        public ShareUsageBuilderBase<T> SetAspSessionCookieName(string cookieName)
        {
            AspSessionCookieName = cookieName;
            return this;
        }

        /// <summary>
        /// If exactly the same evidence values are seen multiple times 
        /// within this time limit then they will only be shared once.
        /// </summary>
        /// <param name="interval">
        /// The interval in minutes.
        /// </param>
        [DefaultValue(Constants.SHARE_USAGE_DEFAULT_REPEAT_EVIDENCE_INTERVAL)]
        public ShareUsageBuilderBase<T> SetRepeatEvidenceIntervalMinutes(int interval)
        {
            RepeatEvidenceInterval = interval;
            return this;
        }

        /// <summary>
        /// If set to true, the configured session cookie will be used to
        /// identify user sessions.
        /// This will help to differentiate duplicate values that should
        /// not be shared.
        /// </summary>
        /// <seealso cref="SetAspSessionCookieName(string)"/>
        /// <param name="track">
        /// Boolean value sets whether the usage element should 
        /// track sessions.
        /// </param>
        /// <returns></returns>
        [DefaultValue(Constants.SHARE_USAGE_DEFAULT_TRACK_SESSION)]
        public ShareUsageBuilderBase<T> SetTrackSession(bool track)
        {
            TrackSession = track;
            return this;
        }

        /// <summary>
        /// Create the <see cref="ShareUsageElement"/>
        /// </summary>
        /// <returns>
        /// The newly created element.
        /// </returns>
        public abstract T Build();
    }
}
