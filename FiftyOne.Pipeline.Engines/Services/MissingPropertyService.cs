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
using System.Globalization;
using System.Linq;
using System.Text;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;

namespace FiftyOne.Pipeline.Engines.Services
{
    /// <summary>
    /// Service that determines the reason for a property not being populated
    /// by an engine.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/properties.md#missing-properties">Specification</see>
    /// </summary>
    /// <remarks>
    /// Cloud-specific behaviour that depends on per-request state — such as
    /// reporting <see cref="MissingPropertyReason.CloudRequestFailed"/> when
    /// the upstream cloud request failed for the request that produced the
    /// aspect data — is added by the cloud-specific subclass
    /// <c>MissingPropertyServiceCloud</c> in the CloudRequestEngine
    /// package.
    /// </remarks>
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
        [Obsolete("For cloud engines use MissingPropertyServiceCloud.Instance — " +
            "this singleton returns generic reasons that mis-report missing " +
            "properties when a cloud request has failed. On-premise engines " +
            "may continue to use this singleton; suppress this warning at the " +
            "call site to acknowledge the choice.")]
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
        /// Constructor. Use <see cref="Instance"/> to obtain a singleton.
        /// The constructor is protected so that cloud-specific subclasses
        /// (e.g. <c>MissingPropertyServiceCloud</c>) can inherit.
        /// </summary>
        protected MissingPropertyService() { }

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

        /// <inheritdoc/>
        public virtual MissingPropertyResult GetMissingPropertyReason(string propertyName, IReadOnlyList<IAspectEngine> engines, IAspectData aspectData)
        {
            return GetMissingPropertyReason(propertyName, engines);
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

            var reason = DetermineReason(propertyName, engine, out IAspectPropertyMetaData property);
            return new MissingPropertyResult
            {
                Reason = reason,
                Description = BuildDescription(reason, propertyName, engine, property),
            };
        }

        /// <summary>
        /// Determine the reason that <paramref name="propertyName"/> is
        /// missing for the supplied <paramref name="engine"/>.
        /// </summary>
        /// <remarks>
        /// Resolution order (first match wins):
        /// <list type="number">
        /// <item>
        /// If the property is in the engine's meta-data:
        /// <list type="bullet">
        /// <item>
        /// For on-premise engines, when
        /// <see cref="IAspectEngine.DataSourceTier"/> is not listed in the
        /// property's <see cref="IAspectPropertyMetaData.DataTiersWherePresent"/>,
        /// return <see cref="MissingPropertyReason.DataFileUpgradeRequired"/>.
        /// Cloud engines do not populate <c>DataTiersWherePresent</c>, so
        /// this check is skipped for them — it would otherwise always fail
        /// and mis-report missing cloud properties as upgrade-required.
        /// </item>
        /// <item>
        /// Otherwise, when the property is marked unavailable
        /// (<c>property.Available == false</c>), return
        /// <see cref="MissingPropertyReason.PropertyExcludedFromEngineConfiguration"/>.
        /// </item>
        /// </list>
        /// </item>
        /// <item>
        /// If the engine is an <see cref="ICloudAspectEngine"/> and has
        /// loaded its meta-data, return
        /// <see cref="MissingPropertyReason.ProductNotAccessibleWithResourceKey"/>
        /// when the engine reports zero properties, otherwise
        /// <see cref="MissingPropertyReason.PropertyNotAccessibleWithResourceKey"/>.
        /// </item>
        /// <item>
        /// If reflection on the engine's data type finds a property getter
        /// with the requested name, fall back to
        /// <see cref="MissingPropertyReason.DataFileUpgradeRequired"/>.
        /// </item>
        /// <item>
        /// Otherwise <see cref="MissingPropertyReason.Unknown"/>.
        /// </item>
        /// </list>
        /// <para>
        /// <see cref="MissingPropertyReason.CloudRequestFailed"/> is not
        /// produced here — that reason depends on per-request state and
        /// is added by <c>MissingPropertyServiceCloud</c> via the 3-arg
        /// <see cref="GetMissingPropertyReason(string, IReadOnlyList{IAspectEngine}, IAspectData)"/>
        /// overload.
        /// </para>
        /// </remarks>
        private MissingPropertyReason DetermineReason(
            string propertyName,
            IAspectEngine engine,
            out IAspectPropertyMetaData property)
        {
            property = null;
            bool isCloudEngine = engine is ICloudAspectEngine;

            if (engine.HasLoadedProperties)
            {
                property = engine.Properties.FirstOrDefault(p => p.Name == propertyName);

                if (property != null)
                {
                    if (isCloudEngine == false &&
                        property.DataTiersWherePresent.Any(t => t == engine.DataSourceTier) == false)
                    {
                        return MissingPropertyReason.DataFileUpgradeRequired;
                    }
                    if (property.Available == false)
                    {
                        return MissingPropertyReason.PropertyExcludedFromEngineConfiguration;
                    }
                }
            }

            if (isCloudEngine && engine.HasLoadedProperties)
            {
                return engine.Properties.Count == 0
                    ? MissingPropertyReason.ProductNotAccessibleWithResourceKey
                    : MissingPropertyReason.PropertyNotAccessibleWithResourceKey;
            }

            if (EngineDataContainsPropertyGetter(propertyName, engine))
            {
                return MissingPropertyReason.DataFileUpgradeRequired;
            }

            return MissingPropertyReason.Unknown;
        }

