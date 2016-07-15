using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.PerfTests.Support;
using Disruptor.Tests.Support;

namespace Disruptor.PerfTests.WorkHandler
{
    public class OneToThreeReleasingWorkerPoolThroughputTest : IThroughputTest
    {
        private static readonly int _numWorkers = 3;
        private static readonly int _bufferSize = 1024 * 8;
        private static readonly long _iterations = 1000L * 1000 * 10L;
        private readonly PaddedLong[] _counters = new PaddedLong[_numWorkers];
        private readonly EventCountingAndReleasingWorkHandler[] _handlers = new EventCountingAndReleasingWorkHandler[_numWorkers];
        private readonly RingBuffer<ValueEvent> _ringBuffer = RingBuffer<ValueEvent>.CreateSingleProducer(() => new ValueEvent(),
                                                                                                         _bufferSize,
                                                                                                         new YieldingWaitStrategy());
        private readonly WorkerPool<ValueEvent> _workerPool;

        public OneToThreeReleasingWorkerPoolThroughputTest()
        {
            for (var i = 0; i < _numWorkers; i++)
            {
                _counters[i] = new PaddedLong();
            }
            for (var i = 0; i < _numWorkers; i++)
            {
                _handlers[i] = new EventCountingAndReleasingWorkHandler(_counters, i);
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
            var ringBuffer = _workerPool.Start(new BasicExecutor(TaskScheduler.Default));
            stopwatch.Start();

            for (long i = 0; i < _iterations; i++)
            {
                var sequence = ringBuffer.Next();
                ringBuffer[sequence].Value = i;
                ringBuffer.Publish(sequence);
            }

            _workerPool.DrainAndHalt();

            // Workaround to ensure that the last worker(s) have completed after releasing their events
            Thread.Sleep(1);
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

        private class EventCountingAndReleasingWorkHandler : IWorkHandler<ValueEvent>, IEventReleaseAware
        {
            private readonly PaddedLong[] _counters;
            private readonly int _index;
            private IEventReleaser _eventReleaser;

            public EventCountingAndReleasingWorkHandler(PaddedLong[] counters, int index)
            {
                _counters = counters;
                _index = index;
            }

            public void OnEvent(ValueEvent evt)
            {
                _eventReleaser.Release();
                _counters[_index].Value = _counters[_index].Value + 1L;
            }

            public void SetEventReleaser(IEventReleaser eventReleaser)
            {
                _eventReleaser = eventReleaser;
            }
        }
    }
}