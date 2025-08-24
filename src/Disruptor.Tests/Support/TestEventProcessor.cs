using System;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Processing;
using Disruptor.Util;

namespace Disruptor.Tests.Support;

public class TestEventProcessor : IEventProcessor
{
    private readonly EventProcessorState _state;

    public TestEventProcessor(SequenceBarrier sequenceBarrier)
    {
        _state = new(sequenceBarrier, restartable: true);
        SequenceBarrier = sequenceBarrier;
    }

    public SequenceBarrier SequenceBarrier { get; }
    public Sequence Sequence { get; } = new();

    public bool IsRunning => _state.IsRunning;

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

    private void Run(EventProcessorState.RunState runState)
    {
        runState.OnStarted();
        SequenceBarrier.WaitForPublishedSequence(0L);
        Sequence.SetValue(Sequence.Value + 1);
        runState.OnShutdown();
    }
}
