using System;
using System.Collections.Generic;
using System.Linq;
using FiftyOne.DiD.Core.Data;
using FiftyOne.DiD.OnPremise.Data;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FiftyOne.Data;
using FiftyOne.Pipeline.Engines.FlowElements;

namespace FiftyOne.DiD.Core.FlowElements
{
    /// <summary>
    /// Helper class that builds metadata
    /// for both OnPremise and Cloud versions of 51DiD engine.
    /// </summary>
    public static class DiDBaseEnginePropertiesBuilder
    {
        /// <summary>
        /// Category name for the data.
        /// </summary>
        public const string CategoryName = "FODiD";
        /// <summary>
        /// Component name for the data.
        /// </summary>
        public const string ComponentName = "FODiD";
        private const string DefaultValue = "N/A";

        /// <summary>
        /// Build metadata for 51DiD aspect engine.
        /// </summary>
        /// <param name="engine">51DiD aspect engine.</param>
        /// <param name="withAspectValueTypes">
        /// If <c>true</c>, will wrap the type of properties
        /// into <see cref="IAspectPropertyValue"/>. 
        /// </param>
        /// <returns>Component MetaData with properties populated.</returns>
        public static ComponentMetaDataDefault BuildComponentMetaData(
            IAspectEngine engine, bool withAspectValueTypes)
        {
            var component = new ComponentMetaDataDefault(ComponentName);
            var properties = BuildProperties(engine, component, withAspectValueTypes);
            foreach (var property in properties)
            {
                component.AddProperty(property);
            }
            return component;
        }

        private static readonly IReadOnlyDictionary<string, string> IdProperties
            = new Dictionary<string, string> {
                {
                    nameof(I51DidData.IdProbGlobal),
                    "Probabilistic 51DiD, unique across all callers from the same device and network."
                },
                {
                    nameof(I51DidData.IdProbLic),
                    "Probabilistic 51DiD, unique only across the caller’s license key."
                },
            };
        
        /// <summary>
        /// All properties that can be taken from <see cref="I51DidData"/>.
        /// </summary>
        public static readonly IReadOnlyList<string> AllProperties =
            IdProperties.Keys
                .Select(x => $"{ComponentName}.{x}".ToLowerInvariant())
                .ToList();

        private static IEnumerable<IFiftyOneAspectPropertyMetaData> BuildProperties(
            IAspectEngine engine,
            ComponentMetaDataDefault component,
            bool withAspectValueTypes)
            => IdProperties.Select(
                nextProperty => new FiftyOneAspectPropertyMetaDataDefault(
                    element: engine,
                    name: nextProperty.Key,
                    type: withAspectValueTypes
                        ? typeof(IAspectPropertyValue<string>)
                        : typeof(string),
                    category: CategoryName,
                    dataTiersWherePresent: Array.Empty<string>(),
                    available: true,
                    component: component,
                    defaultValue: new ValueMetaDataDefault(DefaultValue),
                    values: Enumerable.Empty<ValueMetaDataDefault>(),
                    description: nextProperty.Value)
            );
    }
}