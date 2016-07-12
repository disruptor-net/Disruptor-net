using System.Collections.Concurrent;
using System.Threading;

namespace Disruptor.PerfTests.Support
{
    class ValueAdditionQueueProcessor
    {
        private volatile bool _running;
        private long _value;
        private long _sequence;
        private ManualResetEvent _latch;

        private readonly BlockingCollection<long> _blockingQueue;
        private readonly long _count;

        public ValueAdditionQueueProcessor(BlockingCollection<long> blockingQueue, long count)
        {
            _blockingQueue = blockingQueue;
            _count = count;
        }

        public long GetValue()
        {
            return _value;
        }

        public void Reset(ManualResetEvent latch)
        {
            _value = 0L;
            _sequence = 0L;
            _latch = latch;
        }

        public void Halt()
        {
            _running = false;
        }

        public void Run(CancellationToken cancellationToken)
        {
            _running = true;
            while (true)
            {
                try
                {
                    var value = _blockingQueue.Take(cancellationToken);
                    _value += value;

                    if (_sequence++ == _count)
                    {
                        _latch.Set();
                    }
                }
                catch
                {
                    if (!_running)
                    {
                        break;
                    }
                }
            }
        }
    }
}
