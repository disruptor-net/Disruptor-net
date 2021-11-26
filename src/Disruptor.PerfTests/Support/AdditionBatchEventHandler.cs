using System;
using System.Threading;
using Disruptor.Tests.Support;

#if NETCOREAPP

namespace Disruptor.PerfTests.Support
{
    public class AdditionBatchEventHandler : IBatchEventHandler<PerfEvent>
    {
        private PaddedLong _value;
        private PaddedLong _batchesProcessed;
        private long _latchSequence;
        private readonly ManualResetEvent _latch = new ManualResetEvent(false);

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

        public void OnBatch(ReadOnlySpan<PerfEvent> batch, long sequence)
        {
            _batchesProcessed.Value++;

            foreach (var data in batch)
            {
                _value.Value = _value.Value + data.Value;
            }

            if(_latchSequence == sequence + batch.Length - 1)
            {
                _latch.Set();
            }
        }
    }
}

#endif
