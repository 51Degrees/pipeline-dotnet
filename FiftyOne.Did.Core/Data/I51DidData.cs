using FiftyOne.Pipeline.Engines.Data;

namespace FiftyOne.Did.Core.Data
{
    /// <summary>
    /// Contains IDs related to the user.
    /// </summary>
    public interface I51DidData : IAspectData
    {
        /// <summary>
        /// Probabilistic 51Did,
        /// unique across all callers
        /// from the same device and network.
        /// </summary>
        IAspectPropertyValue<string> IdProbGlobal { get; }

        /// <summary>
        /// Probabilistic 51Did,
        /// unique only across the caller’s license key.
        /// </summary>
        IAspectPropertyValue<string> IdProbLic { get; }
    }
}
