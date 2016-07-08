using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Disruptor.Dsl;
using NUnit.Framework;

namespace Disruptor.Tests.Dsl.Stubs
{
    public class StubExecutor : IExecutor
    {
        private readonly ConcurrentBag<Thread> _threads = new ConcurrentBag<Thread>();
        private Volatile.Boolean _ignoreExecutions = new Volatile.Boolean(false);
        private Volatile.Integer _executionCount = new Volatile.Integer(0);

        public void Execute(Action command)
        {
            _executionCount.AtomicIncrementAndGet();
            if (! _ignoreExecutions.ReadFullFence())
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
            _ignoreExecutions.WriteFullFence(true);
        }

        public int GetExecutionCount()
        {
            return _executionCount.ReadUnfenced();
        }
    }
}