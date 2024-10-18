using FiftyOne.Pipeline.Core.Exceptions;
using System;

namespace FiftyOne.Pipeline.CloudRequestEngine
{
    /// <summary>
    /// Indicates that cloud request engine can not currently
    /// send a request to the remote cloud server.
    /// </summary>
    public class CloudRequestEngineTemporarilyUnavailableException : PipelineTemporarilyUnavailableException
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public CloudRequestEngineTemporarilyUnavailableException() : base() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">
        /// The exception message
        /// </param>
        public CloudRequestEngineTemporarilyUnavailableException(
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
        public CloudRequestEngineTemporarilyUnavailableException(
            string message,
            Exception innerException) :
            base(message, innerException)
        { }
    }
}
