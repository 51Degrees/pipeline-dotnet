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

using FiftyOne.Pipeline.Core.Data;
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
        /// Finds the element that provides the given "vendor.Property" key,
        /// or null if none does.
        /// </summary>
        public static IFlowElement ProvidedBy(
            this IPipeline pipeline, string propertyKey)
        {
            var elementKey = propertyKey.Split('.')[0];
            return pipeline.FlowElements.FirstOrDefault(e =>
                string.Equals(
                    e.ElementDataKey, elementKey,
                    StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// True if the element is wanted directly, or if a wanted element
        /// declared that it needs one of this element's properties.
        /// <paramref name="isWanted"/> answers whether an element is wanted.
        /// </summary>
        public static bool IsNeededFor(
            this IFlowData data,
            IFlowElement element,
            Func<IFlowElement, bool> isWanted)
        {
            if (isWanted(element))
            {
                return true;
            }
            return data.Pipeline.FlowElements
                .Where(e => e is IDeclaresUpstreamDependencies && isWanted(e))
                .SelectMany(e =>
                    ((IDeclaresUpstreamDependencies)e)
                        .RequiredUpstreamProperties)
                .Any(required =>
                    data.Pipeline.ProvidedBy(required) == element);
        }

        /// <summary>
        /// Returns declared dependency keys that no element provides.
        /// </summary>
        public static IReadOnlyList<string> UnresolvedUpstreamDependencies(
            this IPipeline pipeline)
        {
            return pipeline.FlowElements
                .OfType<IDeclaresUpstreamDependencies>()
                .SelectMany(e => e.RequiredUpstreamProperties)
                .Where(key => pipeline.ProvidedBy(key) == null)
                .Distinct()
                .ToList();
        }
    }
}
