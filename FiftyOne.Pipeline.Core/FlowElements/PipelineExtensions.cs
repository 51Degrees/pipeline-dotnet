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

using FiftyOne.Pipeline.Core.Data;
using System;
using System.Threading;

namespace FiftyOne.Pipeline.Core.FlowElements
{
    /// <summary>
    /// Extension methods for <see cref="IPipeline"/>.
    /// </summary>
    public static class PipelineExtensions
    {
        /// <summary>
        /// Create a new flow data that stops processing when the supplied
        /// token is cancelled.
        /// </summary>
        /// <param name="pipeline">
        /// The pipeline to create the flow data from.
        /// </param>
        /// <param name="cancellationToken">
        /// Token that cancels processing of the created flow data.
        /// </param>
        /// <returns>
        /// A new <see cref="IFlowData"/> instance.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied pipeline is null.
        /// </exception>
        public static IFlowData CreateFlowData(
            this IPipeline pipeline,
            CancellationToken cancellationToken)
        {
            if (pipeline == null)
            {
                throw new ArgumentNullException(nameof(pipeline));
            }
            // The token can only be wired in through the internal factory,
            // which every in-box pipeline supports. A third-party IPipeline
            // that predates this feature falls back to the token-less path.
            if (pipeline is IPipelineInternal internalPipeline)
            {
                return internalPipeline.CreateFlowData(cancellationToken);
            }
            return pipeline.CreateFlowData();
        }
    }
}
