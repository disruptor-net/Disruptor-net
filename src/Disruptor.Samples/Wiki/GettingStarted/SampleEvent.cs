using System;

namespace Disruptor.Samples.Wiki.GettingStarted;

// public class SampleEvent
// {
//     public int Id { get; set; }
//     public double Value { get; set; }
//     public DateTime TimestampUtc { get; set; }
// }

/// <remarks>
/// Optional: use initialization methods to configure your events.
/// </remarks>
public class SampleEvent
{
    public int Id { get; private set; }
    public double Value { get; private set; }
    public DateTime TimestampUtc { get; private set; }

    public void Initialize(int id, double value, DateTime timestampUtc)
    {
        Id = id;
        Value = value;
        TimestampUtc = timestampUtc;
    }
}
