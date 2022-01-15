using Disruptor.Tests.Support;

namespace Disruptor.Tests;

public static class TestsExtensions
{
    public static void PublishStubEvent(this RingBuffer<StubEvent> ringBuffer, int value)
    {
        var sequence = ringBuffer.Next();
        try
        {
            ringBuffer[sequence].Value = value;
        }
        finally
        {
            ringBuffer.Publish(sequence);
        }
    }

    public static void PublishStubEvent(this ValueRingBuffer<StubValueEvent> ringBuffer, int value)
    {
        var sequence = ringBuffer.Next();
        try
        {
            ringBuffer[sequence].Value = value;
        }
        finally
        {
            ringBuffer.Publish(sequence);
        }
    }
}