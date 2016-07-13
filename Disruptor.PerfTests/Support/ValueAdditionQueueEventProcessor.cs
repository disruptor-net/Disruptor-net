using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Disruptor.PerfTests.Support
{
    public class ValueAdditionQueueEventProcessor
    {
        private volatile bool _running;
        private long _value;
        private long _sequence;
        private ManualResetEvent _signal;

        private readonly BlockingCollection<long> _queue;
        private readonly long _count;

        public ValueAdditionQueueEventProcessor(BlockingCollection<long> queue, long count)
        {
            _queue = queue;
            _count = count;
        }

        public long Value => _value;

        public void Reset(ManualResetEvent signal)
        {
            _value = 0L;
            _sequence = 0L;
            _signal = signal;
        }

        public void Halt() => _running = false;

        public void Run()
        {
            _running = true;
            while (_running)
            {
                long value;
                if (!_queue.TryTake(out value))
                    continue;

                _value += value;

                if (_sequence++ == _count)
                {
                    _signal.Set();
                }
            }
        }
    }
}
