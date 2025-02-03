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

using FiftyOne.Pipeline.JsonBuilder.Data;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Data.Types;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.JsonBuilder.Converters;
using FiftyOne.Pipeline.Core.Exceptions;
using System.Collections.Concurrent;
using Newtonsoft.Json.Serialization;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.Engines;

namespace FiftyOne.Pipeline.JsonBuilder.FlowElement
{
    /// <summary>
    /// The JsonBuilderElement takes accessible properties and adds the property
    /// key:values to the Json object.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/pipeline-elements/json-builder.md">Specification</see>
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "Pipeline API specification is for JSON " +
        "data to always use fully lower-case keys")]
    public class JsonBuilderElement : 
        FlowElementBase<IJsonBuilderElementData, IElementPropertyMetaData>, 
        IJsonBuilderElement
    {
        /// <summary>
        /// The element data key used by default for this element.
        /// </summary>
#pragma warning disable CA1707 // Identifiers should not contain underscores
        public const string DEFAULT_ELEMENT_DATA_KEY = "json-builder";
#pragma warning restore CA1707 // Identifiers should not contain underscores

        /// <summary>
        /// This contract resolver ensurers that property names 
        /// are always converted to lowercase.
        /// </summary>
        private class LowercaseContractResolver : DefaultContractResolver
        {
            protected override string ResolvePropertyName(string propertyName)
            {

                return JAVASCRIPT_PROPERTIES_NAME == propertyName ? 
                    propertyName : propertyName.ToLowerInvariant();
            }
        }

        private const string JAVASCRIPT_PROPERTIES_NAME = "javascriptProperties";

        private EvidenceKeyFilterWhitelist _evidenceKeyFilter;
        private List<IElementPropertyMetaData> _properties;
        private List<string> _propertyExclusionlist;
        private HashSet<string> _elementExclusionList;

        /// <summary>
        /// Contains configuration information relating to a particular 
        /// pipeline.
        /// In most cases, a single instance of this element will only 
        /// be added to one pipeline at a time but it does support being 
        /// added to multiple pipelines.
        /// simultaneously.
        /// </summary>
        protected class PipelineConfig 
        {
            /// <summary>
            /// A collection of the complete string names of any properties 
            /// with the 'delay execution' flag set to true.
            /// Note that 'complete name' means that the name will include 
            /// the element data key and any other parts of the segmented
            /// name.
            /// For example, `device.ismobile`
            /// </summary>
            public HashSet<string> DelayedExecutionProperties { get; } = 
                new HashSet<string>();
            /// <summary>
            /// A collection containing the details of relevant evidence 
            /// properties.
            /// The key is the complete property name.
            /// Note that 'complete name' means that the name will include 
            /// the element data key and any other parts of the segmented
            /// name.
            /// For example, `device.ismobile`
            /// The value is a list of the JavaScript properties that,
            /// when executed, will provide values that can help determine
            /// the value of the key property. 
            /// </summary>
            public Dictionary<string, IReadOnlyList<string>> DelayedEvidenceProperties { get; } = 
                new Dictionary<string, IReadOnlyList<string>>();
        }


        private ConcurrentDictionary<IPipeline, PipelineConfig> _pipelineConfigs 
            = new ConcurrentDictionary<IPipeline, PipelineConfig>();


        // An array of custom converters to use when serializing 
        // the property values.
        private static JsonConverter[] JSON_CONVERTERS = new JsonConverter[]
        {
            new JavaScriptConverter(),
            new AspectPropertyValueConverter()
        };

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">
        /// The logger for this instance to use.
        /// </param>
        /// <param name="jsonConverters">
        /// A collection of <see cref="JsonConverter"/> instances that will
        /// be used when serializing the data.
        /// </param>
        /// <param name="elementDataFactory">
        /// The factory function to use when creating a new element data
        /// instance.
        /// </param>
        public JsonBuilderElement(
            ILogger<JsonBuilderElement> logger,
            IEnumerable<JsonConverter> jsonConverters,
            Func<IPipeline, FlowElementBase<IJsonBuilderElementData, IElementPropertyMetaData>,
                IJsonBuilderElementData> elementDataFactory)
            : base(logger, elementDataFactory)
        {
            // Set the evidence key filter for the flow data to use.
            _evidenceKeyFilter = 
                new EvidenceKeyFilterWhitelist(new List<string>() { });

            _properties = new List<IElementPropertyMetaData>()
                {
                    new ElementPropertyMetaData(
                        this, "json", typeof(string), true)
                };

            // List of properties which should not be added to the Json.
            _propertyExclusionlist = new List<string>() { "products", "properties" };
            // List of the element data keys of elements that should 
            // not be added to the Json.
            _elementExclusionList = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "cloud-response",
                    DEFAULT_ELEMENT_DATA_KEY,
                    SetHeadersElement.DEFAULT_ELEMENT_DATA_KEY
                };

            _pipelineConfigs = new ConcurrentDictionary<IPipeline, PipelineConfig>();

            JSON_CONVERTERS = JSON_CONVERTERS.Concat(jsonConverters).ToArray();
        }

        /// <summary>
        /// The key to identify this engine's element data instance
        /// within <see cref="IFlowData"/>.
        /// </summary>
        public override string ElementDataKey => DEFAULT_ELEMENT_DATA_KEY;

        /// <summary>
        /// A filter that identifies the evidence items that this 
        /// element can make use of.
        /// JsonBuilder does not make use of any evidence so this filter
        /// will never match anything.
        /// </summary>
        public override IEvidenceKeyFilter EvidenceKeyFilter => 
            _evidenceKeyFilter;

        /// <summary>
        /// The meta-data for the properties populated by this element.
        /// </summary>
        public override IList<IElementPropertyMetaData> Properties => 
            _properties;

        /// <summary>
        /// Cleanup of an managed resources.
        /// </summary>
        protected override void ManagedResourcesCleanup()
        { }

        /// <summary>
        /// Transform the data in the flow data instance into a
        /// JSON object.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/>
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied flow data is null.
        /// </exception>
        protected override void ProcessInternal(IFlowData data) =>
            BuildAndInjectJSON(
                data,
                data.GetOrAdd(
                    ElementDataKeyTyped,
                    CreateElementData),
                true);

        /// <summary>
        /// Transform the data in the flow data instance into a
        /// JSON object.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/>
        /// </param>
        /// <param name="elementData">
        /// The <see cref="IJsonBuilderElementData"/>
        /// </param>
        /// <param name="requireSequenceNumber">
        /// Whether to throw if sequence number was not found
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied flow data is null.
        /// </exception>
        private void BuildAndInjectJSON(IFlowData data, IJsonBuilderElementData elementData, bool requireSequenceNumber)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            if(_pipelineConfigs.TryGetValue(data.Pipeline, out PipelineConfig config) == false)
            {
                config = PopulateMetaDataCollections(data.Pipeline);
                config = _pipelineConfigs.GetOrAdd(data.Pipeline, config);
            }

            var jsonString = BuildJson(data, config, requireSequenceNumber);
            elementData.Json = jsonString;
        }

        /// <summary>
        /// Transform the data in the flow data instance into a
        /// JSON object.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/>
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied flow data is null.
        /// </exception>
        public IJsonBuilderElementData GetFallbackResponse(IFlowData data)
        {
            var elementData = CreateElementData(data.Pipeline);
            BuildAndInjectJSON(data, elementData, false);
            return elementData;
        }


        /// <summary>
        /// Create and populate a JSON string from the specified data.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="config">The configuration to use</param>
        /// <returns>
        /// A string containing the data in JSON format.
        /// </returns>
        protected string BuildJson(IFlowData data, PipelineConfig config)
            => BuildJson(data, config, true);

        /// <summary>
        /// Create and populate a JSON string from the specified data.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="config">The configuration to use</param>
        /// <param name="requireSequenceNumber">Whether to throw if sequence number was not found.</param>
        /// <returns>
        /// A string containing the data in JSON format.
        /// </returns>
        protected virtual string BuildJson(IFlowData data, PipelineConfig config, bool requireSequenceNumber)
        {
            int? sequenceNumber = null;
            try
            {
                sequenceNumber = GetSequenceNumber(data);
            }
            catch (Exception e)
            {
                if (requireSequenceNumber)
                {
                    throw;
                }
                Logger.LogError(e, $"Failed to get {nameof(sequenceNumber)}.");
            }

            // Get property values from all the elements and add the ones that
            // are accessible to a dictionary.
            Dictionary<String, object> allProperties = GetAllProperties(data, config);

            // Only populate the JavaScript properties if the sequence 
            // has not reached max iterations.
            if (!sequenceNumber.HasValue || sequenceNumber.Value < Constants.MAX_JAVASCRIPT_ITERATIONS)
            {
                AddJavaScriptProperties(data, allProperties);
            }

            AddErrors(data, allProperties);

            try
            {
                return BuildJson(allProperties);
            }
            catch (JsonWriterException jsonEx)
            {
                StringBuilder msg = new StringBuilder();
                msg.AppendLine("Error converting data to json string");
                msg.AppendLine("[");
                foreach(var entry in allProperties)
                {
                    msg.AppendLine($"  '{entry.Key}' = {GetObjectAsString(entry.Value, 2)}");
                }
                msg.AppendLine("]");
                var exception = new PipelineDataException(msg.ToString(), jsonEx);
                data.AddError(exception, this);
            }
            return "{ \"error\": \"see flow data errors for more detail\" }";
        }

        /// <summary>
        /// Get a string representation of the specified object.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="indent"></param>
        /// <returns></returns>
        private string GetObjectAsString(object obj, int indent)
        {
            StringBuilder valueAsString = new StringBuilder();
            
            if (obj.GetType() != typeof(string) &&
                typeof(IEnumerable).IsAssignableFrom(obj.GetType()))
            {
                valueAsString.AppendLine("[");
                foreach (var item in obj as IEnumerable)
                {
                    valueAsString.Append(new String(' ', indent * 2));
                    valueAsString.AppendLine($"{GetObjectAsString(item, indent + 1)}, ");
                }
                valueAsString.Append(new String(' ', (indent - 1) * 2));
                valueAsString.Append("]");
            }
            else if (typeof(KeyValuePair<string, object>).IsAssignableFrom(obj.GetType()))
            {
                var kvp = (KeyValuePair<string, object>)obj;
                valueAsString.Append($"'{kvp.Key}' = {GetObjectAsString(kvp.Value, indent + 1)}");
            }
            else
            {
                try
                {
                    valueAsString.Append($"'{obj}'");
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    valueAsString.Append($"'Error converting to string: {ex.Message}'");
                }
            }

            return valueAsString.ToString();
        }

        /// <summary>
        /// Build the JSON 
        /// </summary>
        /// <param name="allProperties">
        /// A dictionary containing the data to convert to JSON.
        /// Key is the element data key.
        /// Value is a <code><![CDATA[Dictionary<string, object>]]></code>
        /// containing the property names and values for that element.
        /// </param>
        /// <returns></returns>
        protected virtual string BuildJson(Dictionary<string, object> allProperties)
        {
            // Build the JSON object from the property list containing property 
            // values and errors.
            return JsonConvert.SerializeObject(allProperties,
                Formatting.Indented,
                new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    Converters = JSON_CONVERTERS,
                    ContractResolver = new LowercaseContractResolver()
                });
        }

        /// <summary>
        /// Add any JavaScript properties to the dictionary
        /// </summary>
        /// <param name="data"></param>
        /// <param name="allProperties"></param>
        private void AddJavaScriptProperties(IFlowData data,
            Dictionary<String, object> allProperties)
        {
            var javascriptProperties = GetJavaScriptProperties(data, allProperties);
            if (javascriptProperties != null &&
                javascriptProperties.Count > 0)
            {
                allProperties.Add("javascriptProperties", javascriptProperties);
            }
        }

        /// <summary>
        /// Add any errors in the flow data object to the dictionary
        /// </summary>
        /// <param name="data"></param>
        /// <param name="allProperties"></param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if one of the supplied parameters is null
        /// </exception>
        protected virtual void AddErrors(IFlowData data,
            Dictionary<string, object> allProperties)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (allProperties == null) throw new ArgumentNullException(nameof(allProperties));

            // If there are any errors then add them to the Json.
            if (data.Errors != null && data.Errors.Count > 0)
            {
                var errors = data.Errors
                    .Select(e => e.ExceptionData.Message)
                    .ToArray();

                allProperties.Add("errors", errors);
            }
        }

        /// <summary>
        /// Get the sequence number from the evidence.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied flow data is null.
        /// </exception>
        /// <exception cref="PipelineException">
        /// Thrown if sequence number is not present in the evidence.
        /// </exception>
        protected static int GetSequenceNumber(IFlowData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            if (data.TryGetEvidence(Engines.FiftyOne.Constants.EVIDENCE_SEQUENCE, out int sequence) == false)
            {
                throw new PipelineException(Messages.ExceptionSequenceNumberNotPresent);
            }
            return sequence;
        }

        /// <summary>
        /// Cleanup any unmanaged resources
        /// </summary>
        protected override void UnmanagedResourcesCleanup()
        { }


        /// <summary>
        /// Get all the properties.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="config">The configuration to use</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied flow data is null.
        /// </exception>
        protected virtual Dictionary<string, object> GetAllProperties(
            IFlowData data, 
            PipelineConfig config)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (config == null) throw new ArgumentNullException(nameof(config));

            Dictionary<string, object> allProperties = new Dictionary<string, object>();

            foreach (var element in data.ElementDataAsDictionary().Where(elementData => 
                _elementExclusionList.Contains(elementData.Key) == false))
            {
                if (allProperties.ContainsKey(element.Key.ToLowerInvariant()) == false)
                {
                    var values = GetValues(data,
                        element.Key.ToLowerInvariant(),
                        (element.Value as IElementData).AsDictionary(),
                        config);
                    allProperties.Add(element.Key.ToLowerInvariant(), values);
                }
            }

            return allProperties;
        }

        /// <summary>
        /// Get the names and values for all the JSON properties required
        /// to represent the given source data.
        /// The method adds meta-properties as required such as
        /// *nullreason, *delayexecution, etc.
        /// </summary>
        /// <param name="flowData">
        /// The <see cref="IFlowData"/> for the current request.
        /// </param>
        /// <param name="dataPath">
        /// The . separated name of the container that the supplied 
        /// data will be added to.
        /// For example, 'location' or 'devices.profiles'
        /// </param>
        /// <param name="sourceData">
        /// The source data to use when populating the result.
        /// </param>
        /// <param name="config">
        /// The configuration to use.
        /// </param>
        /// <returns>
        /// A new dictionary with string keys and object values.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if required parameters are null
        /// </exception>
        protected virtual Dictionary<string, object> GetValues(
            IFlowData flowData,
            string dataPath, 
            IReadOnlyDictionary<string, object> sourceData, 
            PipelineConfig config)
        {
            if (dataPath == null)
            {
                throw new ArgumentNullException(nameof(dataPath));
            }
            if (sourceData == null)
            {
                throw new ArgumentNullException(nameof(sourceData));
            }
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            var values = new Dictionary<string, object>();
            foreach(var value in sourceData)
            {
                AddJsonValuesForProperty(flowData, values, dataPath, 
                    value.Key, value.Value, config, false);
            }
            return values;
        }

        /// <summary>
        /// Add entries to the supplied jsonValues dictionary to
        /// represent the supplied property name and value.
        /// </summary>
        /// <param name="flowData">
        /// The <see cref="IFlowData"/> for the current request.
        /// </param>
        /// <param name="jsonValues">
        /// A dictionary containing the key/value pairs that are going 
        /// to appear in the JSON output.
        /// This method will add new entries to this dictionary for 
        /// the supplied property.
        /// </param>
        /// <param name="dataPath">
        /// The . separated name of the container that the supplied 
        /// property will be added to.
        /// For example, 'location' or 'devices.profiles'
        /// </param>
        /// <param name="name">
        /// The name of the property to add to jsonValues.
        /// </param>
        /// <param name="value">
        /// The value of the property to add to jsonValues.
        /// </param>
        /// <param name="config">
        /// The configuration to use.
        /// </param>
        /// <param name="includeValueDataOnly">
        /// Flag used to indicate if additional meta-data properties should 
        /// be written or not.
        /// These are needed when the JSON is going to be used by the
        /// `JavaScriptBuilderElement` to generate content for use on
        /// the client.
        /// They are not needed to simply serialize the values stored in
        /// aspect properties.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if required parameters are null
        /// </exception>
        protected virtual void AddJsonValuesForProperty(
            IFlowData flowData,
            Dictionary<string, object> jsonValues, 
            string dataPath, 
            string name, 
            object value, 
            PipelineConfig config,
            bool includeValueDataOnly)
        {
            if (jsonValues == null)
            {
                throw new ArgumentNullException(nameof(jsonValues));
            }
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
            if (dataPath == null)
            {
                throw new ArgumentNullException(nameof(dataPath));
            }
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            // Make sure property names are lowercase
            name = name.ToLowerInvariant();
            dataPath = dataPath.ToLowerInvariant();

            var completeName = dataPath +
                Core.Constants.EVIDENCE_SEPERATOR +
                name;
            object propertyValue = null;

            if (value is IAspectPropertyValue aspectProperty)
            {
                if (aspectProperty.HasValue)
                {
                    propertyValue = aspectProperty.Value;
                }
                else
                {
                    jsonValues.Add(name, null);
                    jsonValues.Add(name + "nullreason", aspectProperty.NoValueMessage);
                }
            }
            else
            {
                propertyValue = value;
            }

            if (propertyValue != null)
            {
                // If the value is a list of complex types then
                // recursively call this method for each instance
                // in the list.
                if (propertyValue is IList elementDatas &&
                    (typeof(IElementData).IsAssignableFrom(propertyValue.GetType().GetElementType()) ||
                    typeof(IElementData).IsAssignableFrom(propertyValue.GetType().GenericTypeArguments[0])))
                {
                    var results = new List<object>();
                    foreach (var elementData in elementDatas)
                    {
                        results.Add(GetValues(flowData, completeName,
                            ((IElementData)elementData).AsDictionary(), config));
                    }
                    propertyValue = results;
                }

                // Add this value to the output
                jsonValues.Add(name, propertyValue);

                // Add 'delayexecution' flag if needed.
                if (includeValueDataOnly == false && 
                    config.DelayedExecutionProperties.Contains(completeName))
                {
                    jsonValues.Add(name + "delayexecution", true);
                }
            }
            // Add evidence properties list if needed. 
            // (i.e. if the evidence property has delay execution = true)
            if (includeValueDataOnly == false && 
                config.DelayedEvidenceProperties.TryGetValue(completeName,
                out IReadOnlyList<string> evidenceProperties))
            {
                jsonValues.Add(name + "evidenceproperties", evidenceProperties);
            }
        }

        /// <summary>
        /// Get accessible JavaScript properties and the property keys 
        /// (property names) to a list which will be used by the JavaScript
        /// resource to determine if they should be run.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> instance to get properties from.
        /// </param>
        /// <param name="allProperties">
        /// A collection of the properties that are accessible in the
        /// current request.
        /// Key is the element data key. 
        /// Value is a <code><![CDATA[Dictionary<string, object>]]></code>
        /// that contains the property names and values.
        /// </param>
        /// <returns></returns>
        private IList<string> GetJavaScriptProperties(
            IFlowData data,
            Dictionary<string, object> allProperties)
        {
            // Create a list of the available properties in the form of 
            // "elementdatakey.property" from a 
            // Dictionary<string, Dictionary<string, object>> of properties
            // structured as <element<prefix,prop>>  
            var props = new List<string>();

            foreach(var element in allProperties)
                foreach (var property in (Dictionary<string, object>)element.Value)
                    props.Add(element.Key + Core.Constants.EVIDENCE_SEPERATOR + property.Key);

            return GetJavaScriptProperties(data, props);
        }

        /// <summary>
        /// Get the names of all properties from the given 
        /// <see cref="IFlowData"/> instance that have type 'JavaScript'.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> to get properties from.
        /// </param>
        /// <param name="availableProperties">
        /// A list of the full string names (i.e. prefixed with the element 
        /// data key for the element that populates that property) of the 
        /// available properties in dot-separated format.
        /// </param>
        /// <returns>
        /// A list of the full string names of the properties that are of 
        /// type 'JavaScript'
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied flow data is null.
        /// </exception>
        protected virtual IList<string> GetJavaScriptProperties(
            IFlowData data,
            IList<string> availableProperties)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            // Get a list of all the JavaScript properties which are available.
            var javascriptPropertiesEnumerable =
                data.GetWhere(
                    p => p.Type != null &&
                        Utils.IsTypeOrAspectPropertyValue<JavaScript>(p.Type))
                    .Where(p => availableProperties.Contains(p.Key));            

            List<string> javascriptPropeties = new List<string>();
            foreach (var property in javascriptPropertiesEnumerable)
            {
                javascriptPropeties.Add(property.Key);
            }
            return javascriptPropeties;
        }

        /// <summary>
        /// Executed on first request in order to build some collections 
        /// from the meta-data exposed by the Pipeline.
        /// </summary>
        private PipelineConfig PopulateMetaDataCollections(IPipeline pipeline)
        {
            var config = new PipelineConfig();

            // Populate the collection that contains a list of the
            // properties with 'delay execution' = true.
            foreach (var element in pipeline.ElementAvailableProperties)
            {
                foreach (var propertyName in GetDelayedPropertyNames(
                    element.Key.ToLowerInvariant(), 
                    element.Value.Select(kvp => kvp.Value))) 
                {
                    config.DelayedExecutionProperties.Add(propertyName);
                }
            }

            // Now use that information to populate a list of the 
            // evidence property links that we need.
            // This means only those where the evidence property has
            // the delayed execution flag set.
            foreach (var element in pipeline.ElementAvailableProperties)
            {
                foreach (var property in GetEvidencePropertyNames(
                    config.DelayedExecutionProperties,
                    element.Key.ToLowerInvariant(),
                    element.Key.ToLowerInvariant(),
                    element.Value.Select(kvp => kvp.Value)))
                {
                    config.DelayedEvidenceProperties.Add(
                        property.Key, 
                        property.Value.ToList());
                }
            }

            return config;
        }

        /// <summary>
        /// Get the complete names of any properties that have the
        /// delay execution flag set.
        /// </summary>
        /// <param name="dataPath"></param>
        /// <param name="properties"></param>
        /// <returns></returns>
        private IEnumerable<string> GetDelayedPropertyNames (
            string dataPath, 
            IEnumerable<IElementPropertyMetaData> properties)
        {
            // Return the names of any delayed execution properties.
            foreach(var property in properties.Where(p =>
                p.DelayExecution &&
                Utils.IsTypeOrAspectPropertyValue<JavaScript>(p.Type)))
            {
                yield return $"{dataPath}{Core.Constants.EVIDENCE_SEPERATOR}" +
                    $"{property.Name.ToLowerInvariant()}";
            }

            // Call recursively for any properties that have sub-properties.
            foreach (var collection in properties.Where(p =>
                 p.ItemProperties != null &&
                 p.ItemProperties.Count > 0))
            {
                foreach (var propertyName in GetDelayedPropertyNames(
                    $"{dataPath}{Core.Constants.EVIDENCE_SEPERATOR}{collection.Name}",
                    collection.ItemProperties))
                {
                    yield return propertyName;
                }
            }
        }

        private IEnumerable<KeyValuePair<string, IEnumerable<string>>> GetEvidencePropertyNames(
                HashSet<string> delayedExecutionProperties,
                string elementDataKey,
                string propertyDataPath,
                IEnumerable<IElementPropertyMetaData> properties)
        {
            foreach (var property in properties)
            {
                // Build a list of any evidence properties for this property
                // where the evidence property has the delayed execution 
                // flag set.
                List<string> evidenceProperties = new List<string>();
                if (property.EvidenceProperties != null)
                {
                    foreach (var evidenceProperty in property.EvidenceProperties)
                    {
                        var evidenceName =
                            $"{elementDataKey}{Core.Constants.EVIDENCE_SEPERATOR}" +
                            $"{evidenceProperty.ToLowerInvariant()}";
                        if (delayedExecutionProperties.Contains(evidenceName))
                        {
                            evidenceProperties.Add(evidenceName);
                        }
                    }
                }
                // Only return an entry for this property if it has one or
                // more evidence properties.
                if (evidenceProperties.Count > 0)
                {
                    yield return new KeyValuePair<string, IEnumerable<string>>(
                        $"{propertyDataPath}{Core.Constants.EVIDENCE_SEPERATOR}" +
                        $"{property.Name.ToLowerInvariant()}",
                        evidenceProperties);
                }
            }

            // Call recursively for any properties that have sub-properties.
            foreach (var collection in properties.Where(p =>
                 p.ItemProperties != null &&
                 p.ItemProperties.Count > 0))
            {
                foreach (var property in GetEvidencePropertyNames(
                    delayedExecutionProperties,
                    elementDataKey,
                    $"{propertyDataPath}{Core.Constants.EVIDENCE_SEPERATOR}" +
                    $"{collection.Name}",
                    collection.ItemProperties))
                {
                    yield return property;
                }
            }
        }

    }
}
