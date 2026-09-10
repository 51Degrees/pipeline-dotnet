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
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;

namespace FiftyOne.Pipeline.DerivedProperty.Tests;

/// <summary>
/// Aspect data for <see cref="SlowSourceEngine"/>. It has to be aspect
/// data rather than plain element data, because lazy loading is a feature
/// of an aspect engine and the waiting happens inside
/// <see cref="AspectDataBase"/>.
/// </summary>
public class SlowSourceData : AspectDataBase
{
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="logger">The logger for the new instance to use.</param>
    /// <param name="pipeline">The pipeline the data belongs to.</param>
    /// <param name="engine">The engine that creates the data.</param>
    public SlowSourceData(
        ILogger<AspectDataBase> logger,
        IPipeline pipeline,
        IAspectEngine engine)
        : base(logger, pipeline, engine)
    {
    }
}

/// <summary>
/// An aspect engine that takes a while to produce its value, so that a
/// pipeline configured for lazy loading returns from Process before the
/// value exists.
///
/// It is here to prove that the derived property element waits. An engine
/// with lazy loading has its aspect data added to the flow data
/// immediately and filled on another thread, so an element that reads the
/// dictionary as it stands sees nothing and the derived property loses its
/// value on a race rather than reliably, which is worse than losing it
/// outright.
/// </summary>
public class SlowSourceEngine
    : AspectEngineBase<SlowSourceData, IAspectPropertyMetaData>
{
    private readonly TimeSpan _cost;

    private readonly IList<IAspectPropertyMetaData> _properties;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="logger">The logger for the new instance to use.</param>
    /// <param name="dataFactory">Creates the aspect data.</param>
    /// <param name="cost">
    /// How long the engine takes to produce its value.
    /// </param>
    public SlowSourceEngine(
        ILogger<AspectEngineBase<SlowSourceData, IAspectPropertyMetaData>>
            logger,
        Func<IPipeline,
            FlowElementBase<SlowSourceData, IAspectPropertyMetaData>,
            SlowSourceData> dataFactory,
        TimeSpan cost)
        : base(logger, dataFactory)
    {
        _cost = cost;
        _properties = new List<IAspectPropertyMetaData>()
        {
            new AspectPropertyMetaData(
                this,
                "Verified",
                typeof(bool),
                string.Empty,
                new List<string>(),
                true)
        };
    }

    /// <summary>
    /// The one property this engine supplies.
    /// </summary>
    public override IList<IAspectPropertyMetaData> Properties => _properties;

    /// <summary>
    /// The element data key the value is published under.
    /// </summary>
    public override string ElementDataKey => "signature";

    /// <summary>
    /// This engine reads no evidence.
    /// </summary>
    public override IEvidenceKeyFilter EvidenceKeyFilter { get; } =
        new EvidenceKeyFilterWhitelist(new List<string>());

    /// <summary>
    /// Not a real data source, so there is no tier to report.
    /// </summary>
    public override string DataSourceTier => "test";

    /// <summary>
    /// Waits, and only then writes the value, which is what makes a reader
    /// that does not wait read nothing.
    /// </summary>
    /// <param name="data">The flow data being processed.</param>
    /// <param name="aspectData">The aspect data to populate.</param>
    protected override void ProcessEngine(
        IFlowData data, SlowSourceData aspectData)
    {
        Thread.Sleep(_cost);
        if (aspectData != null)
        {
            aspectData["Verified"] = true;
        }
    }

    /// <summary>
    /// Nothing to clean up.
    /// </summary>
    protected override void ManagedResourcesCleanup()
    {
    }

    /// <summary>
    /// Nothing to clean up.
    /// </summary>
    protected override void UnmanagedResourcesCleanup()
    {
    }
}
