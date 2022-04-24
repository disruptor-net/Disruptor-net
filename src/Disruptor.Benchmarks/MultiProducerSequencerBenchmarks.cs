using BenchmarkDotNet.Attributes;
using System.Runtime.CompilerServices;

namespace Disruptor.Benchmarks;

public class MultiProducerSequencerBenchmarks
{
    private readonly MultiProducerSequencer _sequencer;

    public MultiProducerSequencerBenchmarks()
    {
        _sequencer = new MultiProducerSequencer(1024, new BusySpinWaitStrategy());

        Sequence = 42;
    }

    public long Sequence { get; set; }

    public class MultiProducerSequencer_Publish : MultiProducerSequencerBenchmarks
    {
        [Benchmark]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Invoke()
        {
            _sequencer.Publish(Sequence);
        }
    }

    public class MultiProducerSequencer_IsAvailable : MultiProducerSequencerBenchmarks
    {
        [Benchmark]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool Invoke()
        {
            return _sequencer.IsAvailable(Sequence);
        }
    }

    public class MultiProducerSequencer_NextPublish : MultiProducerSequencerBenchmarks
    {
        [Benchmark]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Invoke()
        {
            var sequence = _sequencer.Next();
            _sequencer.Publish(sequence);
        }
    }
}
