using System;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Tests.Support;

#if DISRUPTOR_V5

namespace Disruptor.PerfTests.Support
{
    public class AdditionAsyncBatchEventHandler : IAsyncBatchEventHandler<PerfEvent>
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

        public ValueTask OnBatch(EventBatch<PerfEvent> batch)
        {
            _batchesProcessed.Value++;

            foreach (var data in batch)
            {
                _value.Value += data.Value;
            }

            if(_latchSequence == batch.EndSequence)
            {
                _latch.Set();
            }

            return ValueTask.CompletedTask;
        }
    }
}

#endif
