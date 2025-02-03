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
using FiftyOne.Pipeline.Core.TypedMap;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Core.FlowElements
{
    /// <summary>
    /// <see cref="IFlowElement"/> is the basic building block of a pipeline.
    /// All FlowElements must implement it.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/conceptual-overview.md#flow-element">Specification</see>
    /// </summary>
    public interface IFlowElement : IDisposable
    {
        /// <summary>
        /// Process the given <see cref="IFlowData"/> with this FlowElement.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> instance that provides input evidence
        /// and carries the output data to the user.
        /// </param>
        void Process(IFlowData data);

        /// <summary>
        /// Called when this element is added to a pipeline.
        /// </summary>
        /// <param name="pipeline">
        /// The pipeline that the element has been added to
        /// </param>
        void AddPipeline(IPipeline pipeline);

        /// <summary>
        /// A filter that will only include the evidence keys that this 
        /// Flow Element can make use of.
        /// </summary>
        IEvidenceKeyFilter EvidenceKeyFilter { get; }

        /// <summary>
        /// The string name of the key used to access the data populated 
        /// by this element in the <see cref="IFlowData"/>.
        /// </summary>
        string ElementDataKey { get; }

        /// <summary>
        /// True if the element starts multiple threads. False otherwise.
        /// </summary>
        bool IsConcurrent { get; }

        /// <summary>
        /// True if the element has been disposed
        /// </summary>
        bool IsDisposed { get; }

        /// <summary>
        /// Details of the properties that this element can populate.
        /// MUST be semantically persistent (i.e. cacheable by IPipeline).
        /// WILL be called from within the pipeline during initialization.
        /// </summary>
        /// <exception cref="Exceptions.PropertiesNotYetLoadedException">
        /// Thrown if properties are not available yet
        /// but MAY(!) be re-requested later.
        /// Means the component MIGHT still recover from the error
        /// and return populated value sometime later.
        /// </exception>
        /// <exception cref="Exceptions.PipelineException">
        /// Thrown when the component detects UNRECOVERABLE errors.
        /// If thrown on the first call to this component,
        /// WILL propagate through pipeline's constructor.
        /// </exception>
        IList<IElementPropertyMetaData> Properties { get; }
    }

    /// <summary>
    /// Generic interface that extends <see cref="IFlowElement"/>
    /// with the ability to return a <see cref="ITypedKey{T}"/> that
    /// will allow type-safe access to the data that this element
    /// populates in <see cref="IFlowData"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The type of element data that the flow element will write to 
    /// <see cref="IFlowData"/>.
    /// </typeparam>
    /// <typeparam name="TMeta">
    /// The type of meta data that the flow element will supply 
    /// about the properties it populates.
    /// </typeparam>
    public interface IFlowElement<T, TMeta> : IFlowElement
        where T : IElementData
        where TMeta : IElementPropertyMetaData
    {
        /// <summary>
        /// Typed data key used for retrieving strongly typed element data.
        /// </summary>
        ITypedKey<T> ElementDataKeyTyped { get; }

        /// <summary>
        /// Details of the properties that this element can populate 
        /// </summary>
        new IList<TMeta> Properties { get; }
    }
}
