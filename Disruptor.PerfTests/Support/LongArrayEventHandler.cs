using System.Threading;

namespace Disruptor.PerfTests.Support
{
    public class LongArrayEventHandler : IEventHandler<long[]>
    {
        private Volatile.PaddedLong _value;

        public long Count { get; private set; }

        public ManualResetEvent Signal { get; private set; }

        public long Value => _value.ReadUnfenced();

        public void Reset(ManualResetEvent signal, long expectedCount)
        {
            _value.WriteFullFence(0L);
            Signal = signal;
            Count = expectedCount;
        }

        public void OnEvent(long[] value, long sequence, bool endOfBatch)
        {
            for (var i = 0; i < value.Length; i++)
            {
                _value.WriteUnfenced(_value.ReadUnfenced() + value[i]);
            }
            
            if (--Count == sequence)
            {
                Signal?.Set();
            }
        }
    }
}