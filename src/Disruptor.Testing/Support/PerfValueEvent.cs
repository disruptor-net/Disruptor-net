using System;

namespace Disruptor.PerfTests.Support;

public struct PerfValueEvent
{
    public static readonly Func<PerfValueEvent> EventFactory = () => new PerfValueEvent();
    public static readonly unsafe int Size = sizeof(PerfValueEvent);

    public long Value { get; set; }

#if LARGE_PERF_EVENTS
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public long Output { get; set; }
#endif
}
