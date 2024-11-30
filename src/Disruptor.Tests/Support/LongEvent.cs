using System;

namespace Disruptor.Tests.Support;

public class LongEvent : IEquatable<LongEvent>
{
    public LongEvent()
    {
    }

    public LongEvent(long value)
    {
        Value = value;
    }

    public long Value { get; set; }
    public bool Equals(LongEvent? other)
    {
        return other != null && Value.Equals(other.Value);
    }

    public override string ToString()
    {
        return $"{nameof(Value)}: {Value}";
    }
}
