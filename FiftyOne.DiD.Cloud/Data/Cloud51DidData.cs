using FiftyOne.DiD.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging;

namespace FiftyOne.DiD.Cloud.Data
{
    /// <summary>
    /// Contains 51DiDs related to the user.
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
