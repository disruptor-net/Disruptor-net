using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.PerfTests.Support;
using Disruptor.Processing;

namespace Disruptor.PerfTests.Throughput.ThreeToOne;

/// <summary>
/// Sequence a series of events from multiple publishers going to one event processor.
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
public class ThreeToOneSequencedThroughputTest : IThroughputTest
{
    private const int _numPublishers = 3;
    private const int _bufferSize = 1024 * 64;
    private const long _iterations = 1000L * 1000L * 20L;

    private readonly CountdownEvent _cyclicBarrier = new(_numPublishers + 1);
    private readonly RingBuffer<PerfEvent> _ringBuffer = RingBuffer<PerfEvent>.CreateMultiProducer(PerfEvent.EventFactory, _bufferSize, new BusySpinWaitStrategy());
    private readonly AdditionEventHandler _handler = new();
    private readonly ProgramOptions _options;
    private readonly IEventProcessor<PerfEvent> _eventProcessor;

    public ThreeToOneSequencedThroughputTest(ProgramOptions options)
    {
        _options = options;
        var sequenceBarrier = _ringBuffer.NewBarrier();
        _eventProcessor = EventProcessorFactory.Create(_ringBuffer, sequenceBarrier, _handler);
        _ringBuffer.AddGatingSequences(_eventProcessor.Sequence);
    }

    public int RequiredProcessorCount => 4;

    public long Run(ThroughputSessionContext sessionContext)
    {
        _cyclicBarrier.Reset();
        _handler.Reset(_eventProcessor.Sequence.Value + (_iterations / _numPublishers) * _numPublishers);

        var publisherTasks = Enumerable.Range(0, _numPublishers)
                                       .Select(i => Task.Run(() => PublishValues(i, _cyclicBarrier, _ringBuffer)))
                                       .ToArray();

        var startTask = _eventProcessor.Start(new BackgroundThreadTaskScheduler(_options.GetCustomCpu(0)));
        startTask.Wait(TimeSpan.FromSeconds(5));

        sessionContext.Start();
        _cyclicBarrier.Signal();
        _cyclicBarrier.Wait();

        for (var i = 0; i < _numPublishers; i++)
        {
            publisherTasks[i].Wait();
        }

        _handler.WaitForSequence();

        sessionContext.Stop();

        var shutdownTask = _eventProcessor.Halt();
        shutdownTask.Wait(2000);

        sessionContext.SetBatchData(_handler.BatchesProcessed, _iterations);

        return _iterations;
    }

    private void PublishValues(int publisherIndex, CountdownEvent countdownEvent, RingBuffer<PerfEvent> ringBuffer)
    {
        using var _ = ThreadAffinityUtil.SetThreadAffinity(_options.GetCustomCpu(1 + publisherIndex), ThreadPriority.Highest);

        var iterations = _iterations / _numPublishers;

        countdownEvent.Signal();
        countdownEvent.Wait();

        for (long i = 0; i < iterations; i++)
        {
            var sequence = ringBuffer.Next();
            var eventData = ringBuffer[sequence];
            eventData.Value = i;
            ringBuffer.Publish(sequence);
        }
    }
}
