using FiftyOne.DiD.Core.Data;
using FiftyOne.DiD.Core.FlowElements;
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
// ComponentMetaDataDefault lives in the FiftyOne.DiD.Core project but
// (inconsistently) under the FiftyOne.DiD.OnPremise.Data namespace —
// no dependency on a DiD.OnPremise project, just the namespace.
using FiftyOne.DiD.OnPremise.Data;

namespace FiftyOne.DiD.Cloud.FlowElements
{
    /// <summary>
    /// On-premise engine for 51DiD
    /// </summary>
    public class DiDCloudEngine : CloudAspectEngineBase<I51DidData>, IDiDEngine
    {
        /// <inheritdoc/>
        public override string ElementDataKey
            => DiDBaseEnginePropertiesBuilder.ComponentName;
    
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
        public DiDCloudEngine(
            ILogger<AspectEngineBase<I51DidData, IAspectPropertyMetaData>> logger,
            Func<
                IPipeline,
                FlowElementBase<I51DidData, IAspectPropertyMetaData>,
                I51DidData> aspectDataFactory)
            : base(logger, aspectDataFactory)
        {
            _componentMetaData = DiDBaseEnginePropertiesBuilder
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
