using System.Threading;
using Disruptor.Tests.Support;

namespace Disruptor.PerfTests.Support
{
    public class ValueAdditionEventHandler : IEventHandler<ValueEvent>
    {
        private PaddedLong _value;
        public long Count { get; private set; }
        private ManualResetEvent Latch { get; set; }
        public long BatchesProcessedCount;

        public long Value => _value.Value;

        public void Reset(ManualResetEvent latch, long expectedCount)
        {
            _value.Value = 0;
            Latch = latch;
            Count = expectedCount;
            BatchesProcessedCount = 0;
        }

        public void OnEvent(ValueEvent value, long sequence, bool endOfBatch)
        {
            _value.Value = _value.Value + value.Value;
            if (endOfBatch)
                BatchesProcessedCount++;

            if(Count == sequence)
            {
                Latch?.Set();
            }
        }
    }
}
