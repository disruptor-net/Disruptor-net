namespace Disruptor.Tests.Support
{
    public class LongEvent
    {
        public LongEvent(long value)
        {
            Value = value;
        }

        public long Value { get; set; }
    }
}