using System.Threading;
using Disruptor.Tests.Support;

namespace Disruptor.PerfTests.Support
{
    public class ValueMutationEventHandler : IEventHandler<ValueEvent>, IBatchStartAware
    {
        private readonly Operation _operation;
        private PaddedLong _value;
        private long _iterations;
        private Barrier _latch;
        public PaddedLong BatchesProcessedCount;

        public ValueMutationEventHandler(Operation operation)
        {
            _operation = operation;
        }

        public long Value => _value.Value;

        public void Reset(Barrier latch, long expectedCount)
        {
            _value.Value = 0L;
            _latch = latch;
            _iterations = expectedCount;
            BatchesProcessedCount.Value = 0;
        }

        public void OnEvent(ValueEvent data, long sequence, bool endOfBatch)
        {
            _value.Value = _operation.Op(_value.Value, data.Value);

            if (sequence == _iterations)
            {
                _latch.SignalAndWait();
            }
        }

        public void OnBatchStart(long batchSize)
        {
            BatchesProcessedCount.Value = BatchesProcessedCount.Value + 1;
        }
    }
}
