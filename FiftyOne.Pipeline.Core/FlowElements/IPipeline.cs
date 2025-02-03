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
using FiftyOne.Pipeline.Core.Exceptions;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Core.FlowElements
{
    /// <summary>
    /// The public interface for a Pipeline.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/conceptual-overview.md#pipeline">Specification</see>
    /// </summary>
    public interface IPipeline : IDisposable
    {
        /// <summary>
        /// Create a new <see cref="IFlowData"/> instance.
        /// </summary>
        /// <returns>
        /// A new <see cref="IFlowData"/> instance.
        /// </returns>
        IFlowData CreateFlowData();

        /// <summary>
        /// Get a filter that will only include the evidence keys that can 
        /// be used by at least one <see cref="IFlowElement"/> within 
        /// this pipeline.
        /// </summary>
        IEvidenceKeyFilter EvidenceKeyFilter { get; }

        /// <summary>
        /// True if any of the <see cref="IFlowElement"/>s in this pipeline
        /// will create multiple threads and execute in parallel.
        /// False otherwise.
        /// </summary>
        bool IsConcurrent { get; }

        /// <summary>
        /// True if the pipeline has been disposed
        /// </summary>
        bool IsDisposed { get; }

        /// <summary>
        /// Control field that indicates if the Pipeline will throw an
        /// aggregate exception during processing or suppress it and ignore the
        /// exceptions added to <see cref="IFlowData.Errors"/>.
        /// </summary>
        bool SuppressProcessExceptions { get; }

        /// <summary>
        /// Check if the pipeline contains an instance of <typeparamref name="TExpectedElement"/>
        /// that will be executed after <typeparamref name="TElement"/>.
        /// </summary>
        /// <typeparam name="TElement">
        /// The type of the element to check.
        /// </typeparam>
        /// <typeparam name="TExpectedElement">
        /// The type of the element that should come after <typeparamref name="TElement"/>.
        /// </typeparam>
        /// <returns>
        /// True if <typeparamref name="TExpectedElement"/> is present in the pipeline and will be 
        /// executed after <typeparamref name="TElement"/>
        /// </returns>
        bool HasExpectedElementAfter<TElement, TExpectedElement>()
            where TElement : IFlowElement
            where TExpectedElement : IFlowElement;

        /// <summary>
        /// Get the specified element from the pipeline.
        /// </summary>
        /// <remarks>
        /// If the pipeline contains multiple elements of the requested type,
        /// this method will return null.
        /// </remarks>
        /// <typeparam name="TElement">
        /// The type of the <see cref="IFlowElement"/> to get
        /// </typeparam>
        /// <returns>
        /// An instance of the specified <see cref="IFlowElement"/> if the 
        /// pipeline contains one. 
        /// Null is returned if there is no such instance or there are 
        /// multiple instances of that type.
        /// </returns>
        TElement GetElement<TElement>()
            where TElement : class, IFlowElement;

        /// <summary>
        /// Get a read only list of the flow elements that are part of this 
        /// pipeline.
        /// </summary>
        IReadOnlyList<IFlowElement> FlowElements { get; }

        /// <summary>
        /// Get the dictionary of available properties for an
        /// <see cref="IFlowElement.ElementDataKey"/>. The dictionary returned
        /// contains the <see cref="IElementPropertyMetaData"/>s keyed on the
        /// name field.
        /// </summary>
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, IElementPropertyMetaData>> ElementAvailableProperties { get; }
    }

    /// <summary>
    /// Internal interface for a pipeline.
    /// Allows <see cref="IFlowData"/> to call the pipeline's Process method.
    /// </summary>
    internal interface IPipelineInternal : IPipeline
    {
        /// <summary>
        /// Process the given <see cref="IFlowData"/> using the 
        /// <see cref="IFlowElement"/>s in the pipeline.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> that contains the evidence and will
        /// allow the user to access the results.
        /// </param>
        void Process(IFlowData data);

        /// <summary>
        /// Get the meta data for the specified property name.
        /// If there are no properties with that name or multiple 
        /// properties on different elements then an exception will 
        /// be thrown.
        /// </summary>
        /// <param name="propertyName">
        /// The property name to find the meta data for
        /// </param>
        /// <returns>
        /// The meta data associated with the specified property name
        /// </returns>
        /// <exception cref="PipelineDataException">
        /// Thrown if the property name is associated with zero or 
        /// multiple elements.
        /// </exception>
        IElementPropertyMetaData GetMetaDataForProperty(string propertyName);
    }
}
