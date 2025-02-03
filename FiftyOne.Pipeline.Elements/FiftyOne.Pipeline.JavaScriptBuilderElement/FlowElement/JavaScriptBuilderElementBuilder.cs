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

using FiftyOne.Pipeline.JavaScriptBuilder.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging;
using System;
using System.Text.RegularExpressions;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.Data;
using System.Globalization;
using FiftyOne.Pipeline.Core.Attributes;

namespace FiftyOne.Pipeline.JavaScriptBuilder.FlowElement
{
    /// <summary>
    /// Builder for the <see cref="JavaScriptBuilderElement"/>
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/pipeline-elements/javascript-builder.md">Specification</see>
    /// </summary>
    public class JavaScriptBuilderElementBuilder
    {
        /// <summary>
        /// Logger factory that may be reused in descendants to produce a distinct logger.
        /// </summary>
        protected ILoggerFactory _loggerFactory;
        private ILogger<JavaScriptBuilderElementData> _dataLogger; 

        /// <summary>
        /// The logger for this instance.
        /// </summary>
        protected ILogger Logger { get; private set; }
        
        /// <summary>
        /// The host name to use when creating a callback URL.
        /// </summary>
        protected string Host { get; private set;} = Constants.BUILDER_DEFAULT_HOST;
        /// <summary>
        /// The end point (i.e. the relative URL) to use when creating 
        /// a callback URL.
        /// </summary>
        protected string Endpoint { get; private set; } = Constants.BUILDER_DEFAULT_ENDPOINT;
        /// <summary>
        /// The protocol to use when creating a callback URL.
        /// </summary>
        protected string Protocol { get; private set; } = Constants.BUILDER_DEFAULT_PROTOCOL;
        /// <summary>
        /// The name of the JavaScript object that will be created.
        /// </summary>
        protected string ObjName { get; private set; } = Constants.BUILDER_DEFAULT_OBJECT_NAME;
        /// <summary>
        /// If set to false, the JavaScript will not populateany cookies, and will instead use
        /// session storage.
        /// </summary>
        protected bool EnableCookies { get; private set; } = Constants.BUILDER_DEFAULT_ENABLE_COOKIES;

        /// <summary>
        /// If set to true, the JavaScript output will be minified
        /// </summary>
        protected bool Minify { get; private set; } = Constants.BUILDER_DEFAULT_MINIFY;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="loggerFactory">
        /// The logger factory.
        /// </param>
        public JavaScriptBuilderElementBuilder(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            Logger = _loggerFactory.CreateLogger<JavaScriptBuilderElementBuilder>();
            _dataLogger = _loggerFactory.CreateLogger<JavaScriptBuilderElementData>();
        }

        /// <summary>
        /// Set whether the client JavaScript stored results of client side
        /// processing in cookies.
        /// This can also be set per request, using the "query.fod-js-enable-cookies"
        /// evidence key.
        /// For more details on personal data policy, see http://51degrees.com/terms/client-services-privacy-policy/
        /// </summary>
        /// <param name="enableCookies">Should enable cookies?</param>
        /// <returns></returns>
        [DefaultValue(Constants.BUILDER_DEFAULT_ENABLE_COOKIES)]
        public JavaScriptBuilderElementBuilder SetEnableCookies(bool enableCookies)
        {
            EnableCookies = enableCookies;
            return this;
        }

        /// <summary>
        /// Set the host that the client JavaScript should query for updates.
        /// By default, the host from the request will be used.
        /// </summary>
        /// <param name="host">The host name.</param>
        /// <returns></returns>
        [DefaultValue(Constants.BUILDER_DEFAULT_HOST)]
        public JavaScriptBuilderElementBuilder SetHost(string host)
        {
            Host = host;
            return this;
        }

        /// <summary>
        /// Set the endpoint which will be queried on the host. e.g /api/v4/json
        /// </summary>
        /// <param name="endpoint">The endpoint.</param>
        /// <returns></returns>
        [DefaultValue(Constants.BUILDER_DEFAULT_ENDPOINT)]
        public JavaScriptBuilderElementBuilder SetEndpoint(string endpoint)
        {
            Endpoint = endpoint;
            return this;
        }

        /// <summary>
        /// The protocol that the client JavaScript will use when 
        /// querying for updates.
        /// By default, the protocol from the request will be used.
        /// </summary>
        /// <param name="protocol">The protocol to use (http / https)</param>
        /// <returns></returns>
        [DefaultValue(Constants.BUILDER_DEFAULT_PROTOCOL)]
        public JavaScriptBuilderElementBuilder SetProtocol(string protocol)
        {
            if (string.Equals(protocol, "http", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(protocol, "https", StringComparison.OrdinalIgnoreCase))
            {
                Protocol = protocol;
            }
            else
            {
                throw new PipelineConfigurationException(
                    $"Invalid protocol in configuration ({protocol}), " +
                    $"must be 'http' or https'");
            }
            return this;
        }

        /// <summary>
        /// The default name of the object instantiated by the client 
        /// JavaScript.
        /// </summary>
        /// <param name="objName"></param>
        /// <returns></returns>
        [DefaultValue(Constants.BUILDER_DEFAULT_OBJECT_NAME)]
        public JavaScriptBuilderElementBuilder SetObjectName(string objName)
        {
            var match = Regex.Match(objName, @"[a-zA-Z_$][0-9a-zA-Z_$]*");

            if (match.Value == objName)
            {
                ObjName = objName;
            }
            else
            {
                var msg = string.Format(CultureInfo.InvariantCulture,
                    Messages.ExceptionObjectNameInvalid,
                    objName);
                Logger.LogCritical(msg);
                throw new PipelineConfigurationException(msg);
            }

            return this;
        }

        /// <summary>
        /// Enable or disable minification of the JavaScript that is 
        /// produced by the <see cref="JavaScriptBuilderElement"/>.
        /// </summary>
        /// <remarks>
        /// The <code>NUglify</code> package is used to minify the
        /// output.
        /// </remarks>
        /// <param name="minify">
        /// True to minify the output. False to not.
        /// </param>
        /// <returns>
        /// This builder.
        /// </returns>
        [DefaultValue(Constants.BUILDER_DEFAULT_MINIFY)]
        public JavaScriptBuilderElementBuilder SetMinify(bool minify)
        {
            Minify = minify;
            return this;
        }

        /// <summary>
        /// Build the element.
        /// </summary>
        /// <returns></returns>
        public virtual JavaScriptBuilderElement Build()
        {
            return new JavaScriptBuilderElement(_loggerFactory.CreateLogger<JavaScriptBuilderElement>(),
                CreateData,
                Endpoint,
                ObjName,
                EnableCookies,
                Minify, 
                Host, 
                Protocol);
        }

        private IJavaScriptBuilderElementData CreateData(
            IPipeline pipeline,
            FlowElementBase<IJavaScriptBuilderElementData, IElementPropertyMetaData> 
                javaScriptBuilderElement)
        {
            return new JavaScriptBuilderElementData(
                _dataLogger,
                pipeline);
        }
    }
}
