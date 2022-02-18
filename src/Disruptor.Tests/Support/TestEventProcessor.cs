using System;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Processing;
using Disruptor.Util;

namespace Disruptor.Tests.Support;

public class TestEventProcessor : IEventProcessor
{
    private readonly ManualResetEventSlim _runEvent = new();
    private readonly ISequenceBarrier _sequenceBarrier;
    private volatile int _running;

    public TestEventProcessor(ISequenceBarrier sequenceBarrier)
    {
        _sequenceBarrier = sequenceBarrier;
    }

    public ISequence Sequence { get; } = new Sequence();

    public void WaitUntilStarted(TimeSpan timeout)
    {
        _runEvent.Wait();
    }

    public bool IsRunning => _running != 0;

    public void Halt()
    {
        _running = 0;
        _runEvent.Reset();
    }

    public Task Start(TaskScheduler taskScheduler, TaskCreationOptions taskCreationOptions)
    {
        return taskScheduler.ScheduleAndStart(Run, taskCreationOptions);
    }

    public void Run()
    {
        if (Interlocked.Exchange(ref _running, 1) != 0)
            throw new InvalidOperationException("Already running");

        _runEvent.Set();
        _sequenceBarrier.WaitFor(0L);
        Sequence.SetValue(Sequence.Value + 1);
    }
}
