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

using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Core.TypedMap;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

[assembly: InternalsVisibleTo("FiftyOne.Pipeline.Web.Tests")]
[assembly: InternalsVisibleTo("FiftyOne.Pipeline.Core.Tests")]

namespace FiftyOne.Pipeline.Core.Data
{
    /// <summary>
    /// The FlowData class contains the data that is used within a pipeline.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/conceptual-overview.md#flow-data">Specification</see>
    /// </summary>
    public class FlowData : IFlowData
    {
        /// <summary>
        /// The input data
        /// </summary>
        private Evidence _evidence;

        /// <summary>
        /// The output data
        /// </summary>
        private ITypedKeyMap _data;

        /// <summary>
        /// The errors that have occurred during processing
        /// </summary>
        private List<IFlowError> _errors;

        /// <summary>
        /// True if this instance has been processed
        /// </summary>
        private bool _processed = false;

        /// <summary>
        /// Logger.
        /// </summary>
        private ILogger<FlowData> _logger;

        /// <summary>
        /// Lock to use when adding element data.
        /// </summary>
        private object _dataLock = new object();

        /// <summary>
        /// The errors that have occurred during processing
        /// </summary>
        public IList<IFlowError> Errors
        {
            get { return _errors; }
        }

        /// <summary>
        /// Lock to use when adding errors.
        /// </summary>
        private object _errorsLock;
        private bool disposedValue;

        /// <summary>
        /// The pipeline that was used to create this FlowData instance
        /// </summary>
        internal IPipelineInternal PipelineInternal { get; private set; }

        /// <summary>
        /// The pipeline that was used to create this FlowData instance
        /// </summary>
        public IPipeline Pipeline { get { return PipelineInternal; } }

        /// <summary>
        /// A boolean flag that can be used to stop further elements
        /// from executing.
        /// </summary>
        public bool Stop { get; set; }


