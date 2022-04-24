using System;
using System.Runtime.CompilerServices;
using System.Threading;
using BenchmarkDotNet.Attributes;

namespace Disruptor.Benchmarks;

public abstract class SequenceBarrierBenchmarks
{
    public const int OperationsPerInvoke = 1_00_000;

    protected readonly RingBuffer<Event> _requesterRingBuffer;
    protected readonly MultiProducerSequencer _requesterSequencer;
    protected readonly RingBuffer<Event> _replierRingBuffer;
    protected readonly MultiProducerSequencer _replierSequencer;

    protected SequenceBarrierBenchmarks()
    {
        _requesterSequencer = new MultiProducerSequencer(1024, new YieldingWaitStrategy());
        _requesterRingBuffer = new RingBuffer<Event>(() => new Event(), _requesterSequencer);

        _replierSequencer = new MultiProducerSequencer(1024, new YieldingWaitStrategy());
        _replierRingBuffer = new RingBuffer<Event>(() => new Event(), _replierSequencer);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    protected void BeforePublication()
    {
        Thread.SpinWait(20);
    }

    public class Event
    {
        public long Value { get; set; }
    }
}
