/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2025 51 Degrees Mobile Experts Limited, Davidson House,
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
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Engines.TestHelpers
{
    public class EmptyEngine : AspectEngineBase<EmptyEngineData, IAspectPropertyMetaData>
    {
        private TimeSpan? _processCost = null;

        private Exception _exception = null;

        public event Action OnProcessEngineEntered;
        public event Action OnWillDelayProcessEngine;

        public EmptyEngine(
            ILogger<AspectEngineBase<EmptyEngineData, IAspectPropertyMetaData>> logger,
            Func<IPipeline, FlowElementBase<EmptyEngineData, IAspectPropertyMetaData>, EmptyEngineData> aspectDataFactory) :
            base(logger, aspectDataFactory)
        {
            _properties = new List<IAspectPropertyMetaData>()
            {
                new AspectPropertyMetaData(this, "valueone", typeof(int), "", new List<string>(), true),
                new AspectPropertyMetaData(this, "valuetwo", typeof(int), "", new List<string>(), true)
            };
        }

        public void SetProcessCost(long ticks)
        {
            _processCost = TimeSpan.FromTicks(ticks);
        }

        public void SetException(Exception exception)
        {
            _exception = exception;
        }

        private List<IAspectPropertyMetaData> _properties = null;
        public override IList<IAspectPropertyMetaData> Properties => _properties;

        public override string ElementDataKey => "empty-aspect";

        private EvidenceKeyFilterWhitelist _evidenceInclusionList = 
            new EvidenceKeyFilterWhitelist(new List<string>()
        {
            "test.value"
        });
        public override IEvidenceKeyFilter EvidenceKeyFilter => _evidenceInclusionList;

        public override string DataSourceTier => throw new NotImplementedException();

        protected override void ProcessEngine(IFlowData data, EmptyEngineData aspectData)
        {
            Logger.LogDebug($"Did enter {nameof(ProcessEngine)}.");
            OnProcessEngineEntered?.Invoke();
            if (_exception != null)
            {
                throw _exception;
            }
            aspectData.ValueOne = 1;
            Logger.LogDebug($"Did assign {nameof(aspectData.ValueOne)}.");
            if (_processCost.HasValue)
            {
                Logger.LogDebug($"Will notify of incoming delay...");
                OnWillDelayProcessEngine?.Invoke();
                Logger.LogDebug($"Will wait for {_processCost.Value}...");
                Task.Delay(_processCost.Value).Wait();
                Logger.LogDebug($"Did wait for {_processCost.Value}...");
            }
            aspectData.ValueTwo = 2;
            Logger.LogDebug($"Did assign {nameof(aspectData.ValueTwo)}.");
        }

        protected override void ManagedResourcesCleanup()
        {
        }

        protected override void UnmanagedResourcesCleanup()
        {
        }
    }
}