        /// <summary>
        /// Get a filter that will only include the evidence keys that can 
        /// be used by the elements within the pipeline that created this
        /// flow element.
        /// </summary>
        public IEvidenceKeyFilter EvidenceKeyFilter
        {
            get
            {
                return PipelineInternal.EvidenceKeyFilter;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">
        /// The logger to use when events occur. Can be null.
        /// </param>
        /// <param name="pipeline">
        /// The pipeline that created this FlowData instance.
        /// </param>
        /// <param name="evidence">
        /// The initial evidence.
        /// </param>
        internal FlowData(
            ILogger<FlowData> logger,
            IPipelineInternal pipeline,
            Evidence evidence)
        {
            _logger = logger;
            PipelineInternal = pipeline;
            _data = new TypedKeyMap(pipeline?.IsConcurrent ?? false);
            _evidence = evidence;

            if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug($"FlowData '{GetHashCode()}' created.");
            }
        }

        /// <summary>
        /// Register an error that occurred while working with this 
        /// instance.
        /// </summary>
        /// <param name="ex">
        /// The exception that occurred.
        /// </param>
        /// <param name="flowElement">
        /// The flow element that the exception occurred in.
        /// </param>
        public void AddError(Exception ex, IFlowElement flowElement)
        {
            AddError(ex, flowElement, true, true);
        }

        /// <summary>
        /// Register an error that occurred while working with this 
        /// instance.
        /// </summary>
        /// <param name="ex">
        /// The exception that occurred.
        /// </param>
        /// <param name="flowElement">
        /// The flow element that the exception occurred in.
        /// </param>
        /// <param name="shouldThrow">
        /// Set whether the pipeline should throw the exception.
        /// </param>
        /// <param name="shouldLog">
        /// Set whether the pipeline should log the exception as an error.
        /// </param>
        public void AddError(Exception ex, IFlowElement flowElement, bool shouldThrow, bool shouldLog)
        {
            if (_errors == null) { _errors = new List<IFlowError>(); }
            if (_errorsLock == null) { _errorsLock = new object(); }
            var error = new FlowError(ex, flowElement, shouldThrow);
            lock (_errorsLock)
            {
                _errors.Add(error);
            }

            if (_logger != null && _logger.IsEnabled(LogLevel.Error) && shouldLog)
            {
                string logMessage = "Error occurred during processing";
                if (flowElement != null)
                {
                    logMessage = logMessage + $" of {flowElement.GetType().Name}";
                }
                _logger.LogError(ex, logMessage);
            }            
        }

        /// <summary>
        /// Get the <see cref="IEvidence"/> object that contains the 
        /// input data for this instance.
        /// </summary>
        /// <returns></returns>
        public IEvidence GetEvidence()
        {
            return _evidence;
        }

        /// <summary>
        /// Try to get the data value from evidence.
        /// </summary>
        /// <param name="key">The evidence key.</param>
        /// <param name="value">The value from evidence.</param>
        /// <returns>True if a value for a given key is found or false if the 
        /// key is not found or if the method cannot cast the value to the 
        /// requested type.</returns>
        public bool TryGetEvidence<T>(string key, out T value)
        {
            var dict = _evidence.AsDictionary();
            bool gotValue = dict.TryGetValue(key, out object objValue);
            if (gotValue &&
                objValue is T tempValue)
            {
                value = tempValue;
            } 
            else
            {
                gotValue = false;
                value = default;
            }

            return gotValue;
        }

        /// <summary>
        /// Get the string keys to the aspects that are contained within
        /// the output data.
        /// </summary>
        /// <returns></returns>
        public IList<string> GetDataKeys()
        {
            return _data.GetKeys().ToList();
        }

        /// <summary>
        /// Get all element data AspectPropertyValues that match the specified predicate
        /// </summary>
        /// <param name="predicate">
        /// If a property passed to this function returns true then it will
        /// be included in the results
        /// </param>
        /// <returns>
        /// All the element data AspectPropertyValues that match the predicate
        /// </returns>
        /// <exception cref="PipelineException">
        /// Thrown if this FlowData instance has not been processed yet.
        /// </exception>
        public IEnumerable<KeyValuePair<string, object>> GetWhere(
            Func<IElementPropertyMetaData, bool> predicate)
        {
            var data = _data.AsStringKeyDictionary();

            foreach (var element in PipelineInternal.FlowElements
                .Where(e => data.ContainsKey(e.ElementDataKey)))
            {
                var elementData = data[element.ElementDataKey] as IElementData;
                if (elementData != null)
                {
                    var elementDataDictionary = elementData.AsDictionary();

                    foreach (var property in
                        element.Properties.Where(predicate).Where(i =>
                            i.Available &&
                            // Check if the property is actually present in 
                            // the element data before trying to access it.
                            elementDataDictionary.ContainsKey(i.Name)))
                    {
#pragma warning disable CA1308 // Normalize strings to uppercase
                        // Pipeline specification is for keys to be lower-case.      
                        yield return new KeyValuePair<string, object>(
                            element.ElementDataKey + "." + property.Name.ToLowerInvariant(),
                            elementDataDictionary[property.Name.ToLowerInvariant()]);
#pragma warning restore CA1308 // Normalize strings to uppercase
                    }
                }
            }
        }

        /// <summary>
        /// Use the pipeline to process this FlowData instance and 
        /// populate the aspect data values.
        /// </summary>
        /// <exception cref="PipelineException">
        /// Thrown if this flow data object has already been processed.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the Pipeline has already been disposed.
        /// </exception>
        public void Process() => Process(default);

        /// <summary>
        /// Use the pipeline to process this FlowData instance and 
        /// populate the aspect data values.
        /// </summary>
        /// <exception cref="PipelineException">
        /// Thrown if this flow data object has already been processed.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the Pipeline has already been disposed.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="cancellationToken"/> tripped
        /// before execution started.
        /// </exception>
        /// <param name="cancellationToken">
        /// Token to cancel processing.
        /// Can be retrieved by flow elements from
        /// <see cref="ProcessingCancellationToken"/>.
        /// </param>
        public void Process(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_processed)
            {
                throw new PipelineException(Messages.ExceptionFlowDataAlreadyProcessed);
            }
            _processed = true;
            ProcessingCancellationToken = cancellationToken;
            PipelineInternal.Process(this);
        }

        /// <summary>
        /// Token passed into <see cref="Process(CancellationToken)"/>.
        /// </summary>
        public CancellationToken ProcessingCancellationToken { get; private set; }

        /// <summary>
        /// Add the specified evidence to the FlowData
        /// </summary>
        /// <param name="key">
        /// The evidence key
        /// </param>
        /// <param name="value">
        /// The evidence value
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if parameters are null
        /// </exception>
        public IFlowData AddEvidence(string key, object value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug($"FlowData '{GetHashCode()}' set evidence " +
                    $"'{key}' to '{value.ToString()}'.");
            }
            _evidence[key] = value;
            return this;
        }

