using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging;

namespace FiftyOne.Pipeline.Elements.Translation.Tests;

/// <summary>
/// Data type for the <see cref="EvidenceCopyElement"/>.
/// </summary>
public class EvidenceCopyData : ElementDataBase
{
    public EvidenceCopyData(
        ILogger<ElementDataBase> logger,
        IPipeline pipeline) : base(logger, pipeline)
    {
    }
}
