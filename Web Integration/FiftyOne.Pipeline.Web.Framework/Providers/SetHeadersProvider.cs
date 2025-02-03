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

using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FiftyOne.Pipeline.Web.Framework.Providers
{
    /// <summary>
    /// This class handles setting HTTP headers in the response based
    /// on values from the <see cref="SetHeadersElement"/>
    /// </summary>
    public class SetHeadersProvider
    {
        /// <summary>
        /// The single instance of the provider.
        /// </summary>
        private static SetHeadersProvider _instance = null;

        /// <summary>
        /// Lock used when constructing the instance.
        /// </summary>
        private static readonly object _lock = new object();

        /// <summary>
        /// The set headers flow element that generates the dictionary
        /// of response header values.
        /// </summary>
        private ISetHeadersElement _setHeadersElement;

        /// <summary>
        /// Get the single instance of the provider. If one does not yet
        /// exist, it is constructed.
        /// </summary>
        /// <returns></returns>
        public static SetHeadersProvider GetInstance()
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new SetHeadersProvider();
                    }
                }
            }
            return _instance;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public SetHeadersProvider()
        {
            var pipeline = WebPipeline.GetInstance().Pipeline;
            _setHeadersElement = pipeline.GetElement<ISetHeadersElement>();
        }

        /// <summary>
        /// Set the HTTP headers in the response based
        /// on values from the <see cref="SetHeadersElement"/>
        /// </summary>
        /// <param name="flowData">
        /// The flow data containing the information about the headers to set.
        /// </param>
        /// <param name="context">
        /// The HTTP context
        /// </param>
        public void SetHeaders(IFlowData flowData, HttpContext context)
        {
            if (flowData == null) throw new ArgumentNullException(nameof(flowData));
            if (context == null) throw new ArgumentNullException(nameof(context));

            if (_setHeadersElement != null)
            {
                var headersToSet = flowData
                    .GetFromElement(_setHeadersElement).ResponseHeaderDictionary;
                SetHeaders(context, headersToSet);
            }
        }

        /// <summary>
        /// Set the HTTP headers in the response based
        /// on values in the supplied headersToSet parameter.
        /// </summary>
        /// <param name="context">
        /// The HTTP context
        /// </param>
        /// <param name="headersToSet">
        /// A dictionary containing the names and values of the headers 
        /// to set.
        /// </param>
        public static void SetHeaders(HttpContext context, 
            IReadOnlyDictionary<string,string> headersToSet)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (headersToSet == null) throw new ArgumentNullException(nameof(headersToSet));

            if (context.Response.HeadersWritten == false)
            {
                foreach (var header in headersToSet)
                {
                    if (context.Response.Headers.AllKeys.Contains(header.Key))
                    {
                        context.Response.Headers[header.Key] += $",{header.Value}";
                    }
                    else
                    {
                        context.Response.Headers.Add(header.Key, header.Value);
                    }
                }
            }
        }
    }
}