        /// <summary>
        /// Add the specified evidence to the FlowData
        /// </summary>
        /// <param name="evidence">
        /// The evidence to add
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if evidence is null
        /// </exception>
        public IFlowData AddEvidence(IDictionary<string, object> evidence)
        {
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            var log = _logger.IsEnabled(LogLevel.Debug);
            foreach (var entry in evidence)
            {
                if (log)
                {
                    _logger.LogDebug($"FlowData '{GetHashCode()}' set evidence " +
                        $"'{entry.Key}' to '{entry.Value.ToString()}'.");
                }
                _evidence[entry.Key] = entry.Value;
            }
            return this;
        }

        /// <summary>
        /// Get the <see cref="IElementData"/> instance containing data
        /// populated by the specified element.
        /// </summary>
        /// <param name="elementDataKey">
        /// The name of the element to get data from.
        /// </param>
        /// <returns>
        /// An <see cref="IElementData"/> instance containing the data.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied data key is null
        /// </exception>
        /// <exception cref="PipelineException">
        /// Thrown if this FlowData instance has not been processed yet.
        /// </exception>
        public IElementData Get(string elementDataKey)
        {
            if (_processed == false)
            {
                throw new PipelineException(Messages.ExceptionFlowDataNotYetProcessed);
            }
            if (elementDataKey == null)
            {
                throw new ArgumentNullException(nameof(elementDataKey));
            }
            return _data.AsStringKeyDictionary()[elementDataKey] as IElementData;
        }

        /// <summary>
        /// Get the <see cref="IElementData"/> instance containing data
        /// populated by the specified element.
        /// </summary>
        /// <typeparam name="T">
        /// The expected type of the data to be returned.
        /// </typeparam>
        /// <param name="key">
        /// An <see cref="ITypedKey{T}"/> indicating the element 
        /// to get data from.
        /// </param>
        /// <returns>
        /// An instance of type T containing the data.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied key is null
        /// </exception>
        /// <exception cref="PipelineException">
        /// Thrown if this FlowData instance has not been processed yet.
        /// </exception>
        public T Get<T>(ITypedKey<T> key) where T : IElementData
        {
            if (_processed == false)
            {
                throw new PipelineException(Messages.ExceptionFlowDataNotYetProcessed);
            }
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            return _data.Get(key);
        }

        /// <summary>
        /// Check if the flow data contains an item with the specified
        /// key name and type. If it does exist, retrieve it.
        /// </summary>
        /// <param name="key">
        /// The key to check for.
        /// </param>
        /// <param name="value">
        /// The value associated with the key.
        /// </param>
        /// <returns>
        /// True if an entry matching the key exists, false otherwise.
        /// </returns>
        public bool TryGetValue<T>(ITypedKey<T> key, out T value)
             where T : IElementData
        {
            return _data.TryGetValue(key, out value);
        }

        /// <summary>
        /// Get the <see cref="IElementData"/> instance containing data
        /// of the specified type. If multiple instances of the type
        /// exist then an exception is thrown.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the data to look for and return.
        /// </typeparam>
        /// <returns>
        /// The data instance.
        /// </returns>
        public T Get<T>() where T : IElementData
        {
            return _data.Get<T>();
        }

        /// <summary>
        /// Get the <see cref="IElementData"/> instance containing data
        /// populated by the specified element.
        /// </summary>
        /// <typeparam name="T">
        /// The expected type of the data to be returned.
        /// </typeparam>    
        /// <typeparam name="TMeta">
        /// The type of meta data that the flow element will supply 
        /// about the properties it populates.
        /// </typeparam>
        /// <param name="flowElement">
        /// The <see cref="IFlowElement{T, TMeta}"/> that populated the
        /// desired data. 
        /// </param>
        /// <returns>
        /// An instance of type T containing the data.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied flow element is null
        /// </exception>
        /// <exception cref="PipelineException">
        /// Thrown if this FlowData instance has not been processed yet.
        /// </exception>
        public T GetFromElement<T, TMeta>(IFlowElement<T, TMeta> flowElement)
            where T : IElementData
            where TMeta : IElementPropertyMetaData
        {
            if (_processed == false)
            {
                throw new PipelineException(Messages.ExceptionFlowDataNotYetProcessed);
            }
            if (flowElement == null)
            {
                throw new ArgumentNullException(nameof(flowElement));
            }
            return _data.Get(flowElement.ElementDataKeyTyped);
        }

