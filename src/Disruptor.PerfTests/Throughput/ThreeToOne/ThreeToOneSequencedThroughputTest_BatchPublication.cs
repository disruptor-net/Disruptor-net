﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.PerfTests.Support;
using Disruptor.Processing;

namespace Disruptor.PerfTests.Throughput.ThreeToOne;

/// <summary>
/// Sequence a series of events from multiple publishers going to one event processor.
/// Use batch publication (<see cref="RingBuffer.Next(int)"/>.
///
/// +----+
/// | P1 |------+
/// +----+      |
///             v
/// +----+    +-----+
/// | P1 |--->| EP1 |
/// +----+    +-----+
///             ^
/// +----+      |
/// | P3 |------+
/// +----+
/// Disruptor:
/// ==========
///             track to prevent wrap
///             +--------------------+
///             |                    |
///             |                    v
/// +----+    +====+    +====+    +-----+
/// | P1 |--->| RB |/---| SB |    | EP1 |
/// +----+    +====+    +====+    +-----+
///             ^   get    ^         |
/// +----+      |          |         |
/// | P2 |------+          +---------+
/// +----+      |            waitFor
///             |
/// +----+      |
/// | P3 |------+
/// +----+
/// P1  - Publisher 1
/// P2  - Publisher 2
/// P3  - Publisher 3
/// RB  - RingBuffer
/// SB  - SequenceBarrier
/// EP1 - EventProcessor 1
/// </summary>
public class ThreeToOneSequencedThroughputTest_BatchPublication : IThroughputTest
{
    private const int _numPublishers = 3;
    private const int _bufferSize = 1024 * 64;
    private const long _iterations = 1000L * 1000L * 100L;
    private readonly CountdownEvent _cyclicBarrier = new(_numPublishers + 1);

    private readonly RingBuffer<PerfEvent> _ringBuffer = RingBuffer<PerfEvent>.CreateMultiProducer(PerfEvent.EventFactory, _bufferSize, new BusySpinWaitStrategy());
    private readonly AdditionEventHandler _handler = new();
    private readonly IEventProcessor<PerfEvent> _eventProcessor;
    private readonly ValueBatchPublisher[] _valuePublishers = new ValueBatchPublisher[_numPublishers];

    public ThreeToOneSequencedThroughputTest_BatchPublication()
    {
        var sequenceBarrier = _ringBuffer.NewBarrier();
        for (var i = 0; i < _numPublishers; i++)
        {
            _valuePublishers[i] = new ValueBatchPublisher(_cyclicBarrier, _ringBuffer, _iterations / _numPublishers, 10);
        }

        _eventProcessor = EventProcessorFactory.Create(_ringBuffer, sequenceBarrier, _handler);
        _ringBuffer.AddGatingSequences(_eventProcessor.Sequence);
    }

    public int RequiredProcessorCount => 4;

    public long Run(ThroughputSessionContext sessionContext)
    {
        _cyclicBarrier.Reset();

        _handler.Reset(_eventProcessor.Sequence.Value + ((_iterations / _numPublishers) * _numPublishers));

        var futures = new Task[_numPublishers];
        for (var i = 0; i < _numPublishers; i++)
        {
            var index = i;
            futures[i] = Task.Run(() => _valuePublishers[index].Run());
        }
        var startTask = _eventProcessor.StartLongRunning();
        startTask.Wait(TimeSpan.FromSeconds(5));

        sessionContext.Start();
        _cyclicBarrier.Signal();
        _cyclicBarrier.Wait();

        for (var i = 0; i < _numPublishers; i++)
        {
            futures[i].Wait();
        }

        _handler.WaitForSequence();

        sessionContext.Stop();

        var shutdownTask = _eventProcessor.Halt();
        shutdownTask.Wait(2000);

        sessionContext.SetBatchData(_handler.BatchesProcessed, _iterations);

        return _iterations;
    }

    private class ValueBatchPublisher
    {
        private readonly CountdownEvent _cyclicBarrier;
        private readonly RingBuffer<PerfEvent> _ringBuffer;
        private readonly long _iterations;
        private readonly int _batchSize;

        public ValueBatchPublisher(CountdownEvent cyclicBarrier, RingBuffer<PerfEvent> ringBuffer, long iterations, int batchSize)
        {
            _cyclicBarrier = cyclicBarrier;
            _ringBuffer = ringBuffer;
            _iterations = iterations;
            _batchSize = batchSize;
        }

        public void Run()
        {
            _cyclicBarrier.Signal();
            _cyclicBarrier.Wait();

            var ringBuffer = _ringBuffer;

            for (long i = 0; i < _iterations; i += _batchSize)
            {
                var hi = ringBuffer.Next(_batchSize);
                var lo = hi - (_batchSize - 1);
                for (var l = lo; l <= hi; l++)
                {
                    var @event = ringBuffer[l];
                    @event.Value = l;
                }
                ringBuffer.Publish(lo, hi);
            }
        }
    }
}
