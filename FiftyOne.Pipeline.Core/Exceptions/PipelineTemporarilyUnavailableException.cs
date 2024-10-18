
using System;

namespace FiftyOne.Pipeline.Core.Exceptions
{
    /// <summary>
    /// Indicates that pipeline refused to process 
    /// <see cref="Data.IFlowData"/>
    /// based on the internal state of some flow elements.
    /// </summary>
    public class PipelineTemporarilyUnavailableException: PipelineException
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public PipelineTemporarilyUnavailableException() : base() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">
        /// The exception message
        /// </param>
        public PipelineTemporarilyUnavailableException(
            string message)
            : base(message)
        { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">
        /// The exception message
        /// </param>
        /// <param name="innerException">
        /// The inner exception that triggered this exception.
        /// </param>
        public PipelineTemporarilyUnavailableException(
            string message,
            Exception innerException) :
            base(message, innerException)
        { }
    }
}