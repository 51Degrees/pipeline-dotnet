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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using System.Globalization;

namespace FiftyOne.Pipeline.Engines.Services
{
    /// <summary>
    /// Service that determines the reason for a property not being populated 
    /// by an engine.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/properties.md#missing-properties">Specification</see>
    /// </summary>
    public class MissingPropertyService : IMissingPropertyService
    {
        private static IMissingPropertyService _instance;
        private static object _lock = new object();

        /// <summary>
        /// Used to store the results of looking up whether a property is available or not.
        /// <seealso cref="EngineDataContainsPropertyGetter"/>
        /// The key is the engine type. The inner dictionary is keyed on property name.
        /// </summary>
        private static Dictionary<Type, Dictionary<string, bool>> _propertyAvailable = 
            new Dictionary<Type, Dictionary<string, bool>>();

        /// <summary>
        /// Get the singleton instance of this service.
        /// </summary>
        public static IMissingPropertyService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if(_instance == null)
                        {
                            _instance = new MissingPropertyService();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Constructor is private to ensure the single instance accessible
        /// through the 'Instance' property is used.
        /// </summary>
        private MissingPropertyService() { }

        /// <summary>
        /// Get the reason that a property is not available from an engine.
        /// </summary>
        /// <param name="propertyName">
        /// The name of the property to look for
        /// </param>
        /// <param name="engines">
        /// The engines that are expected to supply the property value.
        /// </param>
        /// <returns>
        /// A <see cref="MissingPropertyResult"/> instance that includes an
        /// enum giving the reason and a developer-facing description of 
        /// the reason.
        /// </returns>
        public MissingPropertyResult GetMissingPropertyReason(string propertyName, IReadOnlyList<IAspectEngine> engines)
        {
            MissingPropertyResult result = null;
            foreach (var engine in engines.Where(e => e != null))
            {
                result = GetMissingPropertyReason(propertyName, engine);
                if (result.Reason != MissingPropertyReason.Unknown)
                {
                    return result;
                }
            }
            return result;
        }

        /// <summary>
        /// Get the reason that a property is not available from an engine.
        /// </summary>
        /// <param name="propertyName">
        /// The name of the property to look for
        /// </param>
        /// <param name="engine">
        /// The engine that is expected to supply the property value.
        /// </param>
        /// <returns>
        /// A <see cref="MissingPropertyResult"/> instance that includes an
        /// enum giving the reason and a developer-facing description of 
        /// the reason.
        /// </returns>
        public MissingPropertyResult GetMissingPropertyReason(string propertyName, IAspectEngine engine)
        {
            if(engine == null)
            {
                throw new ArgumentNullException(nameof(engine));
            }

            MissingPropertyResult result = new MissingPropertyResult();
            MissingPropertyReason reason = MissingPropertyReason.Unknown;

            IAspectPropertyMetaData property = null;

            if (engine.HasLoadedProperties)
            {
                property = engine.Properties.FirstOrDefault(p => p.Name == propertyName);

                if (property != null)
                {
                    // Check if the property is available in the data file that is 
                    // being used by the engine.
                    if (property.DataTiersWherePresent.Any(t => t == engine.DataSourceTier) == false)
                    {
                        reason = MissingPropertyReason.DataFileUpgradeRequired;
                    }
                    // Check if the property is excluded from the results.
                    else if (property.Available == false)
                    {
                        reason = MissingPropertyReason.PropertyExcludedFromEngineConfiguration;
                    }
                }
            }

            if(reason == MissingPropertyReason.Unknown &&
                engine.HasLoadedProperties &&
                typeof(ICloudAspectEngine).IsAssignableFrom(engine.GetType()))
            {
                if (engine.Properties.Count == 0)
                {
                    reason = MissingPropertyReason.ProductNotAccessibleWithResourceKey;
                }
                else
                {
                    reason = MissingPropertyReason.PropertyNotAccessibleWithResourceKey;
                }
            }
            else if (reason == MissingPropertyReason.Unknown &&
                EngineDataContainsPropertyGetter(propertyName, engine))
            {
                // If the property meta data is not available, but the engine
                // data class defines a getter, it's safe to assume that the data
                // file needs upgrading.
                reason = MissingPropertyReason.DataFileUpgradeRequired;
            }

            // Build the message string to return to the caller.
            StringBuilder message = new StringBuilder();
            message.Append(
                string.Format(CultureInfo.InvariantCulture,
                    Messages.MissingPropertyMessagePrefix,
                    propertyName,
                    engine.ElementDataKey));
            switch (reason)
            {
                case MissingPropertyReason.DataFileUpgradeRequired:
                    message.Append(
                        string.Format(CultureInfo.InvariantCulture,
                            Messages.MissingPropertyMessageDataUpgradeRequired,
                            property == null ?
                                "Unknown" :
                                string.Join(",", property.DataTiersWherePresent),
                            engine.GetType().Name));
                    break;
                case MissingPropertyReason.PropertyExcludedFromEngineConfiguration:
                    message.Append(Messages.MissingPropertyMessagePropertyExcluded);
                    break;
                case MissingPropertyReason.ProductNotAccessibleWithResourceKey:
                    message.Append(
                       string.Format(CultureInfo.InvariantCulture,
                           Messages.MissingPropertyMessageProductNotInCloudResource,
                           engine.ElementDataKey));
                    break;
                case MissingPropertyReason.PropertyNotAccessibleWithResourceKey:
                    message.Append(
                        string.Format(CultureInfo.InvariantCulture,
                            Messages.MissingPropertyMessagePropertyNotInCloudResource,
                            engine.ElementDataKey,
                            string.Join(",", engine.Properties.Select(p => p.Name))));
                    break;
                case MissingPropertyReason.Unknown:
                    message.Append(Messages.MissingPropertyMessageUnknown);
                    break;
                default:
                    break;
            }

            result.Description = message.ToString();
            result.Reason = reason;
            return result;
        }

        /// <summary>
        /// Return true if there is an explicit property getter for the name provided
        /// in the data type returned by the engine.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="engine"></param>
        /// <returns></returns>
        private bool EngineDataContainsPropertyGetter(string propertyName, IAspectEngine engine)
        {
            // Get the property dictionary for this engine
            Dictionary<string, bool> engineProperties;
            if(_propertyAvailable.TryGetValue(engine.GetType(), out engineProperties) == false)
            {
                lock (_propertyAvailable)
                {
                    if (_propertyAvailable.TryGetValue(engine.GetType(), out engineProperties) == false)
                    {
                        engineProperties = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                        _propertyAvailable.Add(engine.GetType(), engineProperties);
                    }
                }
            }

            // If we don't have a stored result in the dictionary for this property then use
            // reflection to figure it out.
            if (engineProperties.TryGetValue(propertyName, out var result) == false)
            {
                result = false;

                foreach (var dataType in engine.GetType().GetInterfaces().SelectMany(i => i.GetGenericArguments())
                    .Where(i => typeof(IAspectData).IsAssignableFrom(i)))
                {
                    if (dataType != null && result == false)
                    {
                        foreach (var property in dataType.GetProperties())
                        {
                            if (property.Name.Equals(propertyName, StringComparison.InvariantCultureIgnoreCase))
                            {
                                result = true;
                                break;
                            }
                        }
                    }
                }

                // Add the result to the property dictionary.
                lock (engineProperties)
                {
                    if (engineProperties.ContainsKey(propertyName) == false)
                    {
                        engineProperties.Add(propertyName, result);
                    }
                }
            }
            return result;
        }
    }
}
