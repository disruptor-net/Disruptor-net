using System.Collections.Generic;
using Disruptor.Tests.Support;
using NUnit.Framework;

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

    public static async IAsyncEnumerable<EventBatch<StubEvent>> TakeEvents(this IAsyncEnumerable<EventBatch<StubEvent>> enumerable, int breakAfterCount)
    {
        var processedCount = 0;

        await foreach (var batch in enumerable)
        {
            yield return batch;

            processedCount += batch.Length;
            if (processedCount >= breakAfterCount)
                break;
        }
    }
}
