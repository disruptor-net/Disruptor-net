using System.Threading;
using Disruptor.Tests.Support;

namespace Disruptor.PerfTests.Support
{
    public class ValueAdditionEventHandler : IEventHandler<ValueEvent>
    {
        private PaddedLong _value;
        public long Count { get; private set; }
        private ManualResetEvent Latch { get; set; }

        public long Value => _value.Value;

        public void Reset(ManualResetEvent latch, long expectedCount)
        {
            _value.Value = 0;
            Latch = latch;
            Count = expectedCount;
        }

        public void OnEvent(ValueEvent value, long sequence, bool endOfBatch)
        {
            _value.Value = _value.Value + value.Value;

            if(Count == sequence)
            {
                Latch?.Set();
            }
        }
    }
}
