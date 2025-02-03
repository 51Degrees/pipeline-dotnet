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

using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FiftyOne.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Engines.FiftyOne.FlowElements
{
    /// <summary>
    /// 51Degrees specific Engine base class. This adds the concept of license
    /// keys to the standard Engine base class.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/conceptual-overview.md#on-premise-engines">Specification</see>
    /// </summary>
    /// <typeparam name="T">
    /// </typeparam>
    public abstract class FiftyOneOnPremiseAspectEngineBase<T> : 
        OnPremiseAspectEngineBase<T, IFiftyOneAspectPropertyMetaData>, 
        IFiftyOneAspectEngine
        where T : IAspectData
    {
        #region Constructor

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">
        /// The logger to use
        /// </param>
        /// <param name="aspectDataFactory">
        /// The factory function to use when the engine creates an
        /// <see cref="AspectDataBase"/> instance.
        /// </param>
        /// <param name="tempDataFilePath">
        /// The directory to use when storing temporary copies of the 
        /// data file(s) used by this engine.
        /// </param>
        public FiftyOneOnPremiseAspectEngineBase(
            ILogger<FiftyOneOnPremiseAspectEngineBase<T>> logger, 
            Func<IPipeline, FlowElementBase<T, IFiftyOneAspectPropertyMetaData>, T> aspectDataFactory, 
            string tempDataFilePath) : 
            base(logger, aspectDataFactory, tempDataFilePath)
        {
        }

        #endregion

        #region Public Interface
        /// <summary>
        /// The profiles that are present in the data source for this engine.
        /// A Profile is a set of specific property values.
        /// Each profile is associated with a component.
        /// For example, the 'hardwareProfile' component has an 'iPhone 8' 
        /// profile that would contain properties relating to that 
        /// hardware device.
        /// </summary>
        public abstract IEnumerable<IProfileMetaData> Profiles { get; }

        /// <summary>
        /// The components that are present in the data source for this engine.
        /// A 'component' refers to a logical sub-set of the collection 
        /// of properties related to an event (usually an HTTP request).
        /// For example, the 'hardwareProfile' component groups all 
        /// properties related to the hardware device that is being used 
        /// to make the request.
        /// Examples of other components would be: the operating system, 
        /// the browser software, the physical location the request is 
        /// being made from, the ISP serving the request, etc.
        /// </summary>
        public abstract IEnumerable<IComponentMetaData> Components { get; }

        /// <summary>
        /// The values that are present in the data source for this engine.
        /// A 'value' is uniquely identified by the combination of a 
        /// property and a value that property can have.
        /// For example, 'HardwareModel' is a property and a possible 
        /// value could be 'A1234'.
        /// If another property 'HardwareVariants' had the same value of
        /// 'A1234', that would be a separate IValueMetaData instance.
        /// </summary>
        public abstract IEnumerable<IValueMetaData> Values { get; }

        /// <summary>
        /// The default value for the 'Type' parameter when checking for
        /// updates from the 51Degrees 'Distributor' web service.
        /// </summary>
        /// <param name="identifier">
        /// The identifier for the data file that we want to get the 
        /// download type for.
        /// </param>
        /// <returns>
        /// The string value to use for the 'Type' parameter when 
        /// making a request to the 51Degrees Distributor.
        /// </returns>
        public abstract string GetDataDownloadType(string identifier);        
        #endregion
    }
}
