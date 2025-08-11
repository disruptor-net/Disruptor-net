using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;

namespace Disruptor.Samples;

public class IpcDisruptorMinimalSample
{
    public static async Task Main(string[] args)
    {
        // 1: Disruptor setup

        await using var disruptor = new IpcDisruptor<IpcEvent>(1024);

        disruptor.HandleEventsWith(new Handler());

        await disruptor.Start();

        // 2: Publisher setup

        // The publisher can be created before the disruptor start, but publishing events
        // before start is dangerous because the events might be lost.

        using var publisher = new IpcPublisher<IpcEvent>(disruptor.IpcDirectoryPath);

        using (var scope = publisher.PublishEvent())
        {
            scope.Event().Value = 101;
        }

        Thread.Sleep(1000);
    }

    public struct IpcEvent
    {
        public int Value;
    }

    public class Handler : IValueEventHandler<IpcEvent>
    {
        public void OnEvent(ref IpcEvent data, long sequence, bool endOfBatch)
        {
            Console.WriteLine($"Event received, Value: {data.Value}");
        }
    }
}
