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
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.FiftyOne.Data;
using FiftyOne.Pipeline.Engines.FiftyOne.Trackers;
using FiftyOne.Pipeline.Engines.TestHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Tests.Trackers
{
	[TestClass]
    public class ShareUsageTrackerTests
    {
		private TimeSpan _interval;

		private IEvidenceKeyFilter _evidenceKeyFilter;

		private ShareUsageTracker _shareUsageTracker;
		private string _aspSessionCookieName;
        private List<string> _blockedHttpHeaders;
        private List<string> _includedQueryStringParameters;
        private Dictionary<string, object> _evidenceData;
		private Mock<IFlowData> _data;

		[TestInitialize]
		public void Init()
		{
			_blockedHttpHeaders = new List<string>() { };
            _includedQueryStringParameters = new List<string>();
			_aspSessionCookieName = Engines.Constants.DEFAULT_ASP_COOKIE_NAME;

			_interval = new TimeSpan(0, 0, 0, 0, 50);
			_evidenceKeyFilter = new EvidenceKeyFilterShareUsage(
				_blockedHttpHeaders, _includedQueryStringParameters, true, _aspSessionCookieName);
			_shareUsageTracker = new ShareUsageTracker(new CacheConfiguration()
				{
					Builder = new LruPutCacheBuilder(),
					Size = 1000
				},
				_interval,
				_evidenceKeyFilter);

			_evidenceData = new Dictionary<string, object>()
			{
				{ Core.Constants.EVIDENCE_HTTPHEADER_PREFIX + Core.Constants.EVIDENCE_SEPERATOR + "user-agent", "iPhone" }
			};

            var dataKeyBuilder = new DataKeyBuilder();
            foreach(var evidence in _evidenceData)
            {
                dataKeyBuilder.Add(100, evidence.Key, evidence.Value);
            }

			_data = MockFlowData.CreateFromEvidence(_evidenceData, false);
			_data.Setup(d => d.GenerateKey(It.IsAny<IEvidenceKeyFilter>())).Returns(dataKeyBuilder.Build());
		}

		/// <summary>
		/// Test that repeat evidence is not tracked when 2 requests are made 
		/// within the tracking interval or session timeout.
		/// </summary>
		[TestMethod]
		public void ShareUsageTracker_RepeatEvidence_BeforeSessionTimeout()
		{
			int trackedEvents = 0;
			for(var i = 0; i <2; i++)
			{
				if (_shareUsageTracker.Track(_data.Object))
					trackedEvents++;
			}
			Assert.IsTrue(trackedEvents == 1);
		}

		/// <summary>
		/// Check that after a tracker interval or session timeout has elapsed, 
		/// repeat evidence is treated as a new session and is tracked.
		/// </summary>
		[TestMethod]
		public void ShareUsageTracker_RepeatEvidence_AfterSessionTimeout()
		{
			int trackedEvents = 0;
			for (var i = 0; i < 2; i++)
			{
				if (_shareUsageTracker.Track(_data.Object))
					trackedEvents++;

                // Wait some time equal to the interval to elapse.
				Thread.Sleep(_interval);
			}
			Assert.IsTrue(trackedEvents == 2);
		}

		/// <summary>
		/// Test that the session ID is included in the evidence when session
		/// tracking is enabled. This means that for a request containing the 
		/// same evidence but a different session ID, both will be tracked.
		/// </summary>
		[TestMethod]
		public void ShareUsageTracker_Session_Track()
		{
			var shareUsageTracker = new ShareUsageTracker(new CacheConfiguration()
			{
				Builder = new LruPutCacheBuilder(),
				Size = 1000
			},
			_interval,
			_evidenceKeyFilter);

			int trackedEvents = 0;
			for (var i = 0; i < 2; i++)
			{
				var evidenceData = new Dictionary<string, object>()
				{
					{ Core.Constants.EVIDENCE_HTTPHEADER_PREFIX + Core.Constants.EVIDENCE_SEPERATOR + "user-agent", "iPhone" },
					{ Core.Constants.EVIDENCE_COOKIE_PREFIX + Core.Constants.EVIDENCE_SEPERATOR + Engines.Constants.DEFAULT_ASP_COOKIE_NAME, i }
				};
				var data = MockFlowData.CreateFromEvidence(_evidenceData, true);
				data.Setup(d => d.GenerateKey(It.IsAny<IEvidenceKeyFilter>())).Returns(
                    (IEvidenceKeyFilter filter) =>
                    {
                        return GenerateKey(filter, evidenceData);
                    });
                if (shareUsageTracker.Track(data.Object))
					trackedEvents++;

			}

			Assert.IsTrue(trackedEvents == 2);
		}

		/// <summary>
		/// Test that when session tracking is disabled, repeat evidence is 
		/// not tracked even if the session id is different.
		/// </summary>
		[TestMethod]
		public void ShareUsageTracker_Session_DoNotTrack()
		{
			var evidenceKeyFilter = new EvidenceKeyFilterShareUsage(
				_blockedHttpHeaders, _includedQueryStringParameters, false, _aspSessionCookieName);
			var shareUsageTracker = new ShareUsageTracker(new CacheConfiguration()
			{
				Builder = new LruPutCacheBuilder(),
				Size = 1000
			},
			_interval,
			evidenceKeyFilter);
			
			int trackedEvents = 0;
			for (var i = 0; i < 2; i++)
			{
				var evidenceData = new Dictionary<string, object>()
				{
					{ Core.Constants.EVIDENCE_HTTPHEADER_PREFIX + Core.Constants.EVIDENCE_SEPERATOR + "user-agent", "iPhone" },
                    { Core.Constants.EVIDENCE_COOKIE_PREFIX + Core.Constants.EVIDENCE_SEPERATOR + Engines.Constants.DEFAULT_ASP_COOKIE_NAME, i }
				};
				var data = MockFlowData.CreateFromEvidence(_evidenceData, true);
				data.Setup(d => d.GenerateKey(It.IsAny<IEvidenceKeyFilter>())).Returns(
                    (IEvidenceKeyFilter filter) => 
                    {
                        return GenerateKey(filter, evidenceData);
                    });
				if (shareUsageTracker.Track(data.Object))
					trackedEvents++;

			}
			Assert.IsTrue(trackedEvents == 1);
		}

		public DataKey GenerateKey(IEvidenceKeyFilter filter, Dictionary<string, object> evidence)
		{
			DataKeyBuilder result = new DataKeyBuilder();
			foreach (var entry in evidence)
			{
				if (filter.Include(entry.Key))
				{
					result.Add(100, entry.Key, entry.Value);
				}
			}
			return result.Build();
		}
	}
}
