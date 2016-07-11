using System;
using System.Collections.Concurrent;
using System.Threading;
using Disruptor.Dsl;
using NUnit.Framework;

namespace Disruptor.Tests.Dsl.Stubs
{
    public class StubExecutor : IExecutor
    {
        private readonly ConcurrentBag<Thread> _threads = new ConcurrentBag<Thread>();
        private bool _ignoreExecutions;
        private int _executionCount;

        public void Execute(Action command)
        {
            Interlocked.Increment(ref _executionCount);
            if (!Volatile.Read(ref _ignoreExecutions))
            {
                var t = new Thread(() => command());
                //t.Name = command.toString();
                _threads.Add(t);
                t.Start();
            }
        }

        public void JoinAllThreads()
        {
            foreach (Thread thread in _threads)
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
                        System.Console.WriteLine(e);
                    }
                }

                Assert.IsFalse(thread.IsAlive, "Failed to stop thread: " + thread);
            }

            Thread t;
            while (_threads.TryTake(out t))
            {
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