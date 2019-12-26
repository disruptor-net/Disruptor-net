namespace Disruptor.Tests.Support
{
    public class TestEvent
    {
        public int Value { get; set; }

        public override string ToString() => $"Test Event {Value}";
    }
}
