using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.PerfTests.Support;
using Disruptor.Processing;

namespace Disruptor.PerfTests.Throughput.OneToThree;

/// <summary>
/// Multicast a series of items between 1 publisher and 3 event processors.
///
///           +-----+
///    +----->| EP1 |
///    |      +-----+
///    |
/// +----+    +-----+
/// | P1 |--->| EP2 |
/// +----+    +-----+
///    |
///    |      +-----+
///    +----->| EP3 |
///           +-----+
/// Disruptor:
/// ==========
///                             track to prevent wrap
///             +--------------------+----------+----------+
///             |                    |          |          |
///             |                    v          v          v
/// +----+    +====+    +====+    +-----+    +-----+    +-----+
/// | P1 |---\| RB |/---| SB |    | EP1 |    | EP2 |    | EP3 |
/// +----+    +====+    +====+    +-----+    +-----+    +-----+
///      claim      get    ^         |          |          |
///                        |         |          |          |
///                        +---------+----------+----------+
///                                      waitFor
/// P1  - Publisher 1
/// RB  - RingBuffer
/// SB  - SequenceBarrier
/// EP1 - EventProcessor 1
/// EP2 - EventProcessor 2
/// EP3 - EventProcessor 3
/// </summary>
public class OneToThreeSequencedThroughputTest : IThroughputTest
{
    private const int _numEventProcessors = 3;
    private const int _bufferSize = 1024 * 8;
    private const long _iterations = 1000L * 1000L * 100L;

    private readonly RingBuffer<PerfEvent> _ringBuffer;
    private readonly IEventProcessor<PerfEvent>[] _eventProcessors = new IEventProcessor<PerfEvent>[_numEventProcessors];
    private readonly long[] _results = new long[_numEventProcessors];
    private readonly PerfMutationEventHandler[] _handlers = new PerfMutationEventHandler[_numEventProcessors];

    public OneToThreeSequencedThroughputTest()
    {
        for (long i = 0; i < _iterations; i++)
        {
            _results[0] = Operation.Addition.Op(_results[0], i);
            _results[1] = Operation.Subtraction.Op(_results[1], i);
            _results[2] = Operation.And.Op(_results[2], i);
        }

        _ringBuffer = RingBuffer<PerfEvent>.CreateSingleProducer(PerfEvent.EventFactory, _bufferSize, new YieldingWaitStrategy());
        var sequenceBarrier = _ringBuffer.NewBarrier();

        _handlers[0] = new PerfMutationEventHandler(Operation.Addition);
        _handlers[1] = new PerfMutationEventHandler(Operation.Subtraction);
        _handlers[2] = new PerfMutationEventHandler(Operation.And);


        for (var i = 0; i < _numEventProcessors; i++)
        {
            _eventProcessors[i] = EventProcessorFactory.Create(_ringBuffer, sequenceBarrier, _handlers[i]);
        }
        _ringBuffer.AddGatingSequences(_eventProcessors.Select(x => x.Sequence).ToArray());
    }

    public int RequiredProcessorCount => 4;

    public long Run(ThroughputSessionContext sessionContext)
    {
        var latch = new Barrier(_numEventProcessors + 1);

        for (var i = 0; i < _numEventProcessors; i++)
        {
            _handlers[i].Reset(latch, _eventProcessors[i].Sequence.Value + _iterations);
            var startTask = _eventProcessors[i].Start();
            startTask.Wait(TimeSpan.FromSeconds(5));
        }

        sessionContext.Start();

        var ringBuffer = _ringBuffer;

        for (long i = 0; i < _iterations; i++)
        {
            var sequence = ringBuffer.Next();
            ringBuffer[sequence].Value = i;
            ringBuffer.Publish(sequence);
        }

        latch.SignalAndWait();
        sessionContext.Stop();

        var shutdownTasks = new List<Task>();
        for (var i = 0; i < _numEventProcessors; i++)
        {
            shutdownTasks.Add(_eventProcessors[i].Halt());
            PerfTestUtil.FailIfNot(_results[i], _handlers[i].Value, $"Result {_results[i]} != {_handlers[i].Value}");
        }
        Task.WaitAll(shutdownTasks.ToArray());

        sessionContext.SetBatchData(_handlers.Sum(x => x.BatchesProcessedCount.Value), _numEventProcessors * _iterations);

        return _numEventProcessors * _iterations;
    }
}
