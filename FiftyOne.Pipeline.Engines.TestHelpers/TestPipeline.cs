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
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Engines.TestHelpers
{
    /// <summary>
    /// Helper class that allows an IPipeline instance to be used
    /// in the creation of a <see cref="TestFlowData"/> instance.
    /// </summary>
    public class TestPipeline
    {
        private class PipelineAdapter : IPipelineInternal
        {
            private IPipeline _pipeline;

            public PipelineAdapter(IPipeline pipeline)
            {
                _pipeline = pipeline;
            }

            public IEvidenceKeyFilter EvidenceKeyFilter => _pipeline.EvidenceKeyFilter;

            public bool IsConcurrent => _pipeline.IsConcurrent;

            public bool IsDisposed => _pipeline.IsDisposed;

            public bool SuppressProcessExceptions => _pipeline.SuppressProcessExceptions;

            public IReadOnlyList<IFlowElement> FlowElements => _pipeline.FlowElements;

            public IReadOnlyDictionary<string, IReadOnlyDictionary<string, IElementPropertyMetaData>> 
                ElementAvailableProperties => _pipeline.ElementAvailableProperties;

            public IFlowData CreateFlowData()
            {
                return _pipeline.CreateFlowData();
            }

            public void Dispose()
            {
                _pipeline.Dispose();
            }

            public IElementPropertyMetaData GetMetaDataForProperty(string propertyName)
            {
                var pipelineInternal = _pipeline as IPipelineInternal;
                if (pipelineInternal == null)
                {
                    throw new NotImplementedException("Unable to get " +
                        "meta data for property using the TestPipeline class. " +
                        "Either create a real pipeline instance or avoid " +
                        "calling GetMetaDataForProperty().");
                }
                else
                {
                    return pipelineInternal.GetMetaDataForProperty(propertyName);
                }
            }

            public bool HasExpectedElementAfter<TElement, TExpectedElement>()
                where TElement : IFlowElement
                where TExpectedElement : IFlowElement
            {
                return _pipeline.HasExpectedElementAfter<TElement, TExpectedElement>();
            }

            public void Process(IFlowData data)
            {
                var pipelineInternal = _pipeline as IPipelineInternal;
                if (pipelineInternal == null)
                {
                    throw new NotImplementedException("Unable to process data " +
                        "using the TestPipeline class. Either create a real " +
                        "pipeline instance or avoid calling Process().");
                } 
                else
                {
                    pipelineInternal.Process(data);
                }
            }

            TElement IPipeline.GetElement<TElement>()
            {
                return _pipeline.GetElement<TElement>();
            }
        }

        public TestPipeline(IPipeline pipeline)
        {
            InternalPipeline = new PipelineAdapter(pipeline);
        }

        internal IPipelineInternal InternalPipeline { get; private set; }
    }
}
