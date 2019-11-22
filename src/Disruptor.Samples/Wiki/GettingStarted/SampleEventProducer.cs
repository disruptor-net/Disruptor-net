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
            // Grab the next sequence
            var sequence = _ringBuffer.Next();
            try
            {
                // Get the event in the Disruptor for the sequence
                var data = _ringBuffer[sequence];

                // Fill with data
                data.Id = MemoryMarshal.Read<int>(input.Span);
                data.Value = MemoryMarshal.Read<double>(input.Span.Slice(4));
            }
            finally
            {
                // Publish the event
                _ringBuffer.Publish(sequence);
            }
        }

        public void ProduceUsingScope(ReadOnlyMemory<byte> input)
        {
            using (var scope = _ringBuffer.PublishEvent())
            {
                var data = scope.Event();

                // Fill with data
                data.Id = MemoryMarshal.Read<int>(input.Span);
                data.Value = MemoryMarshal.Read<double>(input.Span.Slice(4));

                // The event is published at the end of the scope
            }
        }
    }
}
