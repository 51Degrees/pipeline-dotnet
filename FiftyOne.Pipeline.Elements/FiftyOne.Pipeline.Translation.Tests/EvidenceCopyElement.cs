using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Translation.Tests;

/// <summary>
/// Element that just copies the evidence into its element data. This is used 
/// for testing the translation engine without having to worry about the 
/// source element and its data structure.
/// </summary>
public class EvidenceCopyElement : FlowElementBase<EvidenceCopyData, ElementPropertyMetaData>
{
    public EvidenceCopyElement(
        ILogger<FlowElementBase<EvidenceCopyData, ElementPropertyMetaData>> logger)
        : base(logger)
    {
    }

    public override string ElementDataKey => "evidencecopy";

    public override IEvidenceKeyFilter EvidenceKeyFilter => new EvidenceKeyFilterWhitelist(new List<string>());

    public override IList<ElementPropertyMetaData> Properties => new List<ElementPropertyMetaData>();

    protected override void ManagedResourcesCleanup()
    {

    }

    protected override void ProcessInternal(IFlowData data)
    {
        var copyData = data.GetOrAdd(ElementDataKey, p => new EvidenceCopyData(null, p));
        foreach (var evidence in data.GetEvidence().AsDictionary())
        {
            copyData[evidence.Key] = evidence.Value;
        }
    }

    protected override void UnmanagedResourcesCleanup()
    {

    }
}
