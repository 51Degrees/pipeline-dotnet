/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2026 51 Degrees Mobile Experts Limited, Davidson House,
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

using System;
using System.Collections.Generic;
using System.Linq;
using FiftyOne.Did.Core.Data;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FiftyOne.Data;
using FiftyOne.Pipeline.Engines.FlowElements;

namespace FiftyOne.Did.Core.FlowElements
{
    /// <summary>
    /// Helper class that builds metadata
    /// for both OnPremise and Cloud versions of 51Did engine.
    /// </summary>
    public static class DidBaseEnginePropertiesBuilder
    {
        /// <summary>
        /// Category name for the data.
        /// </summary>
        public const string CategoryName = "FODid";
        /// <summary>
        /// Component name for the data.
        /// </summary>
        public const string ComponentName = "FODid";
        private const string DefaultValue = "N/A";

        /// <summary>
        /// Build metadata for 51Did aspect engine.
        /// </summary>
        /// <param name="engine">51Did aspect engine.</param>
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
                    "Probabilistic 51Did, unique across all callers from the same device and network."
                },
                {
                    nameof(I51DidData.IdProbLic),
                    "Probabilistic 51Did, unique only across the caller’s license key."
                },
            };

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