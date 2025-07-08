using System;
using System.Runtime.CompilerServices;
using Disruptor.PerfTests.Support;
using Disruptor.Processing;

namespace Disruptor.PerfTests.Throughput.OneToOne.AsyncBatchEventHandler;

/// <summary>
/// Unicast a series of items between 1 publisher and 1 event processor.
/// Use <seealso cref="IAsyncBatchEventHandler{T}"/>.
///
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
/// | P1 |---\| RB |/---| SB |   | EP1 |
/// +----+    +====+    +====+   +-----+
///      claim       get   ^        |
///                        |        |
///                        +--------+
///                          waitFor
///
/// P1  - Publisher 1
/// RB  - RingBuffer
/// SB  - SequenceBarrier
/// EP1 - EventProcessor 1
/// </summary>
public class OneToOneSequencedThroughputTest_AsyncBatchHandler : IThroughputTest
{
    private const int _bufferSize = 1024 * 64;
    private const long _iterations = 1000L * 1000L * 100L;

    private readonly RingBuffer<PerfEvent> _ringBuffer;
    private readonly AdditionAsyncBatchEventHandler _eventHandler;
    private readonly long _expectedResult = PerfTestUtil.AccumulatedAddition(_iterations);
    private readonly IAsyncEventProcessor<PerfEvent> _eventProcessor;

    public OneToOneSequencedThroughputTest_AsyncBatchHandler()
    {
        _eventHandler = new AdditionAsyncBatchEventHandler();
        _ringBuffer = RingBuffer<PerfEvent>.CreateSingleProducer(PerfEvent.EventFactory, _bufferSize, new AsyncWaitStrategy());
        var sequenceBarrier = _ringBuffer.NewAsyncBarrier();
        _eventProcessor = EventProcessorFactory.Create(_ringBuffer, sequenceBarrier, _eventHandler);
        _ringBuffer.AddGatingSequences(_eventProcessor.Sequence);
    }

    public int RequiredProcessorCount => 2;

    [MethodImpl(512)]
    public long Run(ThroughputSessionContext sessionContext)
    {
        long expectedCount = _eventProcessor.Sequence.Value + _iterations;

        _eventHandler.Reset(expectedCount);
        var startTask = _eventProcessor.Start();
        startTask.Wait(TimeSpan.FromSeconds(5));

        sessionContext.Start();

        var ringBuffer = _ringBuffer;

        for (long i = 0; i < _iterations; i++)
        {
            var s = ringBuffer.Next();
            ringBuffer[s].Value = i;
            ringBuffer.Publish(s);
        }

        _eventHandler.WaitForSequence();
        sessionContext.Stop();
        PerfTestUtil.WaitForEventProcessorSequence(expectedCount, _eventProcessor);
        var shutdownTask = _eventProcessor.Halt();

        if (!shutdownTask.Wait(2000))
            throw new InvalidOperationException("Process task should be completed");

        sessionContext.SetBatchData(_eventHandler.BatchesProcessed, _iterations);

        PerfTestUtil.FailIfNot(_expectedResult, _eventHandler.Value, $"Handler should have processed {_expectedResult} events, but was: {_eventHandler.Value}");

        return _iterations;
    }
}
