using System.Threading;
using Disruptor.Tests.Support;

namespace Disruptor.PerfTests.Support
{
    public class AdditionEventHandler : IEventHandler<PerfEvent>, IValueEventHandler<PerfValueEvent>, IBatchStartAware
    {
        private PaddedLong _value;
        private long _latchSequence;
        public readonly ManualResetEvent Latch = new ManualResetEvent(false);
        public PaddedLong BatchesProcessedCount;

        public long Value => _value.Value;

        public void Reset(long latchSequence)
        {
            _value.Value = 0;
            Latch.Reset();
            _latchSequence = latchSequence;
            BatchesProcessedCount.Value = 0;
        }

        public void OnEvent(PerfEvent data, long sequence, bool endOfBatch)
        {
            _value.Value = _value.Value + data.Value;

            if(_latchSequence == sequence)
            {
                Latch?.Set();
            }
        }

        public void OnEvent(ref PerfValueEvent data, long sequence, bool endOfBatch)
        {
            _value.Value = _value.Value + data.Value;

            if (_latchSequence == sequence)
            {
                Latch?.Set();
            }
        }

        public void OnBatchStart(long batchSize)
        {
            BatchesProcessedCount.Value = BatchesProcessedCount.Value + 1;
        }
    }
}
