using System;

namespace Disruptor.PerfTests.Support
{
    public sealed class PerfEvent
    {
        public static readonly Func<PerfEvent> EventFactory = () => new PerfEvent();

        public long Value { get; set; }
    }
}