        /// <summary>
        /// Get the specified property as the specified type.
        /// </summary>
        /// <typeparam name="T">
        /// The type to return the property value as
        /// </typeparam>
        /// <param name="propertyName">
        /// The name of the property to get
        /// </param>
        /// <returns>
        /// The property value
        /// </returns>
        /// <exception cref="PipelineException">
        /// Thrown if this FlowData instance has not been processed yet.
        /// </exception>
        /// <exception cref="PipelineDataException">
        /// Thrown if the requested property cannot be found or if multiple
        /// flow elements use the same property name.
        /// </exception>
        /// <exception cref="InvalidCastException">
        /// Thrown if property value cannot be cast to the requested type.
        /// </exception>
        /// <exception cref="KeyNotFoundException">
        /// Thrown if property value is expected to be present but was not
        /// found.
        /// </exception>
        public T GetAs<T>(string propertyName)
        {
            T result;

            // Throw an error if the instance has not yet been processed.
            if(_processed == false)
            {
                _logger.LogError(Messages.ExceptionFlowDataNotYetProcessed);
                throw new PipelineException(Messages.ExceptionFlowDataNotYetProcessed);
            }

            var property = PipelineInternal.GetMetaDataForProperty(propertyName);


            // Attempt to cast the property value object to the desired type.
            // If this fails then log an error message and throw an exception.
            try
            {
                result = (T)Get(property.Element.ElementDataKey)[property.Name];
            }
            catch (InvalidCastException)
            {
                string message = $"Failed to cast property '{propertyName}'" +
                    $" to '{typeof(T).Name}'. " +
                    $"Expected property type is '{property.Type.Name}'";
                _logger.LogError(message);
                throw;
            }
            catch (KeyNotFoundException)
            {
                string message = $"Property '{propertyName}' was not in " +
                    $"the data from '{property.Element.GetType().Name}' " +
                    $"as expected.";
                _logger.LogError(message);
                throw;
            }

            return result;
        }

        /// <summary>
        /// Get the specified property as a boolean.
        /// </summary>
        /// <param name="propertyName">
        /// The name of the property to get
        /// </param>
        /// <returns>
        /// The property value
        /// </returns>
        public bool GetAsBool(string propertyName) { return GetAs<bool>(propertyName); }

        /// <summary>
        /// Get the specified property as a string.
        /// </summary>
        /// <param name="propertyName">
        /// The name of the property to get
        /// </param>
        /// <returns>
        /// The property value
        /// </returns>
        public string GetAsString(string propertyName) { return GetAs<string>(propertyName); }

        /// <summary>
        /// Get the specified property as a int.
        /// </summary>
        /// <param name="propertyName">
        /// The name of the property to get
        /// </param>
        /// <returns>
        /// The property value
        /// </returns>
        public int GetAsInt(string propertyName) { return GetAs<int>(propertyName); }

        /// <summary>
        /// Get the specified property as a long.
        /// </summary>
        /// <param name="propertyName">
        /// The name of the property to get
        /// </param>
        /// <returns>
        /// The property value
        /// </returns>
        public long GetAsLong(string propertyName) { return GetAs<long>(propertyName); }

        /// <summary>
        /// Get the specified property as a float.
        /// </summary>
        /// <param name="propertyName">
        /// The name of the property to get
        /// </param>
        /// <returns>
        /// The property value
        /// </returns>
        public float GetAsFloat(string propertyName) { return GetAs<float>(propertyName); }

        /// <summary>
        /// Get the specified property as a double.
        /// </summary>
        /// <param name="propertyName">
        /// The name of the property to get
        /// </param>
        /// <returns>
        /// The property value
        /// </returns>
        public double GetAsDouble(string propertyName) { return GetAs<double>(propertyName); }

