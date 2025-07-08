using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Disruptor.PerfTests.Support;
using Disruptor.Processing;

namespace Disruptor.PerfTests.Throughput.OneToOne.EventHandler;

/// <summary>
/// Unicast a series of items between 1 publisher and 1 event processor.
/// Use <see cref="MultiProducerSequencer"/>.
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
public class OneToOneSequencedThroughputTest_Multi : IThroughputTest
{
    private const int _bufferSize = 1024 * 64;
    private const long _iterations = 1000L * 1000L * 100L;

    private static readonly long _expectedResult = PerfTestUtil.AccumulatedAddition(_iterations);
    private readonly ProgramOptions _options;
    private readonly RingBuffer<PerfEvent> _ringBuffer;
    private readonly AdditionEventHandler _eventHandler;
    private readonly IEventProcessor<PerfEvent> _eventProcessor;

    public OneToOneSequencedThroughputTest_Multi(ProgramOptions options)
    {
        _options = options;
        _eventHandler = new AdditionEventHandler(options.GetCustomCpu(1));
        _ringBuffer = RingBuffer<PerfEvent>.CreateMultiProducer(PerfEvent.EventFactory, _bufferSize, options.GetWaitStrategy());
        var sequenceBarrier = _ringBuffer.NewBarrier();
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

        using var _ = ThreadAffinityUtil.SetThreadAffinity(_options.GetCustomCpu(0), ThreadPriority.Highest);

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
        shutdownTask.Wait(2000);

        sessionContext.SetBatchData(_eventHandler.BatchesProcessed, _iterations);

        PerfTestUtil.FailIfNot(_expectedResult, _eventHandler.Value, $"Handler should have processed {_expectedResult} events, but was: {_eventHandler.Value}");

        return _iterations;
    }
}
