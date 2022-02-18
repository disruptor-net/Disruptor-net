using System;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Processing;

namespace Disruptor.Tests.Support;

public class DummyEventProcessor : IEventProcessor
{
    private readonly ManualResetEventSlim _runEvent = new();
    private int _running;


    public DummyEventProcessor()
        : this(new Sequence())
    {
    }

    public DummyEventProcessor(ISequence sequence)
    {
        Sequence = sequence;
    }

    public ISequence Sequence { get; }

    public void Halt()
    {
        Thread.VolatileWrite(ref _running, 1);
        _runEvent.Reset();
    }

    public Task Start(TaskScheduler taskScheduler, TaskCreationOptions taskCreationOptions)
    {
        return taskScheduler.ScheduleAndStart(Run, taskCreationOptions);
    }

    public void WaitUntilStarted(TimeSpan timeout)
    {
        _runEvent.Wait();
    }

    public bool IsRunning => Thread.VolatileRead(ref _running) == 1;

    public void Run()
    {
        if (Interlocked.Exchange(ref _running, 1) != 0)
            throw new InvalidOperationException("Already running");

        _runEvent.Set();
    }
}
