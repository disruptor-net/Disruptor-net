using System.Threading;

namespace Disruptor.PerfTests.Support
{
    public class ValueAdditionEventHandler : IEventHandler<ValueEvent>
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

        public void OnEvent(ValueEvent value, long sequence, bool endOfBatch)
        {
            _value.WriteUnfenced(_value.ReadUnfenced() + value.Value);

            if(Count == sequence)
            {
                Signal?.Set();
            }
        }
    }
}


