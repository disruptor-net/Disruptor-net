using System;

namespace Disruptor.PerfTests.Support
{
    public struct ValueEvent
    {
        public static readonly Func<ValueEvent> EventFactory = () => new ValueEvent();

        public long Value { get; set; }
    }
}