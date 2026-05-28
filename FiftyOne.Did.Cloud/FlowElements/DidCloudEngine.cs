using FiftyOne.Did.Core.Data;
using FiftyOne.Did.Core.FlowElements;
using FiftyOne.Pipeline.CloudRequestEngine.Data;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
// ComponentMetaDataDefault lives in the FiftyOne.Did.Core project but
// (inconsistently) under the FiftyOne.Did.OnPremise.Data namespace —
// no dependency on a Did.OnPremise project, just the namespace.
using FiftyOne.Did.OnPremise.Data;

namespace FiftyOne.Did.Cloud.FlowElements
{
    /// <summary>
    /// On-premise engine for 51Did
    /// </summary>
    public class DidCloudEngine : CloudAspectEngineBase<I51DidData>, IDidEngine
    {
        /// <inheritdoc/>
        public override string ElementDataKey
            => DidBaseEnginePropertiesBuilder.ComponentName;
    
        /// <inheritdoc/>
        public override IEvidenceKeyFilter EvidenceKeyFilter { get; } =
            new EvidenceKeyFilterWhitelist(new List<string>());

        /// <inheritdoc/>
        public override IList<IAspectPropertyMetaData> Properties => _properties;

        private readonly List<IAspectPropertyMetaData> _properties;

        /// <summary>
        /// Indicates that properties have been loaded.
        /// Always returns true since this engine uses locally defined properties
        /// rather than cloud metadata.
        /// </summary>
        public override bool HasLoadedProperties => true;

        private static readonly JsonConverter[] JsonConverters = {
            new CloudJsonConverter()
        };
        private ComponentMetaDataDefault _componentMetaData;

        /// <summary>
        ///     Designated constructor.
        /// </summary>
        /// <param name="logger">
        ///     The logger to use
        /// </param>
        /// <param name="aspectDataFactory">
        ///     The factory function to use
        ///     when the engine creates an
        ///     FiftyOne.Pipeline.Engines.Data.AspectDataBase instance.
        /// </param>
        public DidCloudEngine(
            ILogger<AspectEngineBase<I51DidData, IAspectPropertyMetaData>> logger,
            Func<
                IPipeline,
                FlowElementBase<I51DidData, IAspectPropertyMetaData>,
                I51DidData> aspectDataFactory)
            : base(logger, aspectDataFactory)
        {
            _componentMetaData = DidBaseEnginePropertiesBuilder
                .BuildComponentMetaData(this, withAspectValueTypes: true);
            _properties = _componentMetaData.GetProperties()
                .Cast<IAspectPropertyMetaData>()
                .ToList();
        }


        /// <inheritdoc/>
        protected override void ProcessCloudEngine(IFlowData data, I51DidData aspectData, string json)
        {
            if (aspectData == null) throw new ArgumentNullException(nameof(aspectData));

            // Extract data from JSON to the aspectData instance.
            var dictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            var fodidEntry = dictionary
                .FirstOrDefault(kvp => 0 == string.Compare(
                    ElementDataKey, kvp.Key,
                    StringComparison.OrdinalIgnoreCase));
            if (fodidEntry.Value is null)
            {
                // Upstream cloud service omitted the fodid block
                // (e.g. id.usage evidence was not provided), so
                // there is nothing to populate.
                return;
            }
            var propertyValues = JsonConvert.DeserializeObject<Dictionary<string, object>>(
                fodidEntry.Value.ToString(),
                new JsonSerializerSettings()
                {
                    Converters = JsonConverters,
                });

            var device = CreateAPVDictionary(propertyValues, _properties);
            aspectData.PopulateFrom(device);
        }

        /// <inheritdoc/>
        protected override Type GetPropertyType(
            PropertyMetaData propertyMetaData,
            Type parentObjectType) =>
            propertyMetaData is null
                ? throw new ArgumentNullException(nameof(propertyMetaData))
                : base.GetPropertyType(propertyMetaData, parentObjectType);

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (!IsDisposed && disposing)
            {
                _componentMetaData?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
