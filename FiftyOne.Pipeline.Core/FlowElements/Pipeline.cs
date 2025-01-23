/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2023 51 Degrees Mobile Experts Limited, Davidson House,
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
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace FiftyOne.Pipeline.Core.FlowElements
{
    /// <summary>
    /// A pipeline is used to create <see cref="IFlowData"/> instances
    /// which then automatically use the pipeline when their Process 
    /// method is called.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/conceptual-overview.md#pipeline">Specification</see>
    /// </summary>
#pragma warning disable CA1724 // Class name 'Pipeline' conflicts with namespace
    // Pipeline is the correct name for both the Class and the namespace.
    // Users will generally be dealing with the IPipeline interface 
    // rather than the class directly.
    public class Pipeline : IPipelineInternal
#pragma warning restore CA1724 // Class name 'Pipeline' conflicts with namespace
    {
        /// <summary>
        /// The pipeline maintains a dictionary of the elements it contains 
        /// indexed by type. This is used by the GetElement method.
        /// </summary>
        private Dictionary<Type, List<IFlowElement>> _elementsByType =
            new Dictionary<Type, List<IFlowElement>>();

        /// <summary>
        /// The pipeline maintains a dictionary of property meta data
        /// indexed by property name. This is used by the
        /// GetElementDataKeyForProperty method.
        /// </summary>
        private ConcurrentDictionary<string, IElementPropertyMetaData> _metaDataByPropertyName = 
            new ConcurrentDictionary<string, IElementPropertyMetaData>();

        /// <summary>
        /// A factory method that is used to create new 
        /// <see cref="IFlowData"/> instances.
        /// </summary>
        private Func<IPipelineInternal, IFlowData> _flowDataFactory;

        /// <summary>
        /// The <see cref="IFlowElement"/>s that make up this pipeline.
        /// </summary>
        private List<IFlowElement> _flowElements;

        /// <summary>
        /// A filter that will only include the evidence keys that can 
        /// be used by at least one <see cref="IFlowElement"/> within 
        /// this pipeline.
        /// (Will only be populated after the <see cref="EvidenceKeyFilter"/>
        /// property is used.)
        /// </summary>
        private EvidenceKeyFilterAggregator _evidenceKeyFilter;
        /// <summary>
        /// Provides an object to lock on when populating the 
        /// evidence key filter.
        /// </summary>
        private object _evidenceKeyFilterLock = new object();

        /// <summary>
        /// True if the instance is disposed. False otherwise.
        /// </summary>
        private bool _disposed = false;

        /// <summary>
        /// True if multiple <see cref="IFlowElement"/> instances will run 
        /// concurrently within this pipeline. False otherwise.
        /// (Will be null until the <see cref="IsConcurrent"/> property
        /// is used.)
        /// </summary>
        private bool? _concurrent = null;

        /// <summary>
        /// Control field that indicates if the Pipeline will automatically
        /// call Dispose on child elements when it is disposed or not.
        /// </summary>
        private bool _autoDisposeElements;

        /// <summary>
        /// Control field that indicates if the Pipeline will throw an
        /// aggregate exception during processing or suppress it and ignore the
        /// exceptions added to <see cref="IFlowData.Errors"/>.
        /// </summary>
        private bool _suppressProcessExceptions;

        private IReadOnlyDictionary<string, IReadOnlyDictionary<string, IElementPropertyMetaData>> _elementAvailableProperties;

        /// <summary>
        /// Logger
        /// </summary>
        private ILogger<Pipeline> _logger;
        
        /// <summary>
        /// Get a filter that will only include the evidence keys that can 
        /// be used by at least one <see cref="IFlowElement"/> within 
        /// this pipeline.
        /// </summary>
        public IEvidenceKeyFilter EvidenceKeyFilter
        {
            get
            {
                if(_evidenceKeyFilter == null)
                {
                    lock (_evidenceKeyFilterLock)
                    {
                        if (_evidenceKeyFilter == null)
                        {
                            var evidenceKeyFilter = 
                                new EvidenceKeyFilterAggregator();
                            foreach (var filter in _flowElements.Select(e => 
                                e.EvidenceKeyFilter))
                            {
                                evidenceKeyFilter.AddFilter(filter);
                            }
                            _evidenceKeyFilter = evidenceKeyFilter;
                        }
                    }
                }
                return _evidenceKeyFilter;
            }
        }

        /// <summary>
        /// True if multiple <see cref="IFlowElement"/> instances will run 
        /// concurrently within this pipeline. False otherwise.
        /// </summary>
        public bool IsConcurrent
        {
            get
            {
                if (_concurrent.HasValue == false)
                {
                    _concurrent = _flowElements.Any(e => e.IsConcurrent);
                }
                return _concurrent.Value;
            }
        }

        /// <summary>
        /// True if the pipeline has been disposed
        /// </summary>
        public bool IsDisposed
        {
            get
            {
                return _disposed;
            }
        }

        /// <summary>
        /// Control field that indicates if the Pipeline will throw an
        /// aggregate exception during processing or suppress it and ignore the
        /// exceptions added to <see cref="IFlowData.Errors"/>.
        /// </summary>
        public bool SuppressProcessExceptions => _suppressProcessExceptions;

        /// <summary>
        /// Get a read only list of the flow elements that are part of 
        /// this pipeline.
        /// </summary>
        /// <remarks>
        /// If there are no <see cref="ParallelElements"/> in the flow 
        /// elements then this method simply returns the the internal
        /// <see cref="_flowElements"/> list. If not then the flow 
        /// elements in instances of ParallelElements are included.
        /// </remarks>
        /// <remarks>
        /// Instances of ParallelElements are never included.
        /// </remarks>
        /// <remarks>
        /// The double check lock could be removed if a 'Complete'
        /// method were added to the pipeline. This method 
        /// would indicate that the pipeline would have completed all of 
        /// its set up tasks and FlowElements would be in a static state.
        /// </remarks>
        public IReadOnlyList<IFlowElement> FlowElements
        {
            get
            {
                if (_allPublicFlowElements == null)
                {
                    lock(_flowElementsLock)
                    {
                        if (_allPublicFlowElements == null)
                        {
                            _allPublicFlowElements =
                                GetAllPublicFlowElements();
                        }
                    }
                }
                return _allPublicFlowElements;
            }
        }
        private readonly object _flowElementsLock = new object();
        private IReadOnlyList<IFlowElement> _allPublicFlowElements;

        private IReadOnlyList<IFlowElement> GetAllPublicFlowElements()
        {
            if (_flowElements.Any(i =>
                i is ParallelElements))
            {
                var result = new List<IFlowElement>();
                var queue = new Queue<IFlowElement>(_flowElements);
                while (queue.Count > 0)
                {
                    var element = queue.Dequeue();
                    if (element is ParallelElements elements)
                    {
                        foreach (var child in 
                            elements.FlowElements)
                        {
                            queue.Enqueue(child);
                        }
                    }
                    else if (element.GetType().IsPublic)
                    {
                        result.Add(element);
                    }
                }
                return result.AsReadOnly();
            }
            else
            {
                return _flowElements;
            }
        }

        /// <summary>
        /// Get the dictionary of available properties for an
        /// <see cref="IFlowElement.ElementDataKey"/>. The dictionary returned
        /// contains the <see cref="IElementPropertyMetaData"/>s keyed on the
        /// name field.
        /// </summary>
        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, IElementPropertyMetaData>> ElementAvailableProperties
        {
            get
            {
                if (_elementAvailableProperties is object)
                {
                    return _elementAvailableProperties;
                }
                lock (_elementAvailablePropertiesLock)
                {
                    if (_elementAvailableProperties is object)
                    {
                        return _elementAvailableProperties;
                    }
                    bool hadFailures = false;
                    var properties = GetElementAvailableProperties(_flowElements, out hadFailures);
                    if (!hadFailures)
                    {
                        _elementAvailableProperties = properties;
                    }
                    return properties;
                }
            }
        }

        private object _elementAvailablePropertiesLock = new object();

        /// <summary>
        /// Constructor.
        /// Calls back to each element via <see cref="IFlowElement.Properties"/>,
        /// allowing them to perform some validations and throw.
        /// </summary>
        /// <param name="logger">
        /// Used for logging.
        /// </param>
        /// <param name="flowDataFactory">
        /// Factory method used to create new <see cref="IFlowData"/>
        /// instances.
        /// </param>
        /// <param name="flowElements">
        /// The <see cref="IFlowElement"/> instances that make up this 
        /// pipeline.
        /// </param>
        /// <param name="autoDisposeElements">
        /// If true then Pipeline will call Dispose on it's child elements
        /// when it is disposed.
        /// </param>
        /// <param name="suppressProcessExceptions">
        /// If true then Pipeline will suppress exceptions added to
        /// <see cref="IFlowData.Errors"/>.
        /// </param>
        /// <exception cref="PipelineException">
        /// Thrown by the flow element(s) detecting UNRECOVERABLE errors.
        /// In case of compromised pipeline integrity
        /// <see cref="PipelineConfigurationException"/> may be used.
        /// </exception>
        internal Pipeline(
            ILogger<Pipeline> logger,
            Func<IPipelineInternal, IFlowData> flowDataFactory,
            List<IFlowElement> flowElements,
            bool autoDisposeElements,
            bool suppressProcessExceptions)
        {
            _logger = logger;
            _flowDataFactory = flowDataFactory;
            _flowElements = flowElements;
            _autoDisposeElements = autoDisposeElements;
            _suppressProcessExceptions = suppressProcessExceptions;

            _elementsByType = new Dictionary<Type, List<IFlowElement>>();
            AddElementsByType(_flowElements);

            _ = ElementAvailableProperties; // perform caching attempt (default happy path)

            _logger.LogInformation($"Pipeline '{GetHashCode()}' created.");
        }

        /// <summary>
        /// Create a new <see cref="IFlowData"/> instance that will use this
        /// pipeline when processing.
        /// </summary>
        /// <returns></returns>
        public IFlowData CreateFlowData()
        {
            return _flowDataFactory(this);
        }

        /// <summary>
        /// Process the given <see cref="IFlowData"/> using the 
        /// <see cref="IFlowElement"/>s in the pipeline.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> that contains the evidence and will
        /// allow the user to access the results.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if a required parameter is null.
        /// </exception>
        /// <exception cref="AggregateException">
        /// Thrown if an error occurred during processing, 
        /// unless <see ref="SuppressProcessExceptions"/> is true.
        /// </exception>
        public void Process(IFlowData data)
        {
            if(data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            var log = _logger.IsEnabled(LogLevel.Debug);
            if (log)
            {
                _logger.LogDebug($"Pipeline '{GetHashCode()}' started processing.");
            }

            foreach (var element in _flowElements)
            {
                try
                {
                    element.Process(data);
#pragma warning disable CS0618 // Type or member is obsolete
                    // This usage will be replaced once the Cancellation Token
                    // mechanism is available.
                    if (data.Stop) break;
#pragma warning restore CS0618 // Type or member is obsolete
                }
#pragma warning disable CA1031 // Do not catch general exception types
                // We want to catch any exception here so that the
                // Pipeline can manage it.
                catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    // If an error occurs then store it in the 
                    // FlowData object.
                    data.AddError(ex, element);
                }
            }

            // If any errors have occurred and exceptions are not
            // suppressed, then throw an aggregate exception.
            if (data.Errors != null &&
                data.Errors.Count > 0 &&
                SuppressProcessExceptions == false)
            {
                throw new AggregateException(data.Errors
                    .Where(e => e.ShouldThrow == true)
                    .Select(e => e.ExceptionData));
            }

            if (log)
            {
                _logger.LogDebug($"Pipeline '{GetHashCode()}' finished processing.");
            }
        }

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
        public TElement GetElement<TElement>()
            where TElement : class, IFlowElement
        {
            TElement result = null;
            List<IFlowElement> elements;
            if (_elementsByType.TryGetValue(typeof(TElement), out elements))
            {
                try
                {
                    result = elements.SingleOrDefault() as TElement;
                }
                catch (InvalidOperationException)
                {
                    result = null;
                }
            }
            else if (_elementsByType.Any(e => typeof(TElement).IsAssignableFrom(e.Key)))
            {
                var matches = _elementsByType.Where(e => typeof(TElement).IsAssignableFrom(e.Key));
                if(matches.Count() == 1 &&
                    matches.Single().Value.Count == 1)
                {
                    result = matches.Single().Value.Single() as TElement;
                }
            }

            return result;
        }


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
        public bool HasExpectedElementAfter<TElement, TExpectedElement>()
            where TElement : IFlowElement
            where TExpectedElement : IFlowElement
        {
            // Get the indicies for these elements.
            int elementIndex = GetElementIndex<TElement>();
            int expectedElementIndex = GetElementIndex<TExpectedElement>();

            return elementIndex >= 0 && expectedElementIndex >= 0 &&
                expectedElementIndex > elementIndex;
        }

        /// <summary>
        /// Get the index of the element matching the specified type.
        /// Note that if there are multiple instances matching the given type, the index of the 
        /// first instance will be returned.
        /// </summary>
        /// <typeparam name="TElement"></typeparam>
        /// <returns></returns>
        private int GetElementIndex<TElement>()
        {
            for (int i = 0; i < _flowElements.Count; i++)
            {
                // If the element is a ParallelElements instance then check the child elements.
                // If there is a match then we want to take the index from the top level element.
                if (_flowElements[i] is ParallelElements parallel)
                {
                    if (parallel.FlowElements.Any(e => typeof(TElement).IsAssignableFrom(e.GetType())))
                    {
                        return i;
                    }
                }
                else
                {
                    if (typeof(TElement).IsAssignableFrom(_flowElements[i].GetType()))
                    {
                        return i;
                    }
                }
            }
            return -1;
        }


        /// <summary>
        /// Add the specified flow elements to the 
        /// <see cref="_elementsByType"/> dictionary, which contains a list
        /// of all the elements in the pipeline indexed by type.
        /// </summary>
        /// <param name="elements">
        /// The <see cref="IFlowElement"/>s to add.
        /// </param>
        private void AddElementsByType(IReadOnlyList<IFlowElement> elements)
        {
            foreach (var element in elements)
            {
                element.AddPipeline(this);
                var type = element.GetType();
                // If the element is a ParallelElements then add it's child elements.
                if (type == typeof(ParallelElements))
                {
                    AddElementsByType((element as ParallelElements).FlowElements);
                }
                // Otherwise, just add the element directly.
                else
                {
                    List<IFlowElement> typeElements;
                    if (_elementsByType.TryGetValue(type, out typeElements) == false)
                    {
                        typeElements = new List<IFlowElement>();
                        _elementsByType.Add(type, typeElements);
                    }
                    typeElements.Add(element);
                }
            }
        }

        private void AddAvailableProperties(
            IReadOnlyList<IFlowElement> elements,
            IDictionary<string, IDictionary<string, IElementPropertyMetaData>> dictionary,
            ref bool hadFailures)
        {
            foreach (var element in elements)
            {
                if (element is ParallelElements)
                {
                    AddAvailableProperties(((ParallelElements)element).FlowElements, dictionary, ref hadFailures);
                }
                else
                {
                    IList<IElementPropertyMetaData> elementProps;
                    try
                    {
                        elementProps = element.Properties;
                    }
                    catch (Exception e)
                    when (e is PropertiesNotYetLoadedException || e is PipelineTemporarilyUnavailableException)
                    {
                        hadFailures = true;
                        continue;
                    }

                    if (dictionary.ContainsKey(element.ElementDataKey) == false)
                    {
                        dictionary[element.ElementDataKey] =
                            new Dictionary<string, IElementPropertyMetaData>(
                                StringComparer.OrdinalIgnoreCase);
                    }
                    var availableElementProperties = dictionary[element.ElementDataKey];
                    foreach (var property in elementProps.Where(p => p.Available))
                    {
                        if (availableElementProperties.ContainsKey(property.Name) == false)
                        {
                            availableElementProperties[property.Name] = property;
                        }
                    }
                }
            }
        }
        /// <summary>
        /// Construct the dictionary of available properties for the elements
        /// in the pipeline.
        /// </summary>
        /// <param name="elements">
        /// Elements to get the available properties from
        /// </param>
        /// <param name="hadFailures">
        /// Flag variable to store whether there were failures during property collection.
        /// </param>
        private IReadOnlyDictionary<string, IReadOnlyDictionary<string, IElementPropertyMetaData>>
            GetElementAvailableProperties(IReadOnlyList<IFlowElement> elements, out bool hadFailures)
        {
            IDictionary<string, IDictionary<string, IElementPropertyMetaData>> dict =
                new Dictionary<string, IDictionary<string, IElementPropertyMetaData>>(
                    StringComparer.OrdinalIgnoreCase);

            hadFailures = false;
            AddAvailableProperties(elements, dict, ref hadFailures);

            return dict.Select(kvp => new KeyValuePair<string, 
                IReadOnlyDictionary<string, IElementPropertyMetaData>>(
                kvp.Key, (IReadOnlyDictionary<string, IElementPropertyMetaData>)kvp.Value))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
        }

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
        public IElementPropertyMetaData GetMetaDataForProperty(string propertyName)
        {
            IElementPropertyMetaData result = null;
            if(_metaDataByPropertyName.TryGetValue(propertyName, out result) == false)
            {
                // Get any properties that match the supplied name.
                var properties = FlowElements
                    .SelectMany(e => e.Properties)
                    .Where(p => p.Name.ToUpperInvariant() == propertyName.ToUpperInvariant());

                // If there is more than one matching property then
                // throw an exception.
                if (properties.Count() > 1)
                {
                    string message = string.Format(
                        CultureInfo.InvariantCulture,
                        Messages.ExceptionMultipleProperties,
                        propertyName,
                        string.Join(",", properties.Select(p => p.Element.GetType().Name)));
                    throw new PipelineDataException(message);
                }
                // If there are no matching properties then 
                // throw an exception.
                if (properties.Any() == false)
                {
                    string message = string.Format(
                        CultureInfo.InvariantCulture,
                        Messages.ExceptionCannotFindProperty, 
                        propertyName);
                    throw new PipelineDataException(message);
                }

                result = properties.Single();
                result = _metaDataByPropertyName.GetOrAdd(propertyName, result);
            }

            return result;
        }

        #region IDisposable Support
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
                    _logger.LogInformation($"Pipeline '{GetHashCode()}' disposed.");
                    if (_autoDisposeElements)
                    {
                        foreach (var element in _flowElements)
                        {
                            (element as IDisposable).Dispose();
                        }
                    }
                }
                else
                {
                    _logger.LogWarning($"Pipeline '{GetHashCode()}' finalized. " +
                        $"It is recommended that instance lifetimes are managed " +
                        $"explicitly with a 'using' block or calling the Dispose " +
                        $"method as part of a 'finally' block.");
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~Pipeline()
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
