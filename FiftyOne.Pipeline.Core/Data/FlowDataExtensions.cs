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
using System.Threading;

namespace FiftyOne.Pipeline.Core.Data
{
    /// <summary>
    /// Extension methods for <see cref="IFlowData"/>.
    /// </summary>
    public static class FlowDataExtensions
    {
        /// <summary>
        /// The token that is cancelled when processing of this flow data
        /// should stop. An element with long-running work can observe this
        /// token to stop cooperatively.
        /// </summary>
        /// <param name="data">The flow data to get the stop token for.</param>
        /// <returns>
        /// The stop token for this flow data. For a concrete
        /// <see cref="FlowData"/> this is linked to the token supplied when
        /// the flow data was created. For any other <see cref="IFlowData"/>
        /// implementation it is a token whose state reflects the current
        /// value of <see cref="IFlowData.Stop"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied data is null.
        /// </exception>
        public static CancellationToken GetStopToken(this IFlowData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (data is FlowData concrete)
            {
                return concrete.StopToken;
            }
            // Third-party IFlowData that predates the stop token: degrade to a
            // boolean snapshot of the interface-level Stop signal.
#pragma warning disable CS0618 // Type or member is obsolete
            return data.Stop
                ? new CancellationToken(canceled: true)
                : CancellationToken.None;
#pragma warning restore CS0618 // Type or member is obsolete
        }

        /// <summary>
        /// Whether processing of this flow data should continue. Returns
        /// false once the stop token has been cancelled (for example by an
        /// aborted web request or an element setting <see cref="IFlowData.Stop"/>).
        /// </summary>
        /// <param name="data">The flow data to check.</param>
        /// <returns>
        /// True while the flow data's stop token is not cancelled.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied data is null.
        /// </exception>
        public static bool ShouldRun(this IFlowData data)
        {
            return data.GetStopToken().IsCancellationRequested == false;
        }

        /// <summary>
        /// Stop processing this flow data when the supplied token is, or
        /// becomes, cancelled. Used to link an external cancellation source
        /// (for example an aborted web request) to a flow data after it has
        /// been created. Calling this again replaces any previously linked
        /// token.
        /// </summary>
        /// <param name="data">The flow data to link the token to.</param>
        /// <param name="stopToken">The token that triggers the stop.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied data is null.
        /// </exception>
        public static void SetStopToken(
            this IFlowData data,
            CancellationToken stopToken)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (data is FlowData concrete)
            {
                concrete.SetStopToken(stopToken);
                return;
            }
            // Third-party IFlowData without a linkable stop token: the only
            // stop signal available is the interface-level Stop flag, so an
            // already-cancelled token maps to it. A token that cancels later
            // cannot be observed by such an implementation.
            if (stopToken.IsCancellationRequested)
            {
#pragma warning disable CS0618 // Type or member is obsolete
                data.Stop = true;
#pragma warning restore CS0618 // Type or member is obsolete
            }
        }
    }
}
