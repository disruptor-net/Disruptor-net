using System;
using System.Runtime.InteropServices;

namespace Disruptor.Samples.Wiki.GettingStarted;

public class SampleEventProducer
{
    private readonly RingBuffer<SampleEvent> _ringBuffer;

    public SampleEventProducer(RingBuffer<SampleEvent> ringBuffer)
    {
        _ringBuffer = ringBuffer;
    }

    public void ProduceUsingRawApi(ReadOnlyMemory<byte> input)
    {
        var id = MemoryMarshal.Read<int>(input.Span);
        var value = MemoryMarshal.Read<double>(input.Span.Slice(4));

        // (1) Claim the next sequence
        var sequence = _ringBuffer.Next();
        try
        {
            // (2) Get and configure the event for the sequence
            var data = _ringBuffer[sequence];
            data.Initialize(id, value, DateTime.UtcNow);
        }
        finally
        {
            // (3) Publish the event
            _ringBuffer.Publish(sequence);
        }
    }

    public void ProduceUsingScope(ReadOnlyMemory<byte> input)
    {
        var id = MemoryMarshal.Read<int>(input.Span);
        var value = MemoryMarshal.Read<double>(input.Span.Slice(4));

        using (var scope = _ringBuffer.PublishEvent())
        {
            var data = scope.Event();
            data.Initialize(id, value, DateTime.UtcNow);

            // The event is published at the end of the scope
        }
    }

    public void ProduceUsingCustomWaitStrategy(ReadOnlyMemory<byte> input)
    {
        var id = MemoryMarshal.Read<int>(input.Span);
        var value = MemoryMarshal.Read<double>(input.Span.Slice(4));

        // Claim the next sequence
        var sequence = _ringBuffer.Next();
        try
        {
            // Get the event for the sequence
            var data = _ringBuffer[sequence];

            // Configure the event
            data.Initialize(id, value, DateTime.UtcNow);
        }
        finally
        {
            // Publish the event
            _ringBuffer.Publish(sequence);
        }
    }
}
