using System;

namespace Disruptor.Logging
{
    /// <summary>
    /// Lightweigt logging interface
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Logs the specified logging event
        /// </summary>
        /// <param name="level">The level of the message to be logged.</param>
        /// <param name="message">The message object to log.</param>
        /// <param name="ex">the exception to log, including its stack trace. Pass null to not log an exception.</param>
        void Log(Level level, object message, Exception ex);
    }
}