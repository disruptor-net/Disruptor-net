using System;

namespace Disruptor.Samples.Wiki.GettingStarted;

public class SampleEventHandler : IEventHandler<SampleEvent>
{
    public void OnEvent(SampleEvent data, long sequence, bool endOfBatch)
    {
        Console.WriteLine($"Event: {data.Id} => {data.Value}");
    }
}