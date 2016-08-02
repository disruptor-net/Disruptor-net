using System.Collections.Concurrent;
using System.Threading;

namespace Disruptor.PerfTests.Support
{
    public class ValueAdditionQueueProcessor
    {
        private volatile bool _running;
        private long _value;
        private long _sequence;
        private ManualResetEvent _latch;

        //private readonly ConcurrentQueue<long> _blockingQueue;
        private readonly ArrayConcurrentQueue<long> _blockingQueue;
        private readonly long _count;

        //public ValueAdditionQueueProcessor(ConcurrentQueue<long> blockingQueue, long count)
        public ValueAdditionQueueProcessor(ArrayConcurrentQueue<long> blockingQueue, long count)
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

        public void Run()
        {
            _running = true;
            while (_running)
            {
                //long value;
                //while (!_blockingQueue.TryDequeue(out value))
                //    break;

                long value;
                while (!_blockingQueue.TryDequeue(out value))
                    break;

                _value += value;

                if (_sequence++ == _count)
                    _latch.Set();
            }
        }
    }
}