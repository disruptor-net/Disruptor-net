using System;

namespace Disruptor.Tests.Support;

public struct StubUnmanagedEvent : IEquatable<StubUnmanagedEvent>, IStubEvent
{
    public static readonly unsafe int Size = sizeof(StubUnmanagedEvent);

    public StubUnmanagedEvent(int i)
    {
        Value = i;
        DoubleValue = default;
    }

    public int Value { get; set; }

    public double DoubleValue { get; set; }

    public static readonly Func<StubUnmanagedEvent> EventFactory = () => new StubUnmanagedEvent(-1);

    public override int GetHashCode()
    {
        return Value;
    }

    public bool Equals(StubUnmanagedEvent other)
    {
        return other.Value == Value && other.DoubleValue == DoubleValue;
    }

    public override bool Equals(object? obj)
    {
        return obj is StubUnmanagedEvent other && Equals(other);
    }

    public void Mutate(int value)
    {
        Value = value;
        DoubleValue = value + 0.1;
    }

    public override string ToString()
    {
        return $"Value: {Value}, DoubleValue: {DoubleValue}";
    }
}