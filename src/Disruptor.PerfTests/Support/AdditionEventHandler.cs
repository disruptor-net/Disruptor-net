using System.Threading;
using Disruptor.Tests.Support;

namespace Disruptor.PerfTests.Support
{
    public class AdditionEventHandler : IEventHandler<PerfEvent>, IValueEventHandler<PerfValueEvent>
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

        public void OnEvent(PerfEvent data, long sequence, bool endOfBatch)
        {
            _value.Value = _value.Value + data.Value;

            if(_latchSequence == sequence)
            {
                _latch.Set();
            }
        }

        public void OnEvent(ref PerfValueEvent data, long sequence, bool endOfBatch)
        {
            _value.Value += data.Value;

            if (_latchSequence == sequence)
            {
                _latch.Set();
            }
        }

        public void OnBatchStart(long batchSize)
        {
            _batchesProcessed.Value++;
        }
    }
}
