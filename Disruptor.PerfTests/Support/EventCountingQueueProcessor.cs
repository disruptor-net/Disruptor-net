using System.Collections.Concurrent;
using Disruptor.Tests.Support;

namespace Disruptor.PerfTests.Support
{
    public class EventCountingQueueProcessor
    {
        private volatile bool _running;
        private readonly BlockingCollection<long> _blockingQueue;
        private readonly PaddedLong[] _counters;
        private readonly int _index;

        public EventCountingQueueProcessor(
            BlockingCollection<long> blockingQueue, PaddedLong[] counters, int index)
        {
            _blockingQueue = blockingQueue;
            _counters = counters;
            _index = index;
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
                long item;
                if (!_blockingQueue.TryTake(out item))
                    continue;

                _counters[_index].Value = _counters[_index].Value + 1L;
            }
        }
    }
}