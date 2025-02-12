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
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Engines.Data
{
    /// <summary>
    /// Base class for element data instances that are generated by an 
    /// <see cref="IAspectEngine"/>.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/conceptual-overview.md#aspect-data">Specification</see>
    /// </summary>
    public abstract class AspectDataBase : ElementDataBase, IAspectData
    {
        private List<Task> _processTasks = new List<Task>();
        private List<IAspectEngine> _engines;
        private bool _cacheHit;

        /// <summary>
        /// The <see cref="IMissingPropertyService"/> instance to be queried
        /// when there is no entry for a requested key.
        /// </summary>
        protected IMissingPropertyService MissingPropertyService { get; }

        /// <summary>
        /// The logger to be used by this instance.
        /// </summary>
        protected ILogger<AspectDataBase> Logger { get; }

        /// <summary>
        /// The engine that generated this data instance.
        /// </summary>
        public IReadOnlyList<IAspectEngine> Engines
        {
            get
            {
                return _engines as IReadOnlyList<IAspectEngine>;
            }
        }

        /// <inheritdoc/>
        public bool CacheHit 
        {
            get { return _cacheHit;  }
        }

        /// <summary>
        /// If the engine is configured for lazy loading, this property 
        /// returns a task that will complete once the engine has finished
        /// processing.
        /// Otherwise, it will be null.
        /// </summary>
        public Task ProcessTask
        {
            get { return Task.WhenAll(_processTasks); }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">
        /// Used for logging
        /// </param>
        /// <param name="pipeline">
        /// The <see cref="IPipeline"/> instance this element data will
        /// be associated with.
        /// </param>
        /// <param name="engine">
        /// The <see cref="IAspectEngine"/> that created this instance
        /// </param>
        public AspectDataBase(
            ILogger<AspectDataBase> logger,
            IPipeline pipeline,
            IAspectEngine engine)
            : this(logger, pipeline, engine, null)
        { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">
        /// Used for logging
        /// </param>
        /// <param name="pipeline">
        /// The <see cref="IPipeline"/> instance this element data will
        /// be associated with.
        /// </param>
        /// <param name="engine">
        /// The <see cref="IAspectEngine"/> that created this instance
        /// </param>
        /// <param name="missingPropertyService">
        /// The <see cref="IMissingPropertyService"/> to use when a requested
        /// key cannot be found.
        /// </param>
        public AspectDataBase(
            ILogger<AspectDataBase> logger,
            IPipeline pipeline,
            IAspectEngine engine,
            IMissingPropertyService missingPropertyService)
            : base (logger, pipeline)
        {
            Logger = logger;
            _engines = new List<IAspectEngine>() { engine };
            MissingPropertyService = missingPropertyService;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">
        /// Used for logging
        /// </param>
        /// <param name="pipeline">
        /// The <see cref="IPipeline"/> instance this element data will
        /// be associated with.
        /// </param>
        /// <param name="engine">
        /// The <see cref="IAspectEngine"/> that created this instance
        /// </param>
        /// <param name="missingPropertyService">
        /// The <see cref="IMissingPropertyService"/> to use when a requested
        /// key cannot be found.
        /// </param>
        /// <param name="dictionary">
        /// The dictionary instance to use internally when storing data values.
        /// </param>
        public AspectDataBase(
            ILogger<AspectDataBase> logger,
            IPipeline pipeline,
            IAspectEngine engine,
            IMissingPropertyService missingPropertyService,
            IDictionary<string, object> dictionary)
            : base(logger, pipeline, dictionary)
        {
            Logger = logger;
            _engines = new List<IAspectEngine>() { engine };
            MissingPropertyService = missingPropertyService;
        }

        internal void AddEngine(IAspectEngine engine)
        {
            lock (Engines)
            {
                _engines.Add(engine);
            }
        }

        /// <summary>
        /// Add a process task to the lazy loading tasks for this 
        /// data instance.
        /// The property accessors will only complete once all such
        /// tasks have completed.
        /// </summary>
        /// <param name="processTask"></param>
        internal void AddProcessTask(Task processTask)
        {
            lock (_processTasks)
            {
                _processTasks.Add(processTask);
            }
        }

        /// <summary>
        /// Set if this instance is a 
        /// </summary>
        internal void SetCacheHit()
        {
            _cacheHit = true;
        }

        /// <summary>
        /// get or set the specified value
        /// </summary>
        /// <param name="key">
        /// The key/name of the property to get or set.
        /// </param>
        /// <returns>
        /// The property value.
        /// </returns>
        /// <exception cref="PropertyMissingException">
        /// Thrown if there is no entry for the specified key.
        /// </exception>
        public override object this[string key]
        {
            get
            {
                return GetAs<object>(key);
            }
        }

        /// <summary>
        /// Get the value for the specified property as the specified type.
        /// </summary>
        /// <remarks>
        /// This overrides the default implementation to provide additional
        /// capabilities such as lazy loading and exposing a reason for  
        /// properties that are not present in the data.
        /// </remarks>
        /// <typeparam name="T">
        /// The type to return
        /// </typeparam>
        /// <param name="key">
        /// The key of the property to get
        /// </param>
        /// <returns>
        /// The value for the specified property.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if key is null.
        /// </exception>
        /// <exception cref="PropertyMissingException">
        /// Thrown if there is no entry matching the specified key.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if lazy loading is enabled and the cancellation token
        /// has been used to cancel processing before it was completed.
        /// </exception>
        /// <exception cref="TimeoutException">
        /// Thrown if lazy loading is enabled and processing did not
        /// complete before the configured timeout expired. 
        /// </exception>
        /// <exception cref="AggregateException">
        /// Thrown if lazy loading is enabled and multiple errors occurred 
        /// during processing.
        /// </exception>
        protected override T GetAs<T>(string key)
        {
            // Check parameter
            if (key == null) throw new ArgumentNullException(nameof(key));
            // Log the request
            if (Logger != null && Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug($"AspectData '{GetType().Name}' " +
                    $"property value requested for key '{key}'.");
            }

            // Attempt to get the property value.
            T propertyValue = default(T);

            var gotProperty = TryGetValue(key, out propertyValue);
            if (gotProperty == false)
            {
                var lazyLoadingEnabled = WaitForLazyLoad();

                if (lazyLoadingEnabled)
                {
                    gotProperty = TryGetValue(key, out propertyValue);
                }

                if (gotProperty == false &&
                    MissingPropertyService != null)
                {
                    // If there was no entry for the key then use the missing
                    // property service to find out why.
                    var missingReason = MissingPropertyService
                        .GetMissingPropertyReason(key, Engines);
                    if (Logger != null && Logger.IsEnabled(LogLevel.Warning))
                    {
                        Logger.LogWarning($"Property '{key}' missing from aspect " +
                        $"data '{GetType().Name}'. {missingReason.Reason}");
                    }
                    throw new PropertyMissingException(missingReason.Reason,
                        key, missingReason.Description);
                }
            }

            return propertyValue;
        }

        /// <inheritdoc/>
        /// <exception cref="OperationCanceledException">
        /// Thrown if lazy loading is enabled and the cancellation token
        /// has been used to cancel processing before it was completed.
        /// </exception>
        /// <exception cref="TimeoutException">
        /// Thrown if lazy loading is enabled and processing did not
        /// complete before the configured timeout expired. 
        /// </exception>
        /// <exception cref="AggregateException">
        /// Thrown if lazy loading is enabled and multiple errors occurred 
        /// during processing.
        /// </exception>
        public override IReadOnlyDictionary<string, object> AsDictionary()
        {
            WaitForLazyLoad();
            return base.AsDictionary();
        }

        /// <summary>
        /// Get the value associated with the specified key.
        /// Inheriting classes can override this method where they access
        /// data in different ways.
        /// </summary>
        /// <param name="key">
        /// The string key to retrieve the value for.
        /// </param>
        /// <param name="value">
        /// Will be populated with the value for the specified key.
        /// </param>
        /// <returns>
        /// True if the key is present in the data store, false if not.
        /// </returns>
        protected virtual bool TryGetValue<T>(string key, out T value)
        {
            object obj;
            // Very important that we call 'base.AsDictionary'
            // and not 'AsDictionary' here because calling
            // 'AsDictionary' will wait for lazy loading if it 
            // is enabled and we don't want that.
            if (base.AsDictionary().TryGetValue(key, out obj))
            {
                try
                {
                    value = (T)obj;
                }
                catch (InvalidCastException)
                {
                    throw new PipelineException(
                        $"Expected property '{key}' to be of " +
                        $"type '{typeof(T).Name}' but it is " +
                        $"'{obj.GetType().Name}'");
                }
                return true;
            }
            value = default(T);
            return false;
        }

        /// <summary>
        /// If lazy loading is configured then wait for engines to finish
        /// processing (or timeouts to be exceeded or cancellation tokens 
        /// to be triggered) before returning.
        /// </summary>
        /// <param name="key">
        /// The key of the parameter that is being accessed if any.
        /// </param>
        /// <returns>
        /// True if lazy loading is enabled, false if not.
        /// </returns>
        /// <exception cref="OperationCanceledException">
        /// Thrown if lazy loading is enabled and the cancellation token
        /// has been used to cancel processing before it was completed.
        /// </exception>
        /// <exception cref="TimeoutException">
        /// Thrown if lazy loading is enabled and processing did not
        /// complete before the configured timeout expired. 
        /// </exception>
        /// <exception cref="AggregateException">
        /// Thrown if lazy loading is enabled and multiple errors occurred 
        /// during processing.
        /// </exception>
        private bool WaitForLazyLoad(string key = null)
        {
            var lazyLoadEngines = Engines.Where(e => e != null &&
                e.LazyLoadingConfiguration != null);
            bool lazyLoadEnabled = lazyLoadEngines.Any();
            CancellationTokenSource tokenSource = null;

            if (lazyLoadEnabled == true)
            {
                var engineCancellationTokens = lazyLoadEngines
                        .Where(e => e.LazyLoadingConfiguration.CancellationToken.HasValue)
                        .Select(e => e.LazyLoadingConfiguration.CancellationToken.Value);
                tokenSource = engineCancellationTokens.Any() ?
                    CancellationTokenSource.CreateLinkedTokenSource(
                        engineCancellationTokens.ToArray()) : null;
            }

            try
            {
                IList<Exception> errors = null;

                // If lazy loading is enabled then wait for tasks to complete.
                if (lazyLoadEnabled == true &&
                    (errors = WaitOnAllProcessTasks(
                        lazyLoadEngines.Max(e => e.LazyLoadingConfiguration.PropertyTimeoutMs),
                        tokenSource?.Token)).Count > 0)
                {
                    var itemText = string.IsNullOrEmpty(key) ? "data" : $"property '{key}'";
                    var enginesText = string.Join(", ", Engines.Select(i => i.GetType().Name).Distinct());

                    Exception e = null;
                    if (errors.Count == 1)
                    {
                        e = errors[0];
                        if (e is OperationCanceledException)
                        {
                            // The property is being lazy loaded but 
                            // been canceled, so pass the exception up.
                            throw (OperationCanceledException)e;
                        }
                        else if (e is TimeoutException)
                        {
                            // The property is being lazy loaded but has 
                            // timed out or been canceled so throw the 
                            // appropriate exception.
                            var msg = string.Format(
                                CultureInfo.InvariantCulture,
                                Messages.ExceptionProcessingTimeout,
                                itemText, enginesText);
                            throw new TimeoutException(msg, e);
                        }
                        else
                        {
                            // The property is being lazy loaded but an error
                            // occurred in the engine's process method
                            var msg = string.Format(
                                CultureInfo.InvariantCulture,
                                Messages.ExceptionProcessingError,
                                itemText, enginesText);
                            throw new PipelineException(msg, e);
                        }
                    }
                    else
                    {
                        // The property is being lazy loaded but multiple 
                        // errors have occurred in the engine's process method.
                        var msg = string.Format(
                            CultureInfo.InvariantCulture,
                            Messages.ExceptionProcessingMultipleErrors,
                            itemText, enginesText);
                        throw new AggregateException(msg, errors.ToArray());
                    }
                }
            }
            finally
            {
                // Make sure we dispose of the cancellation token source.
                tokenSource?.Dispose();
            }

            return lazyLoadEnabled;
        }


        /// <summary>
        /// Waits for the completion of all process tasks which must complete
        /// before fetching a property value. Any exceptions which are thrown
        /// by a task are returned as a list.
        /// </summary>
        /// <param name="timeoutMillis">timeout for each task</param>
        /// <param name="token">cancellation token for the tasks</param>
        /// <returns>list of exceptions that occurred</returns>
        private IList<Exception> WaitOnAllProcessTasks(
            int timeoutMillis,
            CancellationToken? token)
        {
            IList<Exception> errors = new List<Exception>();

            foreach (var task in _processTasks)
            {
                try
                {
                    bool taskCompleted = false;
                    if(token == null)
                    {
                        taskCompleted = task.Wait(timeoutMillis);
                    }
                    else
                    {
                        taskCompleted = task.Wait(timeoutMillis, token.Value);
                    }

                    if (taskCompleted == false)
                    {
                        if (token.Value.IsCancellationRequested)
                        {
                            // The property is being lazy loaded but 
                            // been canceled, so pass the exception up.
                            errors.Add(new OperationCanceledException());
                        }
                        else
                        {
                            // The property is being lazy loaded but 
                            // has timed out or been canceled so throw 
                            // the appropriate exception.
                            errors.Add(new TimeoutException());
                        }
                    }
                }
                catch (AggregateException e)
                {
                    errors.Add(e);
                }
                catch (OperationCanceledException e)
                {
                    errors.Add(e);
                }
            }

            return errors;
        }
    }
}
