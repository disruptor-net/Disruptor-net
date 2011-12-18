namespace Disruptor.Logging
{
    /// <summary>
    /// Log levels (inspired by log4net)
    /// </summary>
    public enum Level
    {
        ///<summary>
        /// The Fatal level designates very severe error events that will presumably lead the application to abort. 
        ///</summary>
        Fatal,
        ///<summary>
        /// The Error level designates error events that might still allow the application to continue running. 
        ///</summary>
        Error,
        ///<summary>
        /// The Warn level designates potentially harmful situations. 
        ///</summary>
        Warn,
        ///<summary>
        /// The Info level designates informational messages that highlight the progress of the application at coarse-grained level. 
        ///</summary>
        Info,
        ///<summary>
        /// The Debug level designates fine-grained informational events that are most useful to debug an application. 
        ///</summary>
        Debug
    }
}