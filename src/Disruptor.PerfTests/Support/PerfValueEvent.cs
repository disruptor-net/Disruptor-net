using System;

namespace Disruptor.PerfTests.Support
{
    public struct PerfValueEvent
    {
        public static readonly Func<PerfValueEvent> EventFactory = () => new PerfValueEvent();
        public static readonly unsafe int Size = sizeof(PerfValueEvent);

        public long Value { get; set; }
    }
}
