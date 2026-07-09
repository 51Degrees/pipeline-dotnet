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

namespace FiftyOne.Pipeline.Core.FlowElements
{
    /// <summary>
    /// Helpers for working with upstream property dependencies.
    /// </summary>
    public static class UpstreamDependencyExtensions
    {
        /// <summary>
        /// Finds the element whose data key matches the vendor prefix of
        /// the given "vendor.Property" key. The property part is not
        /// checked.
        /// </summary>
        /// <param name="pipeline">Pipeline containing the elements.</param>
        /// <param name="propertyKey">
        /// Dependency key, for example "device.DeviceId".
        /// </param>
        /// <returns>The providing element, or null if none matches.</returns>
        public static IFlowElement ProvidedBy(
            this IPipeline pipeline, string propertyKey)
        {
            if (propertyKey == null)
            {
                return null;
            }
            var elementKey = propertyKey.Split('.')[0];
            return pipeline.FlowElements.FirstOrDefault(e =>
                string.Equals(
                    e.ElementDataKey, elementKey,
                    StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Determines whether an element must run: either it is wanted
        /// itself, or a wanted element declares a dependency on one of its
        /// properties. Dependency chains are not walked.
        /// </summary>
        /// <param name="pipeline">Pipeline containing the elements.</param>
        /// <param name="element">Element to check.</param>
        /// <param name="isWanted">
        /// Predicate deciding whether an element is wanted.
        /// </param>
        /// <returns>True if the element is needed.</returns>
        public static bool IsNeededFor(
            this IPipeline pipeline,
            IFlowElement element,
            Func<IFlowElement, bool> isWanted)
        {
            if (isWanted(element))
            {
                return true;
            }
            return pipeline.FlowElements
                .Where(e => e is IDeclaresUpstreamDependencies && isWanted(e))
                .SelectMany(e =>
                    ((IDeclaresUpstreamDependencies)e)
                        .RequiredUpstreamProperties)
                .Any(required => pipeline.ProvidedBy(required) == element);
        }

        /// <summary>
        /// Returns declared dependency keys that no element provides. When
        /// element metadata is available, the property part is checked too.
        /// </summary>
        /// <param name="pipeline">Pipeline containing the elements.</param>
        /// <returns>Unresolved dependency keys.</returns>
        public static IReadOnlyList<string> UnresolvedUpstreamDependencies(
            this IPipeline pipeline)
        {
            return pipeline.FlowElements
                .OfType<IDeclaresUpstreamDependencies>()
                .SelectMany(e => e.RequiredUpstreamProperties)
                .Where(key => !IsProvided(pipeline, key))
                .Distinct()
                .ToList();
        }

        private static bool IsProvided(IPipeline pipeline, string key)
        {
            var element = pipeline.ProvidedBy(key);
            if (element == null)
            {
                return false;
            }
            var separatorIndex = key.IndexOf('.');
            if (separatorIndex < 0)
            {
                return true;
            }
            var propertyName = key.Substring(separatorIndex + 1);
            var allProperties = pipeline.ElementAvailableProperties;
            if (allProperties == null
                || !allProperties.TryGetValue(
                    element.ElementDataKey, out var elementProperties))
            {
                // Metadata may not be loaded yet, so the property
                // cannot be checked.
                return true;
            }
            return elementProperties.ContainsKey(propertyName);
        }
    }
}
