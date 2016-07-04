using System;
using System.Threading;
using System.Threading.Tasks;

namespace Disruptor.Dsl
{
    /// <summary>
    /// TaskScheduler implementation for IExecutor
    /// </summary>
    public class BasicExecutor : IExecutor
    {
        private readonly TaskScheduler _taskScheduler;

        /// <summary>
        /// Create a new <see cref="BasicExecutor"/> with a given <see cref="TaskScheduler"/>
        /// that will handle low-level queuing of commands execution.
        /// </summary>
        public BasicExecutor(TaskScheduler taskScheduler)
        {
            _taskScheduler = taskScheduler;
        }

        /// <summary>
        /// Start a new task executiong the given command in the current taskscheduler
        /// </summary>
        /// <param name="command"></param>
        public void Execute(Action command)
        {
            Task.Factory.StartNew(command, CancellationToken.None, TaskCreationOptions.None, _taskScheduler);
        }
    }
}