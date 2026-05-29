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

using FiftyOne.Did.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging;

namespace FiftyOne.Did.Cloud.Data
{
    /// <summary>
    /// Contains 51Dids related to the user.
    /// </summary>
    public class Cloud51DidData : AspectDataBase, I51DidData
    {
        /// <summary>
        ///     Designated constructor.
        /// </summary>
        /// <param name="logger">
        ///     Used for logging
        /// </param>
        /// <param name="pipeline">
        ///     The FiftyOne.Pipeline.Core.FlowElements.IPipeline instance
        ///     this element data will be associated with.
        /// </param>
        /// <param name="engine">
        ///     The FiftyOne.Pipeline.Engines.FlowElements.<see cref="IAspectEngine"/>
        ///     that created this instance
        /// </param>
        public Cloud51DidData(
            ILogger<Cloud51DidData> logger,
            IPipeline pipeline,
            IAspectEngine engine)
            : base(logger, pipeline, engine)
        {
        }

        /// <inheritdoc/>
        public IAspectPropertyValue<string> IdProbGlobal
        {
            get => GetAs<IAspectPropertyValue<string>>(nameof(IdProbGlobal));
            set => this[nameof(IdProbGlobal)] = value;
        }

        /// <inheritdoc/>
        public IAspectPropertyValue<string> IdProbLic
        {
            get => GetAs<IAspectPropertyValue<string>>(nameof(IdProbLic));
            set => this[nameof(IdProbLic)] = value;
        }
    }
}
