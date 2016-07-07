using System.Threading;
using Atomic;

namespace Disruptor.PerfTests.Support
{
    public class ValueMutationEventHandler : IEventHandler<ValueEvent>
    {
        private readonly Operation _operation;
        private Volatile.PaddedLong _value = new Volatile.PaddedLong(0);
        private readonly long _iterations;
        private readonly CountdownEvent _latch;

        public ValueMutationEventHandler(Operation operation, long iterations, CountdownEvent latch)
        {
            _operation = operation;
            _iterations = iterations;
            _latch = latch;
        }

        public long Value
        {
            get { return _value.ReadUnfenced(); }
        }

        public void OnEvent(ValueEvent data, long sequence, bool endOfBatch)
        {
            _value.WriteUnfenced(_operation.Op(_value.ReadUnfenced(), data.Value));

            if (sequence == _iterations - 1)
            {
                _latch.Signal();
            }
        }
    }
}
