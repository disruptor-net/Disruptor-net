using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using NUnit.Framework;

namespace Disruptor.Tests.Dsl.Stubs
{
    public class StubExecutor : IExecutor
    {
        private readonly ConcurrentQueue<Thread> _threads = new ConcurrentQueue<Thread>();
        private bool _ignoreExecutions;
        private int _executionCount;

        public Task Execute(Action command)
        {
            Interlocked.Increment(ref _executionCount);

            if (!Volatile.Read(ref _ignoreExecutions))
            {
                var t = new Thread(() => command());
                _threads.Enqueue(t);
                t.Start();
            }

            return Task.CompletedTask;
        }

        public void JoinAllThreads()
        {
            Thread thread;
            while (_threads.TryDequeue(out thread))
            {
                if (thread.IsAlive)
                {
                    try
                    {
                        thread.Interrupt();
                        thread.Join(5000);
                    }
                    catch (ThreadInterruptedException e)
                    {
                        Console.WriteLine(e);
                    }
                }

                Assert.IsFalse(thread.IsAlive, "Failed to stop thread: " + thread);
            }
        }

        public void IgnoreExecutions()
        {
            _ignoreExecutions = true;
        }

        public int GetExecutionCount()
        {
            return Volatile.Read(ref _executionCount);
        }
    }
}