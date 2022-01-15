namespace Disruptor.Tests.Support;

public struct TestValueEvent
{
    public static readonly unsafe int Size = sizeof(TestValueEvent);

    public int Value { get; set; }

    public override string ToString() => $"Test Event {Value}";
}