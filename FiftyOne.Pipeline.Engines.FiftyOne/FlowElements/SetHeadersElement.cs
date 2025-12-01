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
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FiftyOne.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FiftyOne.Pipeline.Engines.FiftyOne.FlowElements
{
    /// <summary>
    /// This element is used to determine the values for HTTP headers that other elements in the 
    /// pipeline want to be sent to the client.    
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/pipeline-elements/set-headers-element.md">Specification</see>
    /// </summary>
    public class SetHeadersElement :
        FlowElementBase<ISetHeadersData, IElementPropertyMetaData>,
        ISetHeadersElement
    {
        /// <summary>
        /// The element data key used by default for this element.
        /// </summary>
#pragma warning disable CA1707 // Identifiers should not contain underscores
        public const string DEFAULT_ELEMENT_DATA_KEY = "set-headers";

        private const string SET_HEADER_PROPERTY_PREFIX = "SetHeader";
#pragma warning restore CA1707 // Identifiers should not contain underscores

        /// <summary>
        /// Contains configuration information relating to a particular 
        /// pipeline.
        /// In most cases, a single instance of this element will only 
        /// be added to one pipeline at a time but it does support being 
        /// added to multiple pipelines simultaneously.
        /// </summary>
        protected class PipelineConfig
        {
            /// <summary>
            /// A collection containing details about any properties 
            /// where the name starts with 'SetHeader'
            /// </summary>
            public Dictionary<string, PropertyDetails> SetHeaderProperties { get; } =
                new Dictionary<string, PropertyDetails> ();
        }

        /// <summary>
        /// Used to store details of SetHeader* properties
        /// </summary>
        protected class PropertyDetails
        {
            /// <summary>
            /// The property meta data
            /// </summary>
            public IElementPropertyMetaData PropertyMetaData { get; set; }
            /// <summary>
            /// The name of the HTTP header to set from the 
            /// value of this property.
            /// </summary>
            public string ResponseHeaderName { get; set; }
        }

        private EvidenceKeyFilterWhitelist _evidenceKeyFilter;
        private List<IElementPropertyMetaData> _properties;

        private ConcurrentDictionary<IPipeline, PipelineConfig> _pipelineConfigs;

        /// <inheritdoc/>
        public SetHeadersElement(
            ILogger<FlowElementBase<ISetHeadersData, IElementPropertyMetaData>> logger,
            Func<IPipeline, FlowElementBase<ISetHeadersData, IElementPropertyMetaData>, ISetHeadersData> elementDataFactory)
            : base(logger, elementDataFactory)
        {
            _evidenceKeyFilter =
                new EvidenceKeyFilterWhitelist(new List<string>() { });
            _properties = new List<IElementPropertyMetaData>()
                {
                    new ElementPropertyMetaData(
                        this, "responseheaderdictionary", typeof(IReadOnlyDictionary<string, string>), true)
                };
            _pipelineConfigs = new ConcurrentDictionary<IPipeline, PipelineConfig>();
        }

        /// <inheritdoc/>
        public override string ElementDataKey => DEFAULT_ELEMENT_DATA_KEY;

        /// <inheritdoc/>
        public override IEvidenceKeyFilter EvidenceKeyFilter => _evidenceKeyFilter;

        /// <inheritdoc/>
        public override IList<IElementPropertyMetaData> Properties => _properties;

        /// <inheritdoc/>
        protected override void ProcessInternal(IFlowData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            if (_pipelineConfigs.TryGetValue(data.Pipeline, out PipelineConfig config) == false)
            {
                config = PopulateConfig(data.Pipeline);
                config = _pipelineConfigs.GetOrAdd(data.Pipeline, config);
            }

            var elementData = data.GetOrAdd(
                    ElementDataKeyTyped,
                    CreateElementData);
            var jsonString = BuildResponseHeaderDictionary(data, config);
            elementData.ResponseHeaderDictionary = jsonString;
        }

        /// <inheritdoc/>
        protected override void ManagedResourcesCleanup()
        {
        }

        /// <inheritdoc/>
        protected override void UnmanagedResourcesCleanup()
        {
        }

        private Dictionary<string, string> BuildResponseHeaderDictionary(
            IFlowData data, PipelineConfig config)
        {
            Dictionary<string, HashSet<string>> result = new Dictionary<string, HashSet<string>>();

            // Iterate through 'SetHeader*' properties
            foreach(var property in config.SetHeaderProperties)
            {
                // Get the value for this property.
                var elementData = data.Get(property.Value.PropertyMetaData.Element.ElementDataKey);
                object propertyValue = null;
                try
                {
                    propertyValue = elementData[property.Key];
                }
                catch (PropertyMissingException e)
                {
                    Logger.LogWarning($"Property '${property.Key}' was missing. " +
                        $"Not adding SetHeader value. Error was: ${e.Message}");
                }
                if (propertyValue != null)
                {
                    // Extract the string value.
                    var headerValue = GetHeaderValue(propertyValue);

                    // If value is not blank, null or 'unknown' then
                    // add it to the complete value for the associated
                    // header.
                    if (string.IsNullOrEmpty(headerValue) == false &&
                        headerValue.Equals("Unknown", StringComparison.OrdinalIgnoreCase) == false)
                    {
                        HashSet<string> values;
                        if (result.TryGetValue(property.Value.ResponseHeaderName, out values) == false)
                        {
                            // No entry for this header name so create it.
                            values = new HashSet<string>();
                            result.Add(property.Value.ResponseHeaderName, values);
                        }
                        // Get the individual entries from the comma-separated value for this
                        // property. If each entry is not already in the list of values for this 
                        // header then add it.
                        foreach (var value in headerValue.Split(',')
                            .Select(v => v.Trim()))
                        {
                            if (values.Contains(value) == false)
                            {
                                values.Add(value);
                            }
                        }
                    }
                }
            }

            return result.ToDictionary(entry => entry.Key, 
                entry => string.Join(",", entry.Value));
        }

        private static string GetHeaderValue(object propertyValue)
        {
            var result = string.Empty;
            if (propertyValue is IAspectPropertyValue<string> apv &&
                apv.HasValue)
            {
                result = apv.Value;
            }
            else if (propertyValue is string value)
            {
                result = value;
            }
            return result;
        }

        /// <summary>
        /// Executed on first request in order to build some collections 
        /// from the meta-data exposed by the Pipeline.
        /// </summary>
        private PipelineConfig PopulateConfig(IPipeline pipeline)
        {
            var config = new PipelineConfig();

            // Populate the collection that contains a list of the
            // properties with names starting with 'SetHeader'
            foreach (var element in pipeline.ElementAvailableProperties)
            {
                foreach (var property in element.Value.Where(p => 
                    p.Key.StartsWith(SET_HEADER_PROPERTY_PREFIX, StringComparison.OrdinalIgnoreCase)))
                {
                    PropertyDetails details = new PropertyDetails()
                    {
                        PropertyMetaData = property.Value,
                        ResponseHeaderName = GetResponseHeaderName(property.Key)
                    };
                    config.SetHeaderProperties.Add(property.Key, details);
                }
            }

            return config;
        }

        private static string GetResponseHeaderName(string propertyName)
        {
            // This comparison is case-sensitive because this process relies
            // on the property name having very particular casing.
            // This cannot function if e.g. the whole name is lowercase.
            // Consequently, we want to throw an exception here if the 
            // property name does not match the expected case.
            if(propertyName.StartsWith(SET_HEADER_PROPERTY_PREFIX, 
                StringComparison.Ordinal) == false)
            {
                throw new ArgumentException(string.Format(
                        CultureInfo.InvariantCulture,
                        Messages.ExceptionSetHeadersNotSetHeader, 
                        propertyName), 
                    nameof(propertyName));
            }
            if(propertyName.Length < SET_HEADER_PROPERTY_PREFIX.Length + 2)
            {
                throw new ArgumentException(string.Format(
                        CultureInfo.InvariantCulture,
                        Messages.ExceptionSetHeadersWrongFormat,
                        propertyName),
                    nameof(propertyName));
            }

            int nextUpper = -1;
            for(int i = SET_HEADER_PROPERTY_PREFIX.Length + 1; i < propertyName.Length; i++)
            {
                if (char.IsUpper(propertyName[i]))
                {
                    nextUpper = i;
                    break;
                }
            }

            if(nextUpper == -1)
            {
                throw new ArgumentException(string.Format(
                        CultureInfo.InvariantCulture,
                        Messages.ExceptionSetHeadersWrongFormat,
                        propertyName),
                    nameof(propertyName));
            }

            return propertyName.Substring(nextUpper);
        }
    }
}
