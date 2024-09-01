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

using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.Trackers;
using System;

namespace FiftyOne.Pipeline.Engines.FiftyOne.Trackers
{
    /// <summary>
    /// A tracker used by share usage to attempt to avoid repeatedly 
    /// sending data relating to the same user session.
    /// </summary>
    public class ShareUsageTracker : TrackerBase<DateTime?>
    {
        /// <summary>
        /// The filter that defines which evidence keys to use in the 
        /// tracker.
        /// </summary>
        private IEvidenceKeyFilter _filter;

		/// <summary>
		/// 
		/// </summary>
		private TimeSpan _interval;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="configuration">
        /// The cache configuration to use when building the internal cache
        /// used by this tracker
        /// </param>
        /// <param name="filter">
        /// The <see cref="IEvidenceKeyFilter"/> that defines the evidence
        /// values to use when creating a key from an <see cref="IFlowData"/>.
        /// </param>
        /// <param name="interval">
        /// The tracker will return a match and update the matching entry 
        /// if it is younger than this time interval.
        /// If it is older then it will not return a match.
        /// </param>
        public ShareUsageTracker(
            CacheConfiguration configuration,
			TimeSpan interval,
            IEvidenceKeyFilter filter) 
            : base(configuration)
        {
			_interval = interval;
            _filter = filter;
        }

        /// <summary>
        /// Get the evidence filter used to create keys for this tracker. 
        /// </summary>
        /// <returns>
        /// The <see cref="IEvidenceKeyFilter"/> to use.
        /// </returns>
        protected override IEvidenceKeyFilter GetFilter()
        {
            return _filter;
        }

        /// <summary>
        /// Called when the flow data matches an existing entry in the tracker
        /// </summary>
        /// <param name="data">
        /// The flow data
        /// </param>
        /// <param name="value">
        /// The meta-data value associated with the flow data in the tracker.
        /// </param>
        /// <returns>
        /// Boolean - if tracked values age is less than the period set by 
        /// <see cref="_interval"/> then return true and update. Else if older 
        /// then consider new and return false.
        /// </returns>
        protected override bool Match(IFlowData data, DateTime? value)
        {
			var now = DateTime.Now;
			if (value <= (now - _interval))
			{
				return true;
			}
			else
				return false;
        }

        /// <summary>
        /// Called when a new instance of this tracker's meta-data is needed
        /// to add to the tracker.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> that is being added to the tracker.
        /// </param>
        /// <returns>
        /// True as the meta-data holds no extra detail for this tracker.
        /// </returns>
        protected override DateTime? NewValue(IFlowData data)
        {
            // Stored meta-data values for this tracker are always true.
            return DateTime.Now;
        }
    }
}
