using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.PerfTests.Support;
using Disruptor.Processing;
using Disruptor.Tests.Support;

namespace Disruptor.PerfTests.WorkHandler;

public class OneToThreeWorkerPoolThroughputTest : IThroughputTest
{
    private const int _numWorkers = 3;
    private const int _bufferSize = 1024 * 8;
    private const long _iterations = 1000L * 1000L * 100L;

    private readonly PaddedLong[] _counters = new PaddedLong[_numWorkers];

    private readonly BlockingCollection<long> _blockingQueue = new(_bufferSize);
    private readonly EventCountingQueueProcessor[] _queueWorkers = new EventCountingQueueProcessor[_numWorkers];
    private readonly EventCountingWorkHandler[] _handlers = new EventCountingWorkHandler[_numWorkers];

    private readonly RingBuffer<PerfEvent> _ringBuffer = RingBuffer<PerfEvent>.CreateSingleProducer(PerfEvent.EventFactory,
                                                                                                    _bufferSize,
                                                                                                    new YieldingWaitStrategy());

    private readonly WorkerPool<PerfEvent> _workerPool;

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

        _workerPool = new WorkerPool<PerfEvent>(_ringBuffer,
                                                _ringBuffer.NewBarrier(),
                                                new FatalExceptionHandler<PerfEvent>(),
                                                _handlers);

        _ringBuffer.AddGatingSequences(_workerPool.GetWorkerSequences());
    }

    public int RequiredProcessorCount => 4;

    public long Run(ThroughputSessionContext sessionContext)
    {
        ResetCounters();

        _workerPool.Start();

        sessionContext.Start();

        var ringBuffer = _ringBuffer;
        for (long i = 0; i < _iterations; i++)
        {
            var sequence = ringBuffer.Next();
            ringBuffer[sequence].Value = i;
            ringBuffer.Publish(sequence);
        }

        _workerPool.DrainAndHalt();

        // Workaround to ensure that the last worker(s) have completed after releasing their events
        Thread.Sleep(1);
        sessionContext.Stop();

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