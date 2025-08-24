using System.Threading.Tasks;
using Disruptor.Processing;

namespace Disruptor.Tests.Support;

internal class TestIpcEventProcessor<T> : IIpcEventProcessor<T>
    where T : unmanaged
{
    private readonly EventProcessorState _state;

    public TestIpcEventProcessor(IpcSequenceBarrier sequenceBarrier, SequencePointer sequencePointer)
    {
        _state = new(sequenceBarrier, restartable: true);
        SequenceBarrier = sequenceBarrier;
        SequencePointer = sequencePointer;
    }

    public IpcSequenceBarrier SequenceBarrier { get; }
    public SequencePointer SequencePointer { get; }

    public bool IsRunning => _state.IsRunning;

    public Task Halt()
    {
        return _state.Halt();
    }

    public ValueTask DisposeAsync()
    {
        return new ValueTask(_state.Dispose());
    }

    public Task Start(TaskScheduler taskScheduler)
    {
        var runState = _state.Start();
        taskScheduler.StartLongRunningTask(() => Run(runState));
        return runState.StartTask;
    }

    public void SetExceptionHandler(IValueExceptionHandler<T> exceptionHandler)
    {
    }

    private void Run(EventProcessorState.RunState runState)
    {
        runState.OnStarted();
        SequenceBarrier.WaitForPublishedSequence(0L);
        SequencePointer.SetValue(SequencePointer.Value + 1);
        runState.OnShutdown();
    }
}
