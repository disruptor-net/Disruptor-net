using System;

namespace Disruptor.Logging
{
    /// <summary>
    /// Simple console logger implementation
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        /// <summary>
        /// Factory method to create a new <see cref="ConsoleLogger"/>
        /// </summary>
        /// <returns></returns>
        public static ILogger Create()
        {
            return new ConsoleLogger();
        }

        /// <summary>
        /// Logs the specified logging event
        /// </summary>
        /// <param name="level">The level of the message to be logged.</param>
        /// <param name="message">The message object to log.</param>
        /// <param name="ex">the exception to log, including its stack trace. Pass null to not log an exception.</param>
        public void Log(Level level, object message, Exception ex)
        {
            Console.WriteLine("{0} - {1} - Exception: {2}", level, message, ex);
        }
    }
}