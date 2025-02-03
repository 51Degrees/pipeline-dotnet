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
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;

namespace FiftyOne.Pipeline.Core.FlowElements
{
    /// <summary>
    /// Abstract base class for Flow Elements.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/conceptual-overview.md#flow-element">Specification</see>
    /// </summary>
    /// <remarks>
    /// It is not a requirement for all FlowElements to extend 
    /// <see cref="FlowElementBase{T, TMeta}"/> but it is recommended.
    /// They must implement <see cref="IFlowElement"/> though.
    /// </remarks>
    /// <typeparam name="T">
    /// The type of element data that the flow element will write to 
    /// <see cref="IFlowData"/>.
    /// </typeparam>
    /// <typeparam name="TMeta">
    /// The type of meta data that the flow element will supply 
    /// about the properties it populates.
    /// </typeparam>
    public abstract class FlowElementBase<T, TMeta> : IFlowElement<T, TMeta>
        where T : IElementData
        where TMeta : IElementPropertyMetaData
    {
        /// <summary>
        /// True if the instance has been disposed.
        /// </summary>
        private bool _disposed = false;

        /// <summary>
        /// The key used to access the data populated by this element
        /// in the <see cref="IFlowData"/>.
        /// </summary>
        private ITypedKey<T> _typedKey = null;
        
        /// <summary>
        /// The pipelines that this element has been added to
        /// </summary>
        private List<IPipeline> _pipelines = new List<IPipeline>();
        /// <summary>
        /// A factory function used to create the element data instances
        /// that are populated by this flow element.
        /// </summary>
        private Func<IPipeline, FlowElementBase<T, TMeta>, T> _elementDataFactory;

        /// <summary>
        /// The logger for this instance
        /// </summary>
        protected ILogger<FlowElementBase<T, TMeta>> Logger { get; private set; }

        /// <summary>
        /// Get a read only list of the pipelines that this element has
        /// been added to.
        /// </summary>
        public IReadOnlyList<IPipeline> Pipelines
        {
            get
            {
                return new ReadOnlyCollection<IPipeline>(_pipelines);
            }
        }

        /// <summary>
        /// The string name of the key used to access the data populated 
        /// by this element in the <see cref="IFlowData"/>.
        /// </summary>
        public abstract string ElementDataKey { get; }

        /// <summary>
        /// A list of all the evidence keys that this Flow Element can
        /// make use of.
        /// </summary>
        public abstract IEvidenceKeyFilter EvidenceKeyFilter { get; }

        /// <summary>
        /// Details of the properties that this engine can populate 
        /// </summary>
        public abstract IList<TMeta> Properties { get; }

        /// <summary>
        /// Provide an implementation for the non-generic version
        /// of the meta-data property.
        /// </summary>
        IList<IElementPropertyMetaData> IFlowElement.Properties
        {
            get
            {
                return Properties.Cast<IElementPropertyMetaData>().ToList();
            }
        }

        /// <summary>
        /// True if the element can be run totally asynchronously,
        /// false otherwise.
        /// This should only return true if the FlowElement does not
        /// modify the IFlowData or it's values are lazily loaded.
        /// Otherwise the process method may return before the element 
        /// has completed processing.
        /// </summary>
        public virtual bool Asynchronous { get { return false; } }

        /// <summary>
        /// True if the element starts multiple threads. False otherwise.
        /// </summary>
        public virtual bool IsConcurrent { get { return false; } }

        /// <summary>
        /// True if the element has been disposed
        /// </summary>
        public bool IsDisposed
        {
            get
            {
                return _disposed;
            }
        }

        /// <summary>
        /// Get the key used to access the data populated by this element
        /// in the <see cref="IFlowData"/>.
        /// </summary>
        public ITypedKey<T> ElementDataKeyTyped
        {
            get
            {
                if (_typedKey == null)
                {
                    _typedKey = new TypedKey<T>(ElementDataKey);
                }
                return _typedKey;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">
        /// Used for logging.
        /// </param>
        public FlowElementBase(
            ILogger<FlowElementBase<T, TMeta>> logger) : this(logger, null) { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">
        /// Used for logging.
        /// </param>
        /// <param name="elementDataFactory">
        /// The factory function to use when creating a 
        /// <see cref="ElementDataBase"/> instance.
        /// </param>
        public FlowElementBase(
            ILogger<FlowElementBase<T, TMeta>> logger,
            Func<IPipeline, FlowElementBase<T, TMeta>, T> elementDataFactory)
        {
            Logger = logger ?? NullLogger<FlowElementBase<T, TMeta>>.Instance;
            Logger.LogInformation($"FlowElement '{GetType().Name}' created.");
            _elementDataFactory = elementDataFactory;
        }

        /// <summary>
        /// Process the given <see cref="IFlowData"/> with this FlowElement.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> instance that provides input evidence
        /// and carries the output data to the user.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied data parameter is null.
        /// </exception>
        public virtual void Process(IFlowData data)
        {
            if(data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            Stopwatch sw = null;
#pragma warning disable CS0618 // Type or member is obsolete
            // This usage will be replaced once the Cancellation Token
            // mechanism is available.
            if (data.Stop == false)
#pragma warning restore CS0618 // Type or member is obsolete
            {
                bool log = Logger.IsEnabled(LogLevel.Debug);
                if (log)
                {
                    Logger.LogDebug($"FlowElement " +
                    $"'{GetType().Name}' started processing.");
                    sw = Stopwatch.StartNew();
                }
                ProcessInternal(data);
                if (log)
                {
                    Logger.LogDebug($"FlowElement " +
                        $"'{GetType().Name}' finished processing. " +
                        $"Elapsed time: {sw.ElapsedMilliseconds}ms");
                }
            }
        }

        /// <summary>
        /// Abstract method called by <see cref="Process(IFlowData)"/>.
        /// Extending classes should perform their processing in this method. 
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> instance that provides input evidence
        /// and carries the output data to the user.
        /// </param>
        protected abstract void ProcessInternal(IFlowData data);

        /// <summary>
        /// Called when this element is added to a pipeline.
        /// </summary>
        /// <param name="pipeline">
        /// The pipeline that the element has been added to
        /// </param>
        public virtual void AddPipeline(IPipeline pipeline)
        {
            _pipelines.Add(pipeline);
        }

        /// <summary>
        /// Method used to create element data instances that are populated
        /// by this flow element
        /// </summary>
        /// <returns></returns>
        protected virtual T CreateElementData(IPipeline pipeline)
        {
            if(_elementDataFactory == null)
            {
                Logger.LogError($"Need to specify an elementDataFactory " +
                    $"in constructor for '{GetType().Name}'.");
            }
            return _elementDataFactory(pipeline, this);
        }

        #region IDisposable Support
        /// <summary>
        /// Cleanup any managed resources that the element is using
        /// </summary>
        protected abstract void ManagedResourcesCleanup();

        /// <summary>
        /// Cleanup any unmanaged resources that the element is using
        /// </summary>
        protected abstract void UnmanagedResourcesCleanup();

        /// <summary>
        /// Dispose of any resources.
        /// </summary>
        /// <param name="disposing">
        /// True if Dispose is being called 'correctly' from the Dispose
        /// method.
        /// False if Dispose is being called by the finalizer.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Logger.LogInformation($"FlowElement '{GetType().Name}' disposed.");
                    ManagedResourcesCleanup();
                }
                else
                {
                    Logger.LogWarning($"FlowElement '{GetType().Name}' " +
                        $"finalized. It is recommended that instance lifetimes are " +
                        $"managed explicitly with a 'using' block or calling the Dispose " +
                        $"method as part of a 'finally' block.");
                }
                UnmanagedResourcesCleanup();

                _disposed = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~FlowElementBase()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            // This line is required to prevent the finalizer above from
            // firing when this instance is finalized.
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}