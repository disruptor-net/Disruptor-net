using System.Threading.Tasks;
using Disruptor.Processing;

namespace Disruptor.Tests.Support;

internal class TestIpcEventProcessor<T> : IIpcEventProcessor<T>
    where T : unmanaged
{
    private readonly EventProcessorState _state = new(restartable: true);

    public TestIpcEventProcessor(IpcSequenceBarrier sequenceBarrier, SequencePointer sequencePointer)
    {
        SequenceBarrier = sequenceBarrier;
        SequencePointer = sequencePointer;
    }

    public IpcSequenceBarrier SequenceBarrier { get; }
    public SequencePointer SequencePointer { get; }

    public bool IsRunning => _state.IsRunning;

    public Task Halt()
    {
        var runState = _state.Halt();
        if (runState != null)
        {
            return runState.ShutdownTask;
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        var runState = _state.Dispose();
        if (runState != null)
        {
            return new ValueTask(runState.ShutdownTask);
        }

        return default;
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
