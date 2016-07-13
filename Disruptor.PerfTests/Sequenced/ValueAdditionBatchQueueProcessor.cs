using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Disruptor.PerfTests.Sequenced
{
    class ValueAdditionBatchQueueProcessor
    {
        private volatile bool _running;
        private long _value;
        private long _sequence;
        private ManualResetEvent _latch;

        private readonly BlockingCollection<long> _blockingQueue;
        private readonly List<long> _batch = new List<long>(100);
        private readonly long _count;

        public ValueAdditionBatchQueueProcessor(BlockingCollection<long> blockingQueue, long count)
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
                if (!_blockingQueue.TryTake(out value))
                    continue;

                _sequence++;

                _value += value;

                var batchSize = _blockingQueue.Count;
                var cappedBatchSize = Math.Min(batchSize, 100);
                for (var i = 0; i < cappedBatchSize; i++)
                {
                    _batch.Add(_blockingQueue.Take());
                }
                _sequence += cappedBatchSize;

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
            return "ValueAdditionBatchQueueProcessor{" +
                "value=" + _value +
                ", sequence=" + _sequence +
                ", count=" + _count +
                '}';
        }

    }
}
