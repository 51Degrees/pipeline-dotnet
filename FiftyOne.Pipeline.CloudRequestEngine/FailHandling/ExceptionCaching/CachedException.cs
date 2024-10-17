using System;
using System.Collections.Generic;
using System.Text;

namespace FiftyOne.Pipeline.CloudRequestEngine.FailHandling.ExceptionCaching
{
    /// <summary>
    /// Links the exception and the timestamp.
    /// </summary>
    public class CachedException
    {
        private readonly Exception _exception;
        private readonly DateTime _dateTime;

        /// <summary>
        /// The exception that did happen.
        /// </summary>
        public Exception Exception => _exception;

        /// <summary>
        /// When the exception did happen.
        /// </summary>
        public DateTime DateTime => _dateTime;

        /// <summary>
        /// Designated constructor.
        /// </summary>
        /// <param name="exception">
        /// The exception.
        /// </param>
        public CachedException(Exception exception)
        {
            _exception = exception;
            _dateTime = DateTime.Now;
        }   
    }
}
