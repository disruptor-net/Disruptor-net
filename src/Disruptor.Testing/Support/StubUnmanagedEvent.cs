using System;
using System.Runtime.InteropServices;

namespace Disruptor.Tests.Support;

[StructLayout(LayoutKind.Explicit, Size = 8)]
public struct StubUnmanagedEvent : IEquatable<StubUnmanagedEvent>, IStubEvent
{
    public StubUnmanagedEvent(int i)
    {
        Value = i;
    }

    public StubUnmanagedEvent(int value, int key)
    {
        Value = value;
        Key = key;
    }

    [field: FieldOffset(0)]
    public int Value { get; set; }

    [field: FieldOffset(4)]
    public int Key { get; set; }

    public override int GetHashCode()
    {
        return Value;
    }

    public bool Equals(StubUnmanagedEvent other)
    {
        return other.Value == Value && other.Key == Key;
    }

    public override bool Equals(object? obj)
    {
        return obj is StubUnmanagedEvent other && Equals(other);
    }

    public override string ToString()
    {
        return $"Value: {Value}, Key: {Key}";
    }
}
