using System;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Processing;

namespace Disruptor.Tests.Support;

public class DummyEventProcessor : IEventProcessor
{
    private readonly EventProcessorState _state = new(restartable: true);


    public DummyEventProcessor()
        : this(new Sequence())
    {
    }

    public DummyEventProcessor(Sequence sequence)
    {
        Sequence = sequence;
    }

    public Sequence Sequence { get; }

    public Task Halt()
    {
        var runState = _state.Halt();
        return runState.ShutdownTask;
    }

    public Task Start(TaskScheduler taskScheduler)
    {
        var runState = _state.Start();
        taskScheduler.StartLongRunningTask(() => Run(runState));
        return runState.StartTask;
    }

    public bool IsRunning => _state.IsRunning;

    private void Run(EventProcessorState.RunState runState)
    {
        runState.OnStarted();
        runState.OnShutdown();
    }
}
