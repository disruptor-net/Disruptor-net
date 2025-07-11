using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Disruptor.Dsl;

namespace Disruptor.Samples;

public class AsyncEventStreamSample
{
    public static async Task Main(string[] args)
    {
        var disruptor = new Disruptor<Event>(() => new Event(), 1024, TaskScheduler.Default, ProducerType.Single, new AsyncWaitStrategy());
        var ringBuffer = disruptor.RingBuffer;

        Console.WriteLine("Starting consumer");

        var stream = ringBuffer.NewAsyncEventStream();
        var consumer = RunConsumer(stream);

        Console.WriteLine("Starting disruptor");

        await disruptor.Start();

        var random = new Random();

        for (var i = 0; i < 1_000_000; i++)
        {
            var id = random.Next(10);
            var price = random.NextDouble() * 100 + i;

            using var scope = ringBuffer.PublishEvent();
            var evt = scope.Event();
            evt.Id = id;
            evt.Price = price;
        }

        Console.WriteLine("Stopping disruptor");

        await disruptor.Shutdown();

        // AsyncEventStream instances are not part of the Disruptor (for now), they must be explicitly disposed.

        stream.Dispose();

        Console.WriteLine("Waiting for consumer");

        await consumer;

        Console.WriteLine("Done");
        Console.WriteLine("Press Enter to exit...");
        Console.ReadLine();
    }

    private static async Task RunConsumer(AsyncEventStream<Event> stream)
    {
        var aggregations = new Dictionary<int, (double sum, int count)>();

        try
        {
            await foreach (var batch in stream.ConfigureAwait(false))
            {
                foreach (var evt in batch)
                {
                    var aggregation = aggregations.GetValueOrDefault(evt.Id);

                    aggregations[evt.Id] = (aggregation.sum + evt.Price, aggregation.count + 1);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"Consumer iteration cancelled");
        }

        Console.WriteLine($"EventCount: {aggregations.Sum(x => x.Value.count)}");

        foreach (var (id, aggregation) in aggregations.OrderBy(x => x.Key))
        {
            Console.WriteLine($"Id: {id}, Average: {aggregation.sum / aggregation.count:N2}");
        }
    }

    public class Event
    {
        public int Id { get; set; }
        public double Price { get; set; }
    }
}