        /// <summary>
        /// Build the developer-facing description string for the supplied
        /// reason. Always begins with
        /// <see cref="Messages.MissingPropertyMessagePrefix"/> and is
        /// suffixed with the reason-specific message.
        /// </summary>
        private static string BuildDescription(
            MissingPropertyReason reason,
            string propertyName,
            IAspectEngine engine,
            IAspectPropertyMetaData property)
        {
            var message = new StringBuilder();
            message.Append(string.Format(
                CultureInfo.InvariantCulture,
                Messages.MissingPropertyMessagePrefix,
                propertyName,
                engine.ElementDataKey));

            switch (reason)
            {
                case MissingPropertyReason.DataFileUpgradeRequired:
                    message.Append(string.Format(
                        CultureInfo.InvariantCulture,
                        Messages.MissingPropertyMessageDataUpgradeRequired,
                        property == null
                            ? "Unknown"
                            : string.Join(",", property.DataTiersWherePresent),
                        engine.GetType().Name));
                    break;
                case MissingPropertyReason.PropertyExcludedFromEngineConfiguration:
                    message.Append(Messages.MissingPropertyMessagePropertyExcluded);
                    break;
                case MissingPropertyReason.ProductNotAccessibleWithResourceKey:
                    message.Append(string.Format(
                        CultureInfo.InvariantCulture,
                        Messages.MissingPropertyMessageProductNotInCloudResource,
                        engine.ElementDataKey));
                    break;
                case MissingPropertyReason.PropertyNotAccessibleWithResourceKey:
                    message.Append(string.Format(
                        CultureInfo.InvariantCulture,
                        Messages.MissingPropertyMessagePropertyNotInCloudResource,
                        engine.ElementDataKey,
                        string.Join(",", engine.Properties.Select(p => p.Name))));
                    break;
                case MissingPropertyReason.CloudRequestFailed:
                    message.Append(Messages.MissingPropertyMessageCloudRequestFailed);
                    break;
                case MissingPropertyReason.Unknown:
                    message.Append(Messages.MissingPropertyMessageUnknown);
                    break;
                default:
                    break;
            }

            return message.ToString();
        }

        /// <summary>
        /// Return true if there is an explicit property getter for the name
        /// provided in the data type returned by the engine. Results are
        /// cached in <see cref="_propertyAvailable"/> to avoid repeating
        /// the reflection cost.
        /// </summary>
        private static bool EngineDataContainsPropertyGetter(string propertyName, IAspectEngine engine)
        {
            if (_propertyAvailable.TryGetValue(engine.GetType(), out var engineProperties) == false)
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

            if (engineProperties.TryGetValue(propertyName, out var result))
            {
                return result;
            }

            result = false;
            foreach (var dataType in engine.GetType().GetInterfaces()
                .SelectMany(i => i.GetGenericArguments())
                .Where(t => typeof(IAspectData).IsAssignableFrom(t)))
            {
                if (result)
                {
                    break;
                }
                foreach (var property in dataType.GetProperties())
                {
                    if (property.Name.Equals(propertyName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        result = true;
                        break;
                    }
                }
            }

            lock (engineProperties)
            {
                if (engineProperties.ContainsKey(propertyName) == false)
                {
                    engineProperties.Add(propertyName, result);
                }
            }
            return result;
        }
    }
}
