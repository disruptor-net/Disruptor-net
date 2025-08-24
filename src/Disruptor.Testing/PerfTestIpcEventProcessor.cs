using System.Threading.Tasks;
using Disruptor.Processing;

namespace Disruptor;

public class PerfTestIpcEventProcessor<T>
    where T : unmanaged
{
    private readonly IIpcEventProcessor<T> _eventProcessor;

    internal PerfTestIpcEventProcessor(IIpcEventProcessor<T> eventProcessor)
    {
        _eventProcessor = eventProcessor;
    }

    public SequencePointer SequencePointer => _eventProcessor.SequencePointer;

    public Task Start() => _eventProcessor.Start();

    public Task Halt() => _eventProcessor.Halt();
}
