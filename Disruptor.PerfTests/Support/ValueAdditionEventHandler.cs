using System.Threading;

namespace Disruptor.PerfTests.Support
{
    public class ValueAdditionEventHandler : IEventHandler<ValueEvent>
    {
        private readonly long _iterations;
        private Volatile.PaddedLong _value;
        private readonly ManualResetEvent _mru;

        public ValueAdditionEventHandler(long iterations, ManualResetEvent mru)
        {
            _iterations = iterations;
            _mru = mru;
        }

        public long Value
        {
            get { return _value.ReadUnfenced(); }
        }

        public void OnNext(ValueEvent value, long sequence, bool endOfBatch)
        {
            _value.WriteUnfenced(_value.ReadUnfenced() + value.Value);

            if(sequence == _iterations - 1)
            {
                _mru.Set();
            }
        }
    }
}


