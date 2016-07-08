using System.Threading;

namespace Disruptor.PerfTests.Support
{
    public class ValueMutationEventHandler : IEventHandler<ValueEvent>
    {
        private readonly Operation _operation;
        private long _value;
        private long _iterations;
        private ManualResetEvent _latch;

        public ValueMutationEventHandler(Operation operation, long iterations, ManualResetEvent latch)
        {
            _operation = operation;
            _iterations = iterations;
            _latch = latch;
        }

        public long Value => _value;

        public void Reset(ManualResetEvent latch, long expectedCount)
        {
            _value = 0L;
            _latch = latch;
            _iterations = expectedCount;
        }

        public void OnEvent(ValueEvent data, long sequence, bool endOfBatch)
        {
            _value = _operation.Op(_value, data.Value);

            if (sequence == _iterations)
            {
                _latch.Set();
            }
        }
    }
}
