using FiftyOne.DiD.Core.Data;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;

namespace FiftyOne.DiD.Core.FlowElements
{
    /// <summary>
    /// General interface for 51DiD engines.
    /// </summary>
    public interface IDiDEngine : IAspectEngine<I51DidData, IAspectPropertyMetaData>
    {
        
    }
}