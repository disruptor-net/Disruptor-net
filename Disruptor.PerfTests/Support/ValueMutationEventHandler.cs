using System.Threading;
using Disruptor.MemoryLayout;

namespace Disruptor.PerfTests.Support
{
    public class ValueMutationEventHandler : IEventHandler<ValueEvent>
    {
        private readonly Operation _operation;
        private PaddedLong _value;
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
            get { return _value.Value; }
        }

        public void OnNext(ValueEvent data, long sequence, bool endOfBatch)
        {
            _value.Value = _operation.Op(_value.Value, data.Value);

            if (sequence == _iterations - 1)
            {
                _latch.Signal();
            }
        }
    }
}
