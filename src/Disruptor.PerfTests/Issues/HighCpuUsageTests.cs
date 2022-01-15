using System;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using NUnit.Framework;

namespace Disruptor.PerfTests.Issues;

[TestFixture, Ignore("Manual test")]
public class HighCpuUsageTests
{
    [Test]
    public void WaitForDependentHandler()
    {
        var disruptor = new Disruptor<string>(() => "Whatever", 1024, TaskScheduler.Default, ProducerType.Multi, new BlockingWaitStrategy());

        disruptor.HandleEventsWith(new BlockingHandler()).Then(new NoOpHandler());

        disruptor.Start();

        for (int i = 0; i < 20; i++)
        {
            if (i == 9) // Force a batch
                Thread.Sleep(10);

            Publish(disruptor);
        }

        //Debug.WriteLine("Sleeping main thread");
        Thread.Sleep(60000);

        disruptor.Shutdown();
    }

    private static void Publish(Disruptor<String> disruptor)
    {
        long next = disruptor.RingBuffer.Next();
        disruptor.RingBuffer.Publish(next);
    }

    public class NoOpHandler : IEventHandler<string>
    {
        public void OnEvent(string data, long sequence, bool endOfBatch)
        {
            Console.WriteLine("Got sequence " + sequence);
        }
    }

    public class BlockingHandler : IEventHandler<string>
    {
        public void OnEvent(string data, long sequence, bool endOfBatch)
        {
            if (sequence >= 10)
                Thread.Sleep(20000);
        }
    }
}