using System;

namespace Disruptor.Tests.Support
{
    public class LongEvent
    {
        public long Value { get; set; }

        public static Func<LongEvent> Factory => () => new LongEvent();
    }
}