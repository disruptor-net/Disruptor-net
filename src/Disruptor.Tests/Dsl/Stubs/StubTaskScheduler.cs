using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Disruptor.Tests.Dsl.Stubs
{
    public class StubTaskScheduler : TaskScheduler
    {
        private readonly ConcurrentQueue<Thread> _threads = new();
        private bool _ignoreTasks;
        private int _taskCount;

        public int TaskCount => _taskCount;

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return Enumerable.Empty<Task>();
        }

        protected override void QueueTask(Task task)
        {
            Interlocked.Increment(ref _taskCount);

            if (Volatile.Read(ref _ignoreTasks))
                return;

            var thread = new Thread(ExecuteTask);
            thread.Start();
            _threads.Enqueue(thread);

            void ExecuteTask()
            {
                try
                {
                    TryExecuteTask(task);
                }
                catch (ThreadInterruptedException e)
                {
                    Console.WriteLine(e);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return false;
        }

        public void JoinAllThreads()
        {
            while (_threads.TryDequeue(out var thread))
            {
                if (!thread.Join(5000))
                {
                    thread.Interrupt();
                    Assert.IsTrue(thread.Join(5000), "Failed to stop thread: " + thread);
                }
            }
        }

        public void IgnoreExecutions()
        {
            _ignoreTasks = true;
        }
    }
}
