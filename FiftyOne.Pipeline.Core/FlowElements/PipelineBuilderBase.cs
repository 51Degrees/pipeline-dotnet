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

using FiftyOne.Pipeline.Core.Attributes;
using FiftyOne.Pipeline.Core.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.Pipeline.Core.FlowElements
{
    /// <summary>
    /// Abstract pipeline builder base class
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/conceptual-overview.md#pipeline-builder">Specification</see>
    /// </summary>
    public abstract class PipelineBuilderBase<T>
        where T : PipelineBuilderBase<T>
    {
        /// <summary>
        /// The elements to be added to the pipeline
        /// </summary>
        protected List<IFlowElement> FlowElements { get; private set; }
            = new List<IFlowElement>();

        /// <summary>
        /// Logger
        /// </summary>
        protected ILogger<T> Logger { get; private set; }

        /// <summary>
        /// A factory used to create logger instances.
        /// </summary>
        protected ILoggerFactory LoggerFactory { get; private set; }

        /// <summary>
        /// If true then Pipeline will call Dispose on its child elements
        /// when it is disposed.
        /// </summary>
        private bool _autoDisposeElements = Constants.PIPELINE_BUILDER_DEFAULT_AUTO_DISPOSE_ELEMENTS;

        /// <summary>
        /// If true then Pipeline will suppress exceptions added to
        /// <see cref="IFlowData.Errors"/>.
        /// </summary>
        private bool _suppressProcessExceptions = Constants.PIPELINE_BUILDER_DEFAULT_AUTO_SUPRESS_PROCESS_EXCEPTIONS;

        private ILogger<Evidence> _evidenceLogger;
        private ILogger<FlowData> _flowDataLogger;

        /// <summary>
        /// Create a new <see cref="PipelineBuilderBase{T}"/> instance.
        /// </summary>
        /// <param name="loggerFactory">
        /// The <see cref="ILoggerFactory"/> to use when creating logger
        /// instances.
        /// </param>
        public PipelineBuilderBase(
            ILoggerFactory loggerFactory)
        {
            LoggerFactory = loggerFactory;
            Logger = LoggerFactory.CreateLogger<T>();
            _evidenceLogger = LoggerFactory.CreateLogger<Evidence>();
            _flowDataLogger = LoggerFactory.CreateLogger<FlowData>();
        }

        /// <summary>
        /// Build the pipeline
        /// </summary>
        /// <returns>
        /// A new <see cref="IPipeline"/> instance containing the configured
        /// <see cref="IFlowElement"/> instances.
        /// </returns>
        public virtual IPipeline Build()
        {
            OnPreBuild();
            return new Pipeline(
                LoggerFactory.CreateLogger<Pipeline>(),
                NewFlowData,
                FlowElements,
                _autoDisposeElements,
                _suppressProcessExceptions);
        }

        /// <summary>
        /// Add the specified <see cref="IFlowElement"/> to the pipeline.
        /// Elements are typically executed sequentially in the order 
        /// they are added.
        /// </summary>
        /// <param name="element">
        /// The <see cref="IFlowElement"/> to add
        /// </param>
        /// <returns>
        /// This builder instance.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the element has already been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied element is null.
        /// </exception>
        public T AddFlowElement(IFlowElement element)
        {
            if(element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }
            if (element.IsDisposed)
            {
                throw new ObjectDisposedException(nameof(element));
            }
            FlowElements.Add(element);
            return this as T;
        }

        /// <summary>
        /// Add the specified <see cref="IFlowElement"/> array to the pipeline.
        /// These elements will all be started at the same time and executed
        /// in parallel using one thread for each element.
        /// </summary>
        /// <param name="elements">
        /// The <see cref="IFlowElement"/> array to add
        /// </param>
        /// <returns>
        /// This builder instance.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if any of the elements have already been disposed.
        /// </exception>
        public T AddFlowElementsParallel(params IFlowElement[] elements)
        {
            // Check if any of the elements being added have already 
            // been disposed
            var disposed = elements.FirstOrDefault(e => e.IsDisposed);
            if (disposed != null)
            {
                throw new ObjectDisposedException(nameof(disposed));
            }

            if (elements.Length == 1)
            {
                FlowElements.Add(elements[0]);
            }
            else if (elements.Length > 1)
            {
                var parallelElements = new ParallelElements(
                    LoggerFactory.CreateLogger<ParallelElements>(),
                    elements);
                FlowElements.Add(parallelElements);
            }

            return this as T;
        }

        /// <summary>
        /// Configure the Pipeline to either call dispose on it's child
        /// FlowElements when it is disposed or not.
        /// </summary>
        /// <param name="autoDispose">
        /// If true then Pipeline will call dispose on it's child elements
        /// when it is disposed.
        /// </param>
        /// <returns>
        /// This builder instance.
        /// </returns>
        [DefaultValue(Constants.PIPELINE_BUILDER_DEFAULT_AUTO_DISPOSE_ELEMENTS)]
        public T SetAutoDisposeElements(bool autoDispose)
        {
            _autoDisposeElements = autoDispose;
            return this as T;
        }

        /// <summary>
        /// Configure the Pipeline to either suppress exceptions added to
        /// <see cref="IFlowData.Errors"/> during processing or to throw them
        /// as an aggregate exception once processing is complete.
        /// </summary>
        /// <param name="suppressExceptions">
        /// If true then Pipeline will suppress exceptions added to
        /// <see cref="IFlowData.Errors"/>.
        /// </param>
        /// <returns>
        /// This builder instance.
        /// </returns>
#       pragma warning disable CS0618 // Type or member is obsolete
        [DefaultValue(Constants.PIPELINE_BUILDER_DEFAULT_AUTO_SUPRESS_PROCESS_EXCEPTION)]
#       pragma warning restore CS0618 // Type or member is obsolete
        [ObsoleteAttribute("This setter is obsolete. Use " + nameof(SetSuppressProcessExceptions) + " instead.", false)]
        public T SetSuppressProcessException(bool suppressExceptions)
        {
            return SetSuppressProcessExceptions(suppressExceptions);
        }

        /// <summary>
        /// Configure the Pipeline to either suppress exceptions added to
        /// <see cref="IFlowData.Errors"/> during processing or to throw them
        /// as an aggregate exception once processing is complete.
        /// </summary>
        /// <param name="suppressExceptions">
        /// If true then Pipeline will suppress exceptions added to
        /// <see cref="IFlowData.Errors"/>.
        /// </param>
        /// <returns>
        /// This builder instance.
        /// </returns>
        [DefaultValue(Constants.PIPELINE_BUILDER_DEFAULT_AUTO_SUPRESS_PROCESS_EXCEPTIONS)]
        public T SetSuppressProcessExceptions(bool suppressExceptions)
        {
            _suppressProcessExceptions = suppressExceptions;
            return this as T;
        }

        /// <summary>
        /// Called just before a pipeline is built.
        /// </summary>
        protected virtual void OnPreBuild() { }

        /// <summary>
        /// Factory method that will be used by the created 
        /// <see cref="IPipeline"/> to create new <see cref="FlowData"/> 
        /// instances.
        /// </summary>
        /// <param name="pipeline">
        /// The pipeline that is being used to create the flow data instance.
        /// </param>
        /// <returns>
        /// A new <see cref="FlowData"/> that is linked to the given pipeline.
        /// </returns>
        private IFlowData NewFlowData(IPipelineInternal pipeline)
        {
            var evidence = new Evidence(_evidenceLogger);
            return new FlowData(
                _flowDataLogger,
                pipeline,
                evidence);
        }

    }
}
