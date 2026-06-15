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

using System.Threading;

namespace FiftyOne.Pipeline.Core.Data
{
    /// <summary>
    /// Helpers for cancelling and querying the stop state of a flow data
    /// through its <see cref="IFlowData.StopTokenSource"/>.
    /// </summary>
    public static class FlowDataStopTokenExtensions
    {
        /// <summary>
        /// Stop this flow data when the supplied token is, or becomes,
        /// cancelled. Does nothing when the data has no stop token source.
        /// </summary>
        /// <param name="data">The flow data to stop.</param>
        /// <param name="stopToken">The token that triggers the stop.</param>
        public static void SetStopToken(
            this IFlowData data, CancellationToken stopToken)
        {
            var source = data.StopTokenSource;
            if (source is null)
            {
                return;
            }
            if (stopToken.IsCancellationRequested)
            {
                source.Cancel();
                return;
            }
            stopToken.Register(o => ((CancellationTokenSource)o).Cancel(), source);
        }

        /// <summary>
        /// The stop token for this flow data, or
        /// <see cref="CancellationToken.None"/> when none is set.
        /// </summary>
        /// <param name="data">The flow data to read the token from.</param>
        public static CancellationToken GetStopToken(this IFlowData data)
            => data.StopTokenSource?.Token ?? CancellationToken.None;

        /// <summary>
        /// False once the flow data has been told to stop. Elements can
        /// check this to skip work after cancellation.
        /// </summary>
        /// <param name="data">The flow data to check.</param>
        public static bool ShouldRun(this IFlowData data)
            => data.GetStopToken().IsCancellationRequested == false;
    }
}
