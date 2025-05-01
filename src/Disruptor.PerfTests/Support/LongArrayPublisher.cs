using System.Threading;
using System.Threading.Tasks;

namespace Disruptor.PerfTests.Support;

public class LongArrayPublisher
{
    private readonly CountdownEvent _countdownEvent;
    private readonly RingBuffer<long[]> _ringBuffer;
    private readonly long _publisherIterations;
    private readonly int _arraySize;

    public LongArrayPublisher(CountdownEvent countdownEvent, RingBuffer<long[]> ringBuffer, long publisherIterations, int arraySize)
    {
        _countdownEvent = countdownEvent;
        _ringBuffer = ringBuffer;
        _publisherIterations = publisherIterations;
        _arraySize = arraySize;
    }

    public Task StartLongRunning()
        => Task.Factory.StartNew(Run, TaskCreationOptions.LongRunning);

    public void Run()
    {
        _countdownEvent.Signal();
        _countdownEvent.Wait();

        for (long i = 0; i < _publisherIterations; i++)
        {
            var sequence = _ringBuffer.Next();
            var eventData = _ringBuffer[sequence];
            for (var j = 0; j < _arraySize; j++)
            {
                eventData[j] = i + j;
            }
            _ringBuffer.Publish(sequence);
        }
    }
}
