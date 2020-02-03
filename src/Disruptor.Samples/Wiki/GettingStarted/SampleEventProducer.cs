using System;
using System.Runtime.InteropServices;

namespace Disruptor.Samples.Wiki.GettingStarted
{
    public class SampleEventProducer
    {
        private readonly RingBuffer<SampleEvent> _ringBuffer;

        public SampleEventProducer(RingBuffer<SampleEvent> ringBuffer)
        {
            _ringBuffer = ringBuffer;
        }

        public void ProduceUsingRawApi(ReadOnlyMemory<byte> input)
        {
            // (1) Claim the next sequence
            var sequence = _ringBuffer.Next();
            try
            {
                // (2) Get and configure the event for the sequence
                var data = _ringBuffer[sequence];
                data.Id = MemoryMarshal.Read<int>(input.Span);
                data.Value = MemoryMarshal.Read<double>(input.Span.Slice(4));
            }
            finally
            {
                // (3) Publish the event
                _ringBuffer.Publish(sequence);
            }
        }

        public void ProduceUsingScope(ReadOnlyMemory<byte> input)
        {
            using (var scope = _ringBuffer.PublishEvent())
            {
                var data = scope.Event();
                data.Id = MemoryMarshal.Read<int>(input.Span);
                data.Value = MemoryMarshal.Read<double>(input.Span.Slice(4));

                // The event is published at the end of the scope
            }
        }

        public void ProduceUsingCustomWaitStrategy(ReadOnlyMemory<byte> input)
        {
            // Claim the next sequence
            var sequence = _ringBuffer.Next();
            try
            {
                // Get the event for the sequence
                var data = _ringBuffer[sequence];

                // Configure the event
                data.Id = MemoryMarshal.Read<int>(input.Span);
                data.Value = MemoryMarshal.Read<double>(input.Span.Slice(4));
            }
            finally
            {
                // Publish the event
                _ringBuffer.Publish(sequence);
            }
        }
    }
}
