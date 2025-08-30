using System;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Processing;

namespace Disruptor.Tests.Support;

public class DummyEventProcessor : IEventProcessor
{
    private readonly EventProcessorState _state = new(new DummySequenceBarrier(), restartable: true);

    public DummyEventProcessor()
        : this(new Sequence())
    {
    }

    public DummyEventProcessor(Sequence sequence)
    {
        Sequence = sequence;
    }

    public Sequence Sequence { get; }
    public TaskCompletionSource? RunTaskCompletionSource { get; set; }

    public Task Halt()
    {
        return _state.Halt();
    }

    public void Dispose()
    {
        _state.Dispose();
    }

    public Task Start(TaskScheduler taskScheduler)
    {
        var runState = _state.Start();
        taskScheduler.StartLongRunningTask(() => Run(runState));
        return runState.StartTask;
    }

    public bool IsRunning => _state.IsRunning;

    public bool IsDisposed => _state.IsDisposed;

    private void Run(EventProcessorState.RunState runState)
    {
        runState.OnStarted();

        RunTaskCompletionSource?.Task.Wait();

        runState.OnShutdown();
    }
}
