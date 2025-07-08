using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.PerfTests.Support;
using Disruptor.Processing;

namespace Disruptor.PerfTests.Throughput.TwoToTwo;

/// <summary>
/// Sequence a series of events from multiple publishers going to multiple work processors.
///
/// +----+                  +-----+
/// | P1 |---+          +-->| WP1 |
/// +----+   |  +-----+ |   +-----+
///          +->| RB1 |-+
/// +----+   |  +-----+ |   +-----+
/// | P2 |---+          +-->| WP2 |
/// +----+                  +-----+
///
/// P1  - Publisher 1
/// P2  - Publisher 2
/// RB  - RingBuffer
/// WP1 - EventProcessor 1
/// WP2 - EventProcessor 2
/// </summary>
public class TwoToTwoWorkProcessorThroughputTest : IThroughputTest
{
    private const int _numPublishers = 2;
    private const int _bufferSize = 1024 * 64;
    private const long _iterations = 1000L * 1000L * 1L;
    private readonly CountdownEvent _cyclicBarrier = new(_numPublishers + 1);

    private readonly RingBuffer<PerfEvent> _ringBuffer = RingBuffer<PerfEvent>.CreateMultiProducer(PerfEvent.EventFactory, _bufferSize, new BusySpinWaitStrategy());
    private readonly Sequence _workSequence = new();
    private readonly ValuePublisher[] _valuePublishers = new ValuePublisher[_numPublishers];

    public TwoToTwoWorkProcessorThroughputTest()
    {
        for (var i = 0; i < _numPublishers; i++)
        {
            _valuePublishers[i] = new ValuePublisher(_cyclicBarrier, _ringBuffer);
        }
    }

    public int RequiredProcessorCount => 4;

    public long Run(ThroughputSessionContext sessionContext)
    {
        var sequenceBarrier = _ringBuffer.NewBarrier();

        var workProcessors = new[]
        {
            new WorkProcessor<PerfEvent>(_ringBuffer, sequenceBarrier, new ValueAdditionWorkHandler(), new IgnoreExceptionHandler<PerfEvent>(), _workSequence),
            new WorkProcessor<PerfEvent>(_ringBuffer, sequenceBarrier, new ValueAdditionWorkHandler(), new IgnoreExceptionHandler<PerfEvent>(), _workSequence),
        };

        _ringBuffer.AddGatingSequences(workProcessors.Select(x => x.Sequence).ToArray());

        _cyclicBarrier.Reset();

        var expected = _ringBuffer.Cursor + (_numPublishers * _iterations);
        var valuePublisherTasks = _valuePublishers.Select(x => x.Start()).ToArray();

        foreach (var workProcessor in workProcessors)
        {
            var startTask = workProcessor.Start();
            startTask.Wait(TimeSpan.FromSeconds(5));
        }

        sessionContext.Start();
        _cyclicBarrier.Signal();
        _cyclicBarrier.Wait();

        Task.WaitAll(valuePublisherTasks);

        while (_workSequence.Value < expected)
        {
            Thread.Yield();
        }

        sessionContext.Stop();

        Thread.Sleep(1000);

        foreach (var workProcessor in workProcessors)
        {
            workProcessor.Halt();
            _ringBuffer.RemoveGatingSequence(workProcessor.Sequence);
        }

        return _iterations;
    }

    private class ValuePublisher
    {
        private readonly CountdownEvent _cyclicBarrier;
        private readonly RingBuffer<PerfEvent> _ringBuffer;

        public ValuePublisher(CountdownEvent cyclicBarrier, RingBuffer<PerfEvent> ringBuffer)
        {
            _cyclicBarrier = cyclicBarrier;
            _ringBuffer = ringBuffer;
        }

        public Task Start()
        {
            return Task.Run(Run);
        }

        public void Run()
        {
            _cyclicBarrier.Signal();
            _cyclicBarrier.Wait();

            for (long i = 0; i < _iterations; i++)
            {
                var sequence = _ringBuffer.Next();
                var @event = _ringBuffer[sequence];
                @event.Value = i;
                _ringBuffer.Publish(sequence);
            }
        }
    }

    private class ValueAdditionWorkHandler : IWorkHandler<PerfEvent>
    {
        public long Total { get; private set; }

        public void OnEvent(PerfEvent evt)
        {
            var value = evt.Value;
            Total += value;
        }
    }
}
