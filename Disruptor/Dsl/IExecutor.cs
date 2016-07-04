using System;

namespace Disruptor.Dsl
{
    /// <summary>
    /// Replace the Executor interface in java.util.concurrent
    /// </summary>
    public interface IExecutor
    {
        /// <summary>
        /// Execute the given command in an other thread
        /// </summary>
        /// <param name="command">The command to execute</param>
        void Execute(Action command);
    }
}