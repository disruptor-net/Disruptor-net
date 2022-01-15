using System;

namespace Disruptor.Tests.Support;

public class StubEvent : IEquatable<StubEvent>, IStubEvent
{
    public StubEvent(int i)
    {
        Value = i;
    }

    public int Value { get; set; }

    public static readonly Func<StubEvent> EventFactory = () => new StubEvent(-1);

    public override int GetHashCode()
    {
        return Value;
    }

    public bool Equals(StubEvent? other)
    {
        return other != null && other.Value == Value;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as StubEvent);
    }

    public override string ToString()
    {
        return $"Value: {Value}";
    }

    public void Copy(StubEvent evt)
    {
        Value = evt.Value;
    }
}