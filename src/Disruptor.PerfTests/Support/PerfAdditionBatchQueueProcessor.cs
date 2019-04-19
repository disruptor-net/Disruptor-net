using System.Collections.Generic;
using System.Threading;

namespace Disruptor.PerfTests.Support
{
    public class PerfAdditionBatchQueueProcessor
    {
        private volatile bool _running;
        private long _value;
        private long _sequence;
        private ManualResetEvent _latch;

        private readonly ArrayConcurrentQueue<long> _blockingQueue;
        private readonly List<long> _batch = new List<long>(100);
        private readonly long _count;

        public PerfAdditionBatchQueueProcessor(ArrayConcurrentQueue<long> blockingQueue, long count)
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
                long value;
                if (!_blockingQueue.TryDequeue(out value))
                    continue;

                _sequence++;
                _value += value;

                for (var i = 0; i < 100; i++)
                {
                    long item;
                    if (!_blockingQueue.TryDequeue(out item))
                        break;

                    _batch.Add(item);
                }
                _sequence += _batch.Count;

                value = 0;
                for (int i = 0, n = _batch.Count; i < n; i++)
                {
                    value += _batch[i];
                }
                _value += value;

                _batch.Clear();

                if (_sequence == _count)
                    _latch.Set();
            }
        }

        public override string ToString()
        {
            return "PerfAdditionBatchQueueProcessor{" +
                "value=" + _value +
                ", sequence=" + _sequence +
                ", count=" + _count +
                '}';
        }

    }
}
