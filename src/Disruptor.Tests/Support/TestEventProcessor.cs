using System;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Processing;
using Disruptor.Util;

namespace Disruptor.Tests.Support;

public class TestEventProcessor : IEventProcessor
{
    private readonly EventProcessorState _state = new(restartable: true);

    public TestEventProcessor(SequenceBarrier sequenceBarrier)
    {
        SequenceBarrier = sequenceBarrier;
    }

    public SequenceBarrier SequenceBarrier { get; }
    public Sequence Sequence { get; } = new();

    public bool IsRunning => _state.IsRunning;

    public Task Halt()
    {
        var runState = _state.Halt();
        return runState.ShutdownTask;
    }

    public Task Start(TaskScheduler taskScheduler, TaskCreationOptions taskCreationOptions)
    {
        var runState = _state.Start();
        taskScheduler.ScheduleAndStart(() => Run(runState), taskCreationOptions);
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