        /// <summary>
        /// Get or add the specified element data to the internal map.
        /// </summary>
        /// <param name="elementDataKey">
        /// The name of the element to store the data under.
        /// </param>
        /// <param name="dataFactory">
        /// The method to use to create a new data to store if one does not
        /// already exist.
        /// </param>
        /// <returns>
        /// Existing data matching the key, or newly added data.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied data factory is null
        /// </exception>
        /// <exception cref="InvalidCastException">
        /// Thrown if the <see cref="IElementData"/> retrieved for the 
        /// supplied data key cannot be cast to the type T
        /// </exception>
        public T GetOrAdd<T>(string elementDataKey, Func<IPipeline, T> dataFactory)
            where T : IElementData
        {
            if (dataFactory == null)
            {
                throw new ArgumentNullException(nameof(dataFactory));
            }

            T result = default(T);
            object data;
            if (_data.AsStringKeyDictionary().TryGetValue(elementDataKey, out data) == false)
            {
                if (Pipeline.IsConcurrent == true)
                {
                    lock (_dataLock)
                    {
                        if (_data.AsStringKeyDictionary().TryGetValue(elementDataKey, out data) == false)
                        {
                            data = dataFactory(Pipeline);
                            _data.AsStringKeyDictionary().Add(elementDataKey, data);
                        }
                    }
                }
                else
                {
                    data = dataFactory(Pipeline);
                    _data.AsStringKeyDictionary().Add(elementDataKey, data);
                }
            }
            try
            {
                result = (T)data;
            }
            catch (InvalidCastException)
            {
                string message = $"Failed to cast data '{elementDataKey}'" +
                    $" to '{typeof(T).Name}'. " +
                    $"Expected data type is '{data.GetType().Name}'";
                _logger.LogError(message);
                throw;
            }
            return result;
        }

        /// <summary>
        /// Add the specified element data to the internal map.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the data being stored.
        /// </typeparam>
        /// <param name="key">
        /// The key to use when storing the data.
        /// </param>
        /// <param name="dataFactory">
        /// The method to use to create a new data to store if one does not
        /// already exist.
        /// </param>
        /// <returns>
        /// Existing data matching the key, or newly added data.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied data factory is null
        /// </exception>
        public T GetOrAdd<T>(ITypedKey<T> key, Func<IPipeline, T> dataFactory)
             where T : IElementData
        {
            if (dataFactory == null)
            {
                throw new ArgumentNullException(nameof(dataFactory));
            }

            T data = default(T);
            if (_data.TryGetValue(key, out data) == false)
            {
                if (Pipeline.IsConcurrent == true)
                {
                    lock (_dataLock)
                    {
                        if (_data.TryGetValue(key, out data) == false)
                        {
                            data = dataFactory(Pipeline);
                            _data.Add(key, data);
                        }
                    }
                }
                else
                {
                    data = dataFactory(Pipeline);
                    _data.Add(key, data);
                }
            }
            return data;
        }

        /// <summary>
        /// Get the element data for this instance as a dictionary.
        /// </summary>
        /// <returns>
        /// A dictionary containing the element data.
        /// </returns>
        public IDictionary<string, object> ElementDataAsDictionary()
        {
            return _data.AsStringKeyDictionary();
        }

        /// <summary>
        /// Get the element data for this instance as an enumerable.
        /// </summary>
        /// <returns>
        /// An enumerable containing the element data.
        /// </returns>
        public IEnumerable<IElementData> ElementDataAsEnumerable()
        {
            foreach (var key in _data.GetKeys())
            {
                yield return Get(key);
            }
        }

        /// <summary>
        /// Generate a <see cref="DataKey"/> that contains the evidence 
        /// data from this instance that matches the specified filter.
        /// </summary>
        /// <param name="filter">
        /// An <see cref="IEvidenceKeyFilter"/> instance that defines the 
        /// values to include in the generated key.
        /// </param>
        /// <returns>
        /// A new <see cref="DataKey"/> instance.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied filter is null
        /// </exception>
        public DataKey GenerateKey(IEvidenceKeyFilter filter)
        {
            if(filter == null)
            {
                throw new ArgumentNullException(nameof(filter));
            }

            var evidence = _evidence.AsDictionary();
            // We could use DataKeyBuilder here but instead use Linq
            // directly for better performance.
            return new DataKey(evidence
                .Where(e => filter.Include(e.Key))
                .OrderBy(e => filter.Order(e.Key))
                    .ThenBy(e => e.Key)
                .Select(e => e.Value)
                .ToList());
        }

        /// <summary>
        /// Dispose
        /// </summary>
        /// <param name="disposing">
        /// False if called from finalizer
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug($"Dispose called on Flow data {this.GetHashCode()}.");
            }
            if (!disposedValue)
            {
                if (disposing)
                {
                    foreach (var elementData in _data.AsStringKeyDictionary().Values)
                    {
                        if (typeof(IDisposable).IsAssignableFrom(elementData.GetType()))
                        {
                            ((IDisposable)elementData).Dispose();
                        }
                    }
                }

                disposedValue = true;
            }
        }
        
        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
