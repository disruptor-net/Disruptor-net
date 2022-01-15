using System;
using Disruptor.Dsl;

namespace Disruptor.Samples.Wiki.ValueDisruptor;

public class ValueDisruptorUsage
{
    private static ValueDisruptor<ValueEvent> _disruptor;

    public struct ValueEvent
    {
        public int Id;
        public double Value;
    }

    public class ValueEventHandler : IValueEventHandler<ValueEvent>
    {
        public void OnEvent(ref ValueEvent data, long sequence, bool endOfBatch)
        {
            Console.WriteLine($"{data.Id}: {data.Value}");
        }
    }

    public static void SetupDisruptor()
    {
        _disruptor = new ValueDisruptor<ValueEvent>(() => new ValueEvent(), ringBufferSize: 1024);
        _disruptor.HandleEventsWith(new ValueEventHandler());
        _disruptor.Start();
    }

    public static void PublishEvent(int id, double value)
    {
        using (var scope = _disruptor.PublishEvent())
        {
            ref var data = ref scope.Event();
            data.Id = id;
            data.Value = value;
        }
    }

}