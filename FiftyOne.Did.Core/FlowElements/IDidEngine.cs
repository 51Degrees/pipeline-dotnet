using FiftyOne.Did.Core.Data;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;

namespace FiftyOne.Did.Core.FlowElements
{
    /// <summary>
    /// General interface for 51Did engines.
    /// </summary>
    public interface IDidEngine : IAspectEngine<I51DidData, IAspectPropertyMetaData>
    {
        
    }
}