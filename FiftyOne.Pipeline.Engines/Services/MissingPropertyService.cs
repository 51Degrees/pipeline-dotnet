/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2020 51 Degrees Mobile Experts Limited, 5 Charlotte Close,
 * Caversham, Reading, Berkshire, United Kingdom RG4 7BY.
 *
 * This Original Work is licensed under the European Union Public Licence (EUPL) 
 * v.1.2 and is subject to its terms as set out below.
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
using System.Threading.Tasks;
using System.Text;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using System.Globalization;

namespace FiftyOne.Pipeline.Engines.Services
{
    /// <summary>
    /// Service that determines the reason for a property not being populated 
    /// by an engine.
    /// </summary>
    public class MissingPropertyService : IMissingPropertyService
    {
        private static IMissingPropertyService _instance;
        private static object _lock = new object();

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

            if(reason == MissingPropertyReason.Unknown &&
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
    }
}
