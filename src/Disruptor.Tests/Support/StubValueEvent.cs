using System;

namespace Disruptor.Tests.Support;

public struct StubValueEvent : IEquatable<StubValueEvent>, IStubEvent
{
    public StubValueEvent(int i)
    {
        Value = i;
        TestString = null;
    }

    public int Value { get; set; }

    public string? TestString { get; set; }

    public static readonly Func<StubValueEvent> EventFactory = () => new StubValueEvent(-1);

    public override int GetHashCode()
    {
        return Value;
    }

    public bool Equals(StubValueEvent other)
    {
        return other.Value == Value && other.TestString == TestString;
    }

    public override bool Equals(object? obj)
    {
        return obj is StubValueEvent other && Equals(other);
    }

    public override string ToString()
    {
        return $"Value: {Value}, TestString: {TestString}";
    }
}