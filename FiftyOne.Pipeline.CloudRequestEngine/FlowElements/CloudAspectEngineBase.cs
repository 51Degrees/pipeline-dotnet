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

using FiftyOne.Pipeline.CloudRequestEngine.Data;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Data.Types;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FiftyOne.Pipeline.CloudRequestEngine.FlowElements
{
    /// <summary>
    /// Base class for 51Degrees cloud aspect engines.   
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/pipeline-elements/cloud-aspect-engine.md">Specification</see> 
    /// </summary>
    /// <typeparam name="T">
    /// The type of data that the engine will return. Must implement 
    /// <see cref="IAspectData"/>.
    /// </typeparam>
    public abstract class CloudAspectEngineBase<T> : 
        AspectEngineBase<T, IAspectPropertyMetaData>, 
        ICloudAspectEngine
        where T : IAspectData
    {
        /// <summary>
        /// Internal class that is used to retrieve the
        /// <see cref="CloudRequestEngine"/> instance that will be making
        /// requests on behalf of this engine.
        /// </summary>
        protected class RequestEngineAccessor
        {
            private ICloudRequestEngine _cloudRequestEngine = null;
            private object _cloudRequestEngineLock = new object();
            private IFlowElement _currentElement;
            private Func<IReadOnlyList<IPipeline>> _pipelinesAccessor;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="pipelinesAccessor">
            /// A function that returns the list of pipelines associated 
            /// with the parent engine.
            /// </param>
            /// <param name="currentElement">
            /// The <see cref="IFlowElement"/> this instance of 
            /// RequestEngineAccessor was created by.
            /// </param>
            public RequestEngineAccessor(
                Func<IReadOnlyList<IPipeline>> pipelinesAccessor,
                IFlowElement currentElement)
            {
                _currentElement = currentElement;
                _pipelinesAccessor = pipelinesAccessor;
            }


            /// <summary>
            /// Get the <see cref="CloudRequestEngine"/> that will be making
            /// requests on behalf of this engine.
            /// </summary>
            /// 
            [Obsolete("Use the 'GetInstance' method instead. " +
                "This property will be removed in a future version.")]
#pragma warning disable CA1721 // Property names should not match get methods
            // Set as obsolete and Will be removed in a future version.
            public ICloudRequestEngine Instance => GetInstance();
#pragma warning restore CA1721 // Property names should not match get methods

            /// <summary>
            /// Get the <see cref="CloudRequestEngine"/> that will be making
            /// requests on behalf of this engine.
            /// </summary>
            public ICloudRequestEngine GetInstance()
            {
                if (_cloudRequestEngine == null)
                {
                    lock (_cloudRequestEngineLock)
                    {
                        if (_cloudRequestEngine == null)
                        {
                            if (_pipelinesAccessor().Count > 1)
                            {
                                throw new PipelineConfigurationException(
                                    $"'{_currentElement.GetType().Name}' does not support being " +
                                    $"added to multiple Pipelines.");
                            }
                            if (_pipelinesAccessor().Count == 0)
                            {
                                throw new PipelineConfigurationException(
                                    $"'{_currentElement.GetType().Name}' has not yet been added " +
                                    $"to a Pipeline.");
                            }

                            _cloudRequestEngine = _pipelinesAccessor()[0].GetElement<ICloudRequestEngine>();

                            if (_cloudRequestEngine == null)
                            {
                                throw new PipelineConfigurationException(
                                    $"The '{_currentElement.GetType().Name}' requires a 'CloudRequestEngine' " +
                                    $"before it in the Pipeline. This engine will be unable " +
                                    $"to produce results until this is corrected.");
                            }
                        }
                    }
                }
                return _cloudRequestEngine;
            }
        }

        private IList<IAspectPropertyMetaData> _aspectProperties;
        private object _aspectPropertiesLock = new object();
        private string _dataSourceTier;

        /// <summary>
        /// The 'tier' of the source data used to service this request.
        /// For 51Degrees cloud, this means the name for the set of
        /// properties that are accessible to the license key(s) 
        /// associated with the resource key that is used.
        /// </summary>
        public override string DataSourceTier => _dataSourceTier;

        /// <summary>
        /// Used to access the <see cref="CloudRequestEngine"/> that will
        /// be making requests on behalf of this engine.
        /// </summary>
        protected RequestEngineAccessor RequestEngine { get; set; }


        /// <summary>
        /// Get property meta-data for properties populated by this engine
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", 
            "CA1065:Do not raise exceptions in unexpected locations", 
            Justification = "")]
        public override IList<IAspectPropertyMetaData> Properties
        {
            get
            {
                if (_aspectProperties == null)
                {
                    lock (_aspectPropertiesLock)
                    {
                        if (_aspectProperties == null)
                        {
                            var engineInstance = RequestEngine.GetInstance();
                            try
                            {
                                if (LoadAspectProperties(engineInstance) == false)
                                {
                                    throw new PipelineException(string.Format(
                                        CultureInfo.InvariantCulture,
                                        Messages.ExceptionFailedToLoadProperties,
                                        ElementDataKey));
                                }
                            }
                            catch (CloudRequestException ex)
                            {
                                throw new PropertiesNotYetLoadedException("Failed to request properties from the cloud.", ex);
                            }
                        }
                    }
                }
                return _aspectProperties;
            }
        }

        /// <summary>
        /// Provide an implementation for the non-generic, 
        /// aspect-specific version of the meta-data property.
        /// </summary>
        public override bool HasLoadedProperties { get { return _aspectProperties != null; } }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">
        /// The logger used by this instance.
        /// </param>
        /// <param name="aspectDataFactory">
        /// The factory function to use when creating new data instances
        /// of type <code>T</code>.
        /// </param>
        public CloudAspectEngineBase(ILogger<AspectEngineBase<T, IAspectPropertyMetaData>> logger, 
            Func<IPipeline, FlowElementBase<T, IAspectPropertyMetaData>, T> aspectDataFactory) : base(logger, aspectDataFactory)
        {
            RequestEngine = new RequestEngineAccessor(() => Pipelines, this);
        }
        
        /// <summary>
        /// Cleanup any unmanaged resources.
        /// </summary>
        protected override void UnmanagedResourcesCleanup()
        {
        }

        /// <summary>
        /// Get property meta data from the <see cref="CloudRequestEngine"/> 
        /// for properties relating to this engine instance.
        /// This method will populate the <see cref="_aspectProperties"/>
        /// field.
        /// </summary>
        /// <remarks>
        /// There will be one <see cref="CloudRequestEngine"/> in a
        /// Pipeline that makes the actual web requests to the cloud service.
        /// One or more cloud aspect engines will take the response from these
        /// cloud requests and convert them into strongly typed objects.
        /// Given this model, the cloud aspect engines have no knowledge
        /// of which properties the <see cref="CloudRequestEngine"/> can
        /// return.
        /// They must extract the properties relevant to them from the
        /// meta-data for all properties that the 
        /// <see cref="CloudRequestEngine"/> exposes.
        /// </remarks>
        /// <param name="engine">
        /// The <see cref="CloudRequestEngine"/> from which to retrieve
        /// property meta-data.
        /// </param>
        /// <returns>
        /// True if the _aspectProperties has been successfully populated
        /// with the relevant property meta-data.
        /// False if something has gone wrong.
        /// </returns>
        private bool LoadAspectProperties(ICloudRequestEngine engine)
        {
            var dictionary = engine.PublicProperties;

            if (dictionary != null &&
                dictionary.Count > 0 &&
                dictionary.ContainsKey(ElementDataKey))
            {
                _aspectProperties = new List<IAspectPropertyMetaData>();
                _dataSourceTier = dictionary[ElementDataKey].DataTier;

                foreach (var item in dictionary[ElementDataKey].Properties)
                {
                    var property = LoadProperty(item);
                    if (property != null)
                    {
                        _aspectProperties.Add(property);
                    }
                }

                return _aspectProperties.Count > 0;
            }
            else
            {
                Logger.LogError($"Aspect properties could not be " +
                    $"loaded for {GetType().Name} - '{ElementDataKey}'", this);
                return false;
            }
        }

        /// <summary>
        /// Translate the specified property from <see cref="PropertyMetaData"/>
        /// to <see cref="AspectPropertyMetaData"/>.
        /// </summary>
        /// <param name="property">
        /// The <see cref="PropertyMetaData"/> instance to translate.
        /// </param>
        /// <param name="parentObjectType">
        /// The type of the object on which this property exists.
        /// </param>
        /// <returns>
        /// A new <see cref="AspectPropertyMetaData"/> instance, created from
        /// the values in the supplied <see cref="PropertyMetaData"/>.
        /// </returns>
        private AspectPropertyMetaData LoadProperty(PropertyMetaData property, Type parentObjectType = null)
        {
            // If parent object type is not set then use the type of the
            // data returned by this engine.
            if(parentObjectType == null)
            {
                parentObjectType = typeof(T);
            }
            // Get the property info for this property based on the 
            // supplied name.
            var propertyType = GetPropertyType(property, parentObjectType);


            // Load any sub properties.
            List<AspectPropertyMetaData> subProperties = null;
            if (property.ItemProperties != null &&
                property.ItemProperties.Count > 0)
            {
                subProperties = new List<AspectPropertyMetaData>();
                if (typeof(IEnumerable).IsAssignableFrom(propertyType) &&
                    propertyType.IsGenericType)
                {
                    // Get the type of the items in this list so
                    // LoadProperty can use reflection to get its
                    // properties.
                    var itemType = propertyType.GetGenericArguments()[0];
                    foreach (var subproperty in property.ItemProperties)
                    {
                        var newProperty = LoadProperty(subproperty, itemType);
                        if (newProperty != null)
                        {
                            subProperties.Add(newProperty);
                        }
                    }
                }
                else
                {
                    Logger.LogWarning($"Problem parsing sub-items. " +
                        $"Property '{parentObjectType.Name}.{property.Name}' " +
                        $"does not implement IEnumerable<>.");
                }
            }

            // Create the AspectPropertyMetaData instance.
            return new AspectPropertyMetaData(this,
                property.Name,
                propertyType,
                property.Category,
                new List<string>(),
                true,
                "",
                subProperties,
                property.DelayExecution,
                property.EvidenceProperties);
        }

        /// <summary>
        /// Retrieve the raw JSON response from the 
        /// <see cref="CloudRequestEngine"/> in this pipeline, extract 
        /// the data for this specific engine and populate the 
        /// <code>TData</code> instance accordingly.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> to get the raw JSON data from.
        /// </param>
        /// <param name="aspectData">
        /// The <code>TData</code> instance to populate with values.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if a required parameter is null.
        /// </exception>
        protected override void ProcessEngine(IFlowData data, T aspectData) {
            if (data == null) throw new ArgumentNullException(nameof(data));

            CloudRequestData requestData;
            // Get requestData from CloudRequestEngine. If requestData does not
            // exist in the element data TypedKeyMap then the engine either 
            // does not exist in the Pipeline or is not run before this engine.
            try
            {
                requestData = data.GetFromElement(RequestEngine.GetInstance());
            } 
            catch (KeyNotFoundException ex) 
            {
                throw new PipelineConfigurationException(
                    $"The '{GetType().Name}' requires a 'CloudRequestEngine' " +
                    $"before it in the Pipeline. This engine will be unable " +
                    $"to produce results until this is corrected.", ex);
            }
            
            // Check the requestData ProcessStarted flag which informs whether
            // the cloud request engine process method was called.
            if (requestData?.ProcessStarted == false)
            {
                throw new PipelineConfigurationException(
                    $"The '{GetType().Name}' requires a 'CloudRequestEngine' " +
                    $"before it in the Pipeline. This engine will be unable " +
                    $"to produce results until this is corrected.");
            }

            var json = requestData?.JsonResponse;

            // If the JSON is empty or null then do not Process the CloudAspectEngine.
            // Empty or null JSON indicates that an error has occurred in the 
            // CloudRequestEngine. The error will have been reported by the 
            // CloudRequestEngine so just log a warning that this 
            // CloudAspectEngine did not process.
            if (string.IsNullOrEmpty(json) == false)
            {
                ProcessCloudEngine(data, aspectData, json);
            }
            else
            {
                Logger.LogInformation($"The '{GetType().Name}' did not process " +
                    $"as the JSON response from the CloudRequestEngine was null " +
                    $"or empty. Please refer to errors generated by the " +
                    $"CloudRequestEngine in the logs as this indicates an error " +
                    $"occurred there.");
            }
        }

        /// <summary>
        /// A virtual method to be implemented by the derived class which
        /// uses the JsonResponse from the CloudRequestEngine to populate the 
        /// <code>T</code> instance accordingly.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> instance.
        /// </param>
        /// <param name="aspectData">
        /// The <code>TData</code> instance to populate with values.
        /// </param>
        /// <param name="json">
        /// The JSON response from the <see cref="CloudRequestEngine"/>
        /// </param>
        protected virtual void ProcessCloudEngine(IFlowData data, T aspectData, string json)
        {
            throw new NotImplementedException(Messages.ProcessCloudEngineNotImplemented);
        }

        /// <summary>
        /// Use the supplied cloud data to create a dictionary of 
        /// <see cref="AspectPropertyValue{T}"/> instances.
        /// </summary>
        /// <remarks>
        /// This method uses the meta-data exposed by the 
        /// <see cref="Properties"/> collection to determine
        /// if a given entry in the supplied cloudData should
        /// be converted to an <see cref="AspectPropertyValue{T}"/>
        /// or not.
        /// If not it will be output unchanged. If it is then a new
        /// <see cref="AspectPropertyValue{T}"/> instance will be created 
        /// and the value from the cloud data assigned to it.
        /// If the value is null then the code will look for a property
        /// in the cloud data with the same name suffixed with 'nullreason'.
        /// If it exists, then it's value will be used to set the 
        /// noValueMessage on the new <see cref="AspectPropertyValue{T}"/>.
        /// </remarks>
        /// <param name="cloudData">
        /// The cloud data to be processed.
        /// Keys are flat property names (i.e. no '.' separators).
        /// Values are the property values.
        /// </param>
        /// <param name="propertyMetaData">
        /// The meta-data for the properties in the data.
        /// This will usually be the list from <see cref="Properties"/>
        /// but will be different if dealing with sub-properties.
        /// </param>
        /// <returns>
        /// A dictionary containing the original values converted to 
        /// <see cref="AspectPropertyValue{T}"/> instances where needed.
        /// Any entries in the source dictionary where the key ends 
        /// with 'nullreason' will not appear in the output.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if a required parameter is null.
        /// </exception>
        protected Dictionary<string, object> CreateAPVDictionary(
            Dictionary<string, object> cloudData,
            IReadOnlyList<IElementPropertyMetaData> propertyMetaData)
        {
            if (cloudData == null) throw new ArgumentNullException(nameof(cloudData));

            // Convert the meta-data to a dictionary for faster access.
            var metaDataDictionary = propertyMetaData.ToDictionary(
                p => p.Name, p => p, 
                StringComparer.OrdinalIgnoreCase);

            Dictionary<string, object> result = new Dictionary<string, object>();
            // Iterate through all entries in the source data where the
            // key is not suffixed with 'nullreason'.
            foreach (var property in cloudData
                .Where(kvp => kvp.Key.EndsWith("nullreason", 
                    StringComparison.OrdinalIgnoreCase) == false))
            {
                var outputValue = property.Value;

                if (metaDataDictionary.TryGetValue(property.Key, out IElementPropertyMetaData metaData) == true)
                {
                    if (typeof(IAspectPropertyValue).IsAssignableFrom(metaData.Type))
                    {
                        // If this property has a type of AspectPropertyValue
                        // then create a new instance and populate it.
                        var apvType = typeof(AspectPropertyValue<>);
                        var genericType = apvType.MakeGenericType(metaData.Type.GetGenericArguments());
                        object obj = Activator.CreateInstance(genericType);
                        var apv = obj as IAspectPropertyValue;
                        if (property.Value != null)
                        {
                            var newValue = property.Value;
                            // TODO - Replace this with a more generalized type factory.
                            var valueType = metaData.Type.GetGenericArguments()[0];
                            if (valueType == typeof(JavaScript))
                            {
                                newValue = new JavaScript(newValue.ToString());
                            }
                            else if (valueType == typeof(Dictionary<string, string>))
                            {
                                newValue = ((Newtonsoft.Json.Linq.JObject)newValue).ToObject<Dictionary<string, string>>();
                            }
                            apv.Value = newValue;
                        }
                        else 
                        { 
                            // Value is null so check if we have a 
                            // corresponding reason.
                            // We need to set the no value message with 
                            // reflection as the property is read only 
                            // through the interface.
                            var messageProperty = genericType.GetProperty("NoValueMessage");
                            if (cloudData.TryGetValue(property.Key + "nullreason", out object nullreason))
                            {
                                messageProperty.SetValue(apv, nullreason);
                            }
                            else
                            {
                                messageProperty.SetValue(apv, "Unknown");
                            }
                        }
                        outputValue = apv;
                    }
                }
                else
                {
                    Logger.LogWarning($"No meta-data entry for property " +
                        $"'{property.Key}' in '{GetType().Name}'");
                }

                result.Add(property.Key, outputValue);
            }
            return result;
        }

        /// <summary>
        /// Try to get the type of a property from the information
        /// returned by the cloud service. This should be overridden
        /// if anything other than simple types are required.
        /// </summary>
        /// <param name="propertyMetaData">
        /// The <see cref="PropertyMetaData"/> instance to translate.
        /// </param>
        /// <param name="parentObjectType">
        /// The type of the object on which this property exists.
        /// </param>
        /// <returns>
        /// The type of the property determined from the Type field
        /// of propertyMetaData.
        /// </returns>
        protected virtual Type GetPropertyType(
            PropertyMetaData propertyMetaData,
            Type parentObjectType)
        {
            if (propertyMetaData == null)
            {
                throw new ArgumentNullException(nameof(propertyMetaData));
            }
            switch (propertyMetaData.Type)
            {
                case "String":
                    return typeof(string);
                case "Int32":
                    return typeof(int);
                case "Boolean":
                    return typeof(bool);
                case "JavaScript":
                    return typeof(JavaScript);
                case "Double":
                    return typeof(double);
                case "Array":
                default:
                    throw new PipelineException(string.Format(
                        CultureInfo.InvariantCulture,
                        Messages.ExceptionCloudPropertyType,
                        parentObjectType,
                        propertyMetaData.Name,
                        propertyMetaData.Type));
            }
        }
    }
}
