using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.PerfTests.Support;
using Disruptor.Tests.Support;

namespace Disruptor.PerfTests.WorkHandler
{
    public class OneToThreeWorkerPoolThroughputTest : IThroughputTest
    {
        private const int _numWorkers = 3;
        private const int _bufferSize = 1024 * 8;
        private const long _iterations = 1000L * 1000L * 100L;

        private readonly PaddedLong[] _counters = new PaddedLong[_numWorkers];

        private readonly BlockingCollection<long> _blockingQueue = new BlockingCollection<long>(_bufferSize);
        private readonly EventCountingQueueProcessor[] _queueWorkers = new EventCountingQueueProcessor[_numWorkers];
        private readonly EventCountingWorkHandler[] _handlers = new EventCountingWorkHandler[_numWorkers];

        private readonly RingBuffer<ValueEvent> _ringBuffer = RingBuffer<ValueEvent>.CreateSingleProducer(() => new ValueEvent(),
                                                                                                          _bufferSize,
                                                                                                          new YieldingWaitStrategy());

        private readonly WorkerPool<ValueEvent> _workerPool;

        public OneToThreeWorkerPoolThroughputTest()
        {
            for (var i = 0; i < _numWorkers; i++)
            {
                _counters[i] = new PaddedLong();
            }

            for (var i = 0; i < _numWorkers; i++)
            {
                _queueWorkers[i] = new EventCountingQueueProcessor(_blockingQueue, _counters, i);
            }
            for (var i = 0; i < _numWorkers; i++)
            {
                _handlers[i] = new EventCountingWorkHandler(_counters, i);
            }

            _workerPool = new WorkerPool<ValueEvent>(_ringBuffer,
                                                     _ringBuffer.NewBarrier(),
                                                     new FatalExceptionHandler(),
                                                     _handlers);

            _ringBuffer.AddGatingSequences(_workerPool.WorkerSequences);
        }

        public int RequiredProcessorCount => 4;

        public long Run(Stopwatch stopwatch)
        {
            ResetCounters();
            RingBuffer<ValueEvent> ringBuffer = _workerPool.Start(new BasicExecutor(TaskScheduler.Default));
            stopwatch.Start();

            for (long i = 0; i < _iterations; i++)
            {
                long sequence = ringBuffer.Next();
                ringBuffer[sequence].Value = i;
                ringBuffer.Publish(sequence);
            }

            _workerPool.DrainAndHalt();
            stopwatch.Stop();

            PerfTestUtil.FailIfNot(_iterations, SumCounters());

            return _iterations;
        }

        private void ResetCounters()
        {
            for (var i = 0; i < _numWorkers; i++)
            {
                _counters[i].Value = 0L;
            }
        }

        private long SumCounters()
        {
            var sumJobs = 0L;
            for (var i = 0; i < _numWorkers; i++)
            {
                sumJobs += _counters[i].Value;
            }

            return sumJobs;
        }
    }
}
