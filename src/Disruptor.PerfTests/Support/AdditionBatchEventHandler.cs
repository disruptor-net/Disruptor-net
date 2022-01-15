using System;
using System.Threading;
using Disruptor.Tests.Support;

namespace Disruptor.PerfTests.Support
{
    public class AdditionBatchEventHandler : IBatchEventHandler<PerfEvent>
    {
        private PaddedLong _value;
        private PaddedLong _batchesProcessed;
        private long _latchSequence;
        private readonly ManualResetEvent _latch = new(false);

        public long Value => _value.Value;
        public long BatchesProcessed => _batchesProcessed.Value;

        public void WaitForSequence()
        {
            _latch.WaitOne();
        }

        public void Reset(long expectedSequence)
        {
            _value.Value = 0;
            _latch.Reset();
            _latchSequence = expectedSequence;
            _batchesProcessed.Value = 0;
        }

        public void OnBatch(EventBatch<PerfEvent> batch, long sequence)
        {
            _batchesProcessed.Value++;

            foreach (var data in batch.AsSpan())
            {
                _value.Value = _value.Value + data.Value;
            }

            if(sequence + batch.Length - 1 == _latchSequence)
            {
                _latch.Set();
            }
        }
    }
}
