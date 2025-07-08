using System;
using System.Threading;
using Disruptor.PerfTests.Support;
using Disruptor.Processing;
using Disruptor.Testing.Support;

namespace Disruptor.PerfTests.Throughput.OneToThree;

public class OneToThreeReleasingWorkerPoolThroughputTest : IThroughputTest
{
    private static readonly int _numWorkers = 3;
    private static readonly int _bufferSize = 1024 * 8;
    private static readonly long _iterations = 1000L * 1000 * 10L;
    private readonly PaddedLong[] _counters = new PaddedLong[_numWorkers];
    private readonly IWorkHandler<PerfEvent>[] _handlers = new IWorkHandler<PerfEvent>[_numWorkers];
    private readonly RingBuffer<PerfEvent> _ringBuffer = RingBuffer<PerfEvent>.CreateSingleProducer(PerfEvent.EventFactory, _bufferSize, new YieldingWaitStrategy());

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
    }

    public int RequiredProcessorCount => 4;

    public long Run(ThroughputSessionContext sessionContext)
    {
        ResetCounters();

        var workerPool = new WorkerPool<PerfEvent>(_ringBuffer, Array.Empty<Sequence>(), new FatalExceptionHandler<PerfEvent>(), _handlers);
        _ringBuffer.AddGatingSequences(workerPool.GetWorkerSequences());

        var startTask = workerPool.Start();
        startTask.Wait(TimeSpan.FromSeconds(5));

        sessionContext.Start();

        var ringBuffer = _ringBuffer;
        for (long i = 0; i < _iterations; i++)
        {
            var sequence = ringBuffer.Next();
            ringBuffer[sequence].Value = i;
            ringBuffer.Publish(sequence);
        }

        while (workerPool.HasBacklog())
        {
            Thread.Yield();
        }

        // ???
        // Workaround to ensure that the last worker(s) have completed after releasing their events
        Thread.Sleep(1);

        sessionContext.Stop();

        var shutdownTask = workerPool.Halt();
        shutdownTask.Wait();

        PerfTestUtil.FailIfNot(_iterations, SumCounters());

        foreach (var workerSequence in workerPool.GetWorkerSequences())
        {
            _ringBuffer.RemoveGatingSequence(workerSequence);
        }

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

    private class EventCountingAndReleasingWorkHandler : IWorkHandler<PerfEvent>, IEventReleaseAware
    {
        private readonly PaddedLong[] _counters;
        private readonly int _index;
        private IEventReleaser _eventReleaser;

        public EventCountingAndReleasingWorkHandler(PaddedLong[] counters, int index)
        {
            _counters = counters;
            _index = index;
        }

        public void OnEvent(PerfEvent evt)
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
