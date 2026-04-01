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
