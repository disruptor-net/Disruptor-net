using System;

namespace Disruptor.PerfTests.Support
{
    public sealed class ValueEvent
    {
        public static readonly Func<ValueEvent> EventFactory = () => new ValueEvent();

        public long Value { get; set; }
    }
}