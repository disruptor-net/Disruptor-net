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

        /// <summary>
        /// Start a new task executiong the given command in the current taskscheduler
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
            return "BasicExecutor{" +
                   "threads=" + DumpThreadInfo() +
                   "}";
        }

        private string DumpThreadInfo()
        {
            List<Thread> threads;
            lock (_threads)
            {
                threads = _threads.ToList();
            }
            var sb = new StringBuilder();
            foreach (var t in threads)
            {
                sb.Append("{");
                sb.Append("name=").Append(t.Name).Append(",");
                sb.Append("id=").Append(t.ManagedThreadId).Append(",");
                sb.Append("state=").Append(t.ThreadState).Append(",");
                sb.Append("}");
            }
            return sb.ToString();
        }
    }
}