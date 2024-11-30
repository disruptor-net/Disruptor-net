using System;
using System.Threading;
using Disruptor.PerfTests.Support;
using Disruptor.Processing;

namespace Disruptor.PerfTests.Throughput.OneToOne.EventHandler;

/// <summary>
/// Unicast a series of items between 1 publisher and 1 event processor.
///
/// <code>
/// +----+    +-----+
/// | P1 |--->| EP1 |
/// +----+    +-----+
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
/// P1  - Publisher 1
/// RB  - RingBuffer
/// SB  - SequenceBarrier
/// EP1 - EventProcessor 1
/// </code>
/// </summary>
public class OneToOneSequencedThroughputTest_LongArray : IThroughputTest
{
    private const int _bufferSize = 1024 * 1;
    private const long _iterations = 1000L * 1000L * 1L;
    private const int _arraySize = 2 * 1024;

    ///////////////////////////////////////////////////////////////////////////////////////////////

    private readonly ProgramOptions _options;
    private readonly RingBuffer<long[]> _ringBuffer;
    private readonly LongArrayEventHandler _handler;
    private readonly IEventProcessor<long[]> _eventProcessor;

    public OneToOneSequencedThroughputTest_LongArray(ProgramOptions options)
    {
        _options = options;
        _handler = new LongArrayEventHandler(options.GetCustomCpu(1));
        _ringBuffer = RingBuffer<long[]>.CreateSingleProducer(() => new long[_arraySize], _bufferSize, new YieldingWaitStrategy());
        var sequenceBarrier = _ringBuffer.NewBarrier();
        _eventProcessor = EventProcessorFactory.Create(_ringBuffer, sequenceBarrier, _handler);
        _ringBuffer.AddGatingSequences(_eventProcessor.Sequence);
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////

    public int RequiredProcessorCount => 2;

    public long Run(ThroughputSessionContext sessionContext)
    {
        var expectedCount = _eventProcessor.Sequence.Value + _iterations;

        var signal = new ManualResetEvent(false);
        _handler.Reset(signal, _iterations);
        var processorTask = _eventProcessor.Start();

        _eventProcessor.WaitUntilStarted(TimeSpan.FromSeconds(5));

        using var _ = ThreadAffinityUtil.SetThreadAffinity(_options.GetCustomCpu(0), ThreadPriority.Highest);

        sessionContext.Start();

        var ringBuffer = _ringBuffer;
        for (var i = 0; i < _iterations; i++)
        {
            var next = ringBuffer.Next();
            var @event = ringBuffer[next];
            for (var j = 0; j < @event.Length; j++)
            {
                @event[j] = i;
            }
            ringBuffer.Publish(next);
        }

        signal.WaitOne();
        sessionContext.Stop();
        WaitForEventProcessorSequence(expectedCount);
        _eventProcessor.Halt();
        processorTask.Wait(2000);

        sessionContext.SetBatchData(_handler.BatchesProcessed, _iterations);

        PerfTestUtil.FailIf(0, _handler.Value, "Handler has not processed any event");

        return _iterations * _arraySize;
    }

    private void WaitForEventProcessorSequence(long expectedCount)
    {
        while (_eventProcessor.Sequence.Value != expectedCount)
        {
            Thread.Sleep(1);
        }
    }
}
