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
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FiftyOne.Pipeline.Core.FlowElements
{
    /// <summary>
    /// Emits a tracing span for each flow element a pipeline processes.
    /// Consumers opt in by listening to the
    /// <see cref="Constants.TRACING_SOURCE_NAME"/> activity source.
    /// </summary>
    internal static class ElementTracing
    {
        internal static readonly ActivitySource Source =
            new ActivitySource(
                Constants.TRACING_SOURCE_NAME,
                typeof(ElementTracing).Assembly.GetName().Version?.ToString());

        /// <summary>
        /// The span name and tags of one element, resolved once: they
        /// cannot change over the element's lifetime.
        /// </summary>
        private sealed class ElementNames
        {
            public string SpanName;
            public KeyValuePair<string, object>[] Tags;
        }

        private static readonly
            ConditionalWeakTable<IFlowElement, ElementNames> _names =
                new ConditionalWeakTable<IFlowElement, ElementNames>();

        // Held in a field because the project compiles as C# 7.3, which
        // has no static method group delegate caching: passing Resolve
        // directly would allocate a delegate on every traced element.
        private static readonly
            ConditionalWeakTable<IFlowElement, ElementNames>.CreateValueCallback
                _resolveCallback = Resolve;

        /// <summary>
        /// Start a span for the element about to be processed. Returns
        /// null when nothing listens to the source, so tracing that is
        /// switched off costs nothing.
        /// </summary>
        /// <param name="element">
        /// The element about to be processed.
        /// </param>
        /// <returns>
        /// The started activity, or null when tracing is off or the
        /// element is a <see cref="ParallelElements"/>.
        /// </returns>
        internal static Activity StartElement(IFlowElement element)
        {
            if (Source.HasListeners() == false)
            {
                return null;
            }
            // ParallelElements has no data key of its own; its children
            // produce their own spans instead.
            if (element is ParallelElements)
            {
                return null;
            }
            var names = _names.GetValue(element, _resolveCallback);
            // Tags handed over at creation are visible to samplers and
            // cost no work per request.
            return Source.StartActivity(
                names.SpanName,
                ActivityKind.Internal,
                default(ActivityContext),
                names.Tags);
        }

        private static ElementNames Resolve(IFlowElement element)
        {
            string dataKey;
            try
            {
                dataKey = element.ElementDataKey;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                // Tracing stays observational: an element whose data key
                // getter throws must not start failing processing once a
                // listener attaches.
                dataKey = element.GetType().Name;
            }
            return new ElementNames
            {
                SpanName = "element." + dataKey,
                Tags = new[]
                {
                    new KeyValuePair<string, object>(
                        "element.type", element.GetType().Name),
                    new KeyValuePair<string, object>(
                        "element.data_key", dataKey),
                },
            };
        }
    }
}
