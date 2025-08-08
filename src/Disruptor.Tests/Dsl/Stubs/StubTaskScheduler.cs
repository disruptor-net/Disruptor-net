using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Disruptor.Tests.Dsl.Stubs;

public class StubTaskScheduler : TaskScheduler
{
    private readonly ConcurrentQueue<Thread> _threads = new();
    private ManualResetEvent _executionSignal = new(true);
    private int _taskCount;

    public int TaskCount => _taskCount;

    protected override IEnumerable<Task> GetScheduledTasks()
    {
        return [];
    }

    protected override void QueueTask(Task task)
    {
        Interlocked.Increment(ref _taskCount);

        var executionSignal = Volatile.Read(ref _executionSignal);

        var thread = new Thread(ExecuteTask);
        thread.Start();
        _threads.Enqueue(thread);

        void ExecuteTask()
        {
            executionSignal.WaitOne();
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

    public bool JoinAllThreads(int millisecondsTimeout)
    {
        while (_threads.TryDequeue(out var thread))
        {
            if (!thread.Join(millisecondsTimeout))
                return false;
        }

        return true;
    }

    public IDisposable SuspendExecutions()
    {
        var executionSignal = new ManualResetEvent(false);
        _executionSignal = executionSignal;

        return new SuspendExecutionsScope(executionSignal);
    }

    private class SuspendExecutionsScope(ManualResetEvent executionSignal) : IDisposable
    {
        public void Dispose()
        {
            executionSignal.Set();
        }
    }
}
