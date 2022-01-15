using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;

namespace Disruptor.Samples.Wiki.GettingStarted;

public class SampleProgram
{
    public static void Main()
    {
        // Specify the size of the ring buffer, must be power of 2.
        const int bufferSize = 1024;

        // Construct the Disruptor
        var disruptor = new Dsl.Disruptor<SampleEvent>(() => new SampleEvent(), bufferSize);

        // Connect the handler
        disruptor.HandleEventsWith(new SampleEventHandler());

        // Start the Disruptor, starts all threads running
        disruptor.Start();

        // Get the ring buffer from the Disruptor to be used for publishing.
        var ringBuffer = disruptor.RingBuffer;

        var producer = new SampleEventProducer(ringBuffer);
        var memory = new Memory<byte>(new byte[12]);

        for (var i = 0; ; i++)
        {
            MemoryMarshal.Write(memory.Span, ref i);

            producer.ProduceUsingRawApi(memory);

            Thread.Sleep(1000);
        }
    }
}

public class SampleConstruct
{
    public void Main()
    {
        // Construct the Disruptor with a SingleProducerSequencer
        var disruptor = new Disruptor<SampleEvent>(() => new SampleEvent(), 1024, TaskScheduler.Default, ProducerType.Single, new BlockingWaitStrategy());
    }
}