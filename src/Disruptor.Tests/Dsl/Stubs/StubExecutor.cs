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
        private static readonly Task _completedTask = Task.FromResult(0);
        private readonly ConcurrentQueue<Thread> _threads = new ConcurrentQueue<Thread>();
        private bool _ignoreExecutions;
        private int _executionCount;

        public Task Execute(Action command)
        {
            Interlocked.Increment(ref _executionCount);

            if (!Volatile.Read(ref _ignoreExecutions))
            {
                var t = new Thread(() =>
                {
                    try
                    {
                        command();
                    }
                    catch (ThreadInterruptedException e)
                    {
                        Console.WriteLine(e);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                });
                _threads.Enqueue(t);
                t.Start();
            }

            return _completedTask;
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
            _ignoreExecutions = true;
        }

        public int GetExecutionCount()
        {
            return Volatile.Read(ref _executionCount);
        }
    }
}
