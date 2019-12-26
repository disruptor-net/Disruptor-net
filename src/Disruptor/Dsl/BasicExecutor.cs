using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private readonly List<Thread> _threads = new List<Thread>();

        /// <summary>
        /// Create a new <see cref="BasicExecutor"/> with a given <see cref="TaskScheduler"/>
        /// that will handle low-level queuing of commands execution.
        /// </summary>
        public BasicExecutor(TaskScheduler taskScheduler)
        {
            _taskScheduler = taskScheduler;
        }

        public int ThreadCount
        {
            get
            {
                lock (_threads)
                {
                    return _threads.Count;
                }
            }
        }

        /// <summary>
        /// Start a new task executing the given command in the current TaskScheduler.
        /// </summary>
        /// <param name="command"></param>
        public Task Execute(Action command)
        {
            return Task.Factory.StartNew(() => ExecuteCommand(command), CancellationToken.None, TaskCreationOptions.LongRunning, _taskScheduler);
        }

        private void ExecuteCommand(Action command)
        {
            var currentThread = Thread.CurrentThread;
            lock (_threads)
            {
                _threads.Add(currentThread);
            }
            try
            {
                command.Invoke();
            }
            finally
            {
                lock (_threads)
                {
                    _threads.Remove(currentThread);
                }
            }
        }

        public override string ToString()
        {
            return $"BasicExecutor {{ThreadCount={ThreadCount}}}";
        }
    }
}
