﻿using System;
using System.Threading;
using Disruptor.PerfTests.Support;
using Disruptor.Processing;

namespace Disruptor.PerfTests.Throughput.OneToOne.EventHandler;

/// <summary>
/// Unicast a series of items between 1 publisher and 1 event processor
/// Use batch publication (<see cref="RingBuffer.Next(int)"/>.
///
/// <code>
/// +----+    +-----+
/// | P1 |--->| EP1 |
/// +----+    +-----+
///
/// Disruptor:
/// ==========
///              track to prevent wrap
///              +------------------+
///              |                  |
///              |                  v
/// +----+    +====+    +====+   +-----+
/// | P1 |---›| RB |‹---| SB |   | EP1 |
/// +----+    +====+    +====+   +-----+
///      claim      get    ^        |
///                        |        |
///                        +--------+
///                          waitFor
///
/// P1  - Publisher 1
/// RB  - RingBuffer
/// SB  - SequenceBarrier
/// EP1 - EventProcessor 1
/// </code>
/// </summary>
public class OneToOneSequencedThroughputTest_BatchPublisher : IThroughputTest
{
    private const int _batchSize = 10;
    private const int _bufferSize = 1024 * 64;
    private const long _iterations = 1000L * 1000L * 100L;

    ///////////////////////////////////////////////////////////////////////////////////////////////

    private static readonly long _expectedResult = PerfTestUtil.AccumulatedAddition(_iterations) * _batchSize;
    private readonly ProgramOptions _options;
    private readonly RingBuffer<PerfEvent> _ringBuffer;
    private readonly AdditionEventHandler _handler;
    private readonly IEventProcessor<PerfEvent> _eventProcessor;

    public OneToOneSequencedThroughputTest_BatchPublisher(ProgramOptions options)
    {
        _options = options;
        _handler = new AdditionEventHandler(options.GetCustomCpu(1));
        _ringBuffer = RingBuffer<PerfEvent>.CreateSingleProducer(PerfEvent.EventFactory, _bufferSize, options.GetWaitStrategy());
        var sequenceBarrier = _ringBuffer.NewBarrier();
        _eventProcessor = EventProcessorFactory.Create(_ringBuffer, sequenceBarrier, _handler);
        _ringBuffer.AddGatingSequences(_eventProcessor.Sequence);
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////

    public int RequiredProcessorCount => 2;

    public long Run(ThroughputSessionContext sessionContext)
    {
        var expectedCount = _eventProcessor.Sequence.Value + _iterations * _batchSize;
        _handler.Reset(expectedCount);
        var startTask = _eventProcessor.Start();
        startTask.Wait(TimeSpan.FromSeconds(5));

        using var _ = ThreadAffinityUtil.SetThreadAffinity(_options.GetCustomCpu(0), ThreadPriority.Highest);

        sessionContext.Start();

        var ringBuffer = _ringBuffer;
        for (var i = 0; i < _iterations; i++)
        {
            var hi = ringBuffer.Next(_batchSize);
            var lo = hi - (_batchSize - 1);
            for (var l = lo; l <= hi; l++)
            {
                ringBuffer[l].Value = (i);
            }
            ringBuffer.Publish(lo, hi);
        }

        _handler.WaitForSequence();
        sessionContext.Stop();
        PerfTestUtil.WaitForEventProcessorSequence(expectedCount, _eventProcessor);

        var shutdownTask = _eventProcessor.Halt();
        shutdownTask.Wait(2000);

        sessionContext.SetBatchData(_handler.BatchesProcessed, _iterations * _batchSize);

        PerfTestUtil.FailIfNot(_expectedResult, _handler.Value, $"Handler should have processed {_expectedResult} events, but was: {_handler.Value}");

        return _batchSize * _iterations;
    }
}
