using System;
using System.Collections.Generic;
using System.Text;

namespace FiftyOne.Pipeline.Engines.FlowElements
{
    /// <summary>
    /// Defines a translation from one property to another. 
    /// Used in <see cref="TranslationEngine"/>
    /// </summary>
    public class Translation : ITranslation
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="source"></param>
        /// <param name="destination"></param>
        public Translation(string source, string destination)
        {
            SourceProperty = source;
            DestinationProperty = destination;
        }

        /// <inheritdoc/>
        public string SourceProperty { get; set; }

        /// <inheritdoc/>
        public string DestinationProperty { get; set; }
    }
}
