using System;
using System.Collections.Concurrent;

namespace Disruptor.PerfTests.Support
{
    public class ValueMutationQueueEventProcessor
    {
        private long _value;
        private readonly BlockingCollection<long> _blockingQueue;
        private readonly Operation _operation;
        private readonly long _iterations;
        private bool _done;

        public ValueMutationQueueEventProcessor(BlockingCollection<long> blockingQueue, Operation operation, long iterations)
        {
            _blockingQueue = blockingQueue;
            _operation = operation;
            _iterations = iterations;
            _done = false;
        }

        public bool Done
        {
            get { return _done; }
        }

        public long Value
        {
            get { return _value; }
        }

        public void Reset()
        {
            _value = 0L;
            _done = false;
        }

        public void Run()
        {
            for (var i = 0; i < _iterations; i++)
            {
                try
                {
                    var value = _blockingQueue.Take();
                    _value = _operation.Op(_value, value);
                }
                catch (Exception)
                {
                    break;
                }
            }
            _done = true;
        }
    }
}
