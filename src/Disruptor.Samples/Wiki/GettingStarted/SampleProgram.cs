using System;
using System.Runtime.InteropServices;
using System.Threading;
using Disruptor.Dsl;

namespace Disruptor.Samples.Wiki.GettingStarted;

public class SampleProgram
{
    public static void Main()
    {
        // Size of the ring buffer, must be power of 2.
        const int bufferSize = 1024;

        // Create the disruptor
        var disruptor = new Disruptor<SampleEvent>(() => new SampleEvent(), bufferSize);

        // Configure a simple chain
        // SampleEventHandler -> OtherSampleEventHandler
        disruptor.HandleEventsWith(new SampleEventHandler());

        // Start the disruptor (start the consumer threads)
        disruptor.Start();

        var ringBuffer = disruptor.RingBuffer;

        // Use the ring buffer to publish events

        var producer = new SampleEventProducer(ringBuffer);
        var memory = new Memory<byte>(new byte[12]);

        for (var i = 0; ; i++)
        {
            MemoryMarshal.Write(memory.Span, i);

            producer.ProduceUsingRawApi(memory);

            Thread.Sleep(1000);
        }
    }
}
