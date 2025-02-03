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

using FiftyOne.Pipeline.Engines.Trackers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Xml;

[assembly: InternalsVisibleTo("FiftyOne.Pipeline.Engines.FiftyOne.Tests")]
namespace FiftyOne.Pipeline.Engines.FiftyOne.FlowElements
{
    /// <summary>
    /// Flow element that sends usage data to 51Degrees for analysis.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/pipeline-elements/usage-sharing-element.md">Specification</see>
    /// </summary>
    public class ShareUsageElement : ShareUsageBase
    {
        private long _successCount = 0;
        private long _failCount = 0;

        /// <inheritdoc/>
        internal ShareUsageElement(
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
            : base(logger,
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
                  shareAllEvidence)
        {
        }

        /// <summary>
        /// Create the XML file from the specified data and send it to
        /// the configured URL.
        /// </summary>
        protected override void BuildAndSendXml()
        {
            List<ShareUsageData> allData = new List<ShareUsageData>();
            ShareUsageData currentData;
            while (EvidenceCollection.TryTake(out currentData, TakeTimeout) &&
                allData.Count < MinEntriesPerMessage * 2)
            {
                allData.Add(currentData);
            }

            // Create the zip stream and XML writer.
            using (MemoryStream memory = new MemoryStream())
            {
                using (GZipStream compressedStream = new GZipStream(
                    memory, CompressionMode.Compress, true))
                using (XmlWriter writer = XmlWriter.Create(compressedStream, WriterSettings))
                {
                    writer.WriteStartElement("Devices");
                    // Write each element in turn.
                    foreach (var data in allData)
                    {
                        WriteData(writer, data);
                    }
                    writer.WriteEndElement();
                }

                memory.Position = 0;
                using (var content = new StreamContent(memory))
                {
                    content.Headers.Add("content-encoding", "gzip");
                    content.Headers.Add("content-type", "text/xml");

                    var res = HttpClient.PostAsync(ShareUsageUri, content).Result;
                    if (res.StatusCode == HttpStatusCode.OK)
                    {
                        Interlocked.Increment(ref _successCount);
                    } 
                    else
                    {
                        Interlocked.Increment(ref _failCount);
                        string response = "";
                        try
                        {
                            response = res.Content.ReadAsStringAsync().Result;
                        }
                        // Ignore any failures to read the response content, we're only logging it.
#pragma warning disable CA1031 // Do not catch general exception types
                        catch (Exception) { }
#pragma warning restore CA1031 // Do not catch general exception types

                        // If failure percentage is > 1% then log this as a warning.
                        // Otherwise, log at information level.
                        var logLevel = LogLevel.Information;
                        var percentageFailure = _failCount / (_successCount + _failCount);
                        if (percentageFailure > 0.01)
                        {
                            logLevel = LogLevel.Warning;
                        }

                        var message = string.Format(CultureInfo.CurrentCulture,
                            Messages.MessageShareUsageFailedToSend,
                            percentageFailure,
                            ShareUsageUri,
                            (int)res.StatusCode,
                            res.StatusCode,
                            response);
                        Logger.Log(logLevel, message);
                    }
                }
            }
        }
    }
}
