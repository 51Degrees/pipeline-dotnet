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

using FiftyOne.Pipeline.Core.Attributes;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FiftyOne.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using System;

namespace FiftyOne.Pipeline.Engines.FiftyOne.FlowElements
{
    /// <summary>
    /// A base class for engine builders for on-premise engines that use 
    /// a single data file and use the 51Degrees Distributor web service to 
    /// check for and download data updates.
    /// </summary>
    /// <typeparam name="TBuilder">
    /// The specific type of the builder.
    /// </typeparam>
    /// <typeparam name="TEngine">
    /// The type of the engine that this builder builds.
    /// </typeparam>
    public abstract class FiftyOneOnPremiseAspectEngineBuilderBase<TBuilder, TEngine> :
        SingleFileAspectEngineBuilderBase<TBuilder, TEngine>
        where TBuilder : FiftyOneOnPremiseAspectEngineBuilderBase<TBuilder, TEngine>
        where TEngine : IFiftyOneAspectEngine
    {
        private string _dataDownloadType;

        /// <summary>
        /// The default value to use for the 'Type' parameter when sending
        /// a request to the Distributor
        /// </summary>
        protected abstract string DefaultDataDownloadType { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="dataUpdateService"></param>
        public FiftyOneOnPremiseAspectEngineBuilderBase(
            IDataUpdateService dataUpdateService) : 
            base(dataUpdateService)
        {
            SetDataUpdateUrl(new Uri("https://distributor.51degrees.com/api/v2/download"));
            SetDataUpdateUrlFormatter(new FiftyOneUrlFormatter());
        }

        /// <summary>
        /// Set the 'type' string that will be sent to the 'distributor' 
        /// service when downloading a new data file.
        /// Note that this is only needed if using UpdateOnStartup. 
        /// Otherwise, the update service will use the type name from the 
        /// existing data file.
        /// The default value is provided by the specific engine builder 
        /// implementation.
        /// </summary>
        /// <param name="typeName">
        /// The download type to use. For example 'HashV4'.
        /// </param>
        /// <returns>
        /// This builder.
        /// </returns>
        [DefaultValue("Set by subclass")]
        public TBuilder SetDataDownloadType(string typeName)
        {
            _dataDownloadType = typeName;
            return this as TBuilder;
        }

        /// <summary>
        /// This method is called when the builder needs to create a new 
        /// <see cref="AspectEngineDataFile"/> instance.
        /// </summary>
        /// <returns>
        /// A new <see cref="FiftyOneDataFile"/> instance with the 
        /// DataDownloadType property set based on the configuration
        /// of this builder.
        /// </returns>
        protected override AspectEngineDataFile NewAspectEngineDataFile()
        {
            return new FiftyOneDataFile(typeof(TEngine))
            {
                DataDownloadType = _dataDownloadType ?? DefaultDataDownloadType
            };
        }
    }
}
