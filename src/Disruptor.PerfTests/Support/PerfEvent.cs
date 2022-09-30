using System;

namespace Disruptor.PerfTests.Support;

public sealed class PerfEvent
{
    public static readonly Func<PerfEvent> EventFactory = () => new PerfEvent();

    public long Value;

#if LARGE_PERF_EVENTS
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public long Output { get; set; }
#endif
}
