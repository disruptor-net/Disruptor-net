using BenchmarkDotNet.Attributes;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Configs;
using Disruptor.Benchmarks.Reference;

namespace Disruptor.Benchmarks;

public class MultiProducerSequencerBenchmarks
{
    private readonly MultiProducerSequencer _sequencer;
    private readonly MultiProducerSequencerRef2 _sequencerRef2;

    public MultiProducerSequencerBenchmarks()
    {
        _sequencer = new MultiProducerSequencer(1024, new BusySpinWaitStrategy());
        _sequencerRef2 = new MultiProducerSequencerRef2(1024, new BusySpinWaitStrategy());

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

        [Benchmark(Baseline = true)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void InvokeRef2()
        {
            _sequencerRef2.Publish(Sequence);
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

        [Benchmark(Baseline = true)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool InvokeRef2()
        {
            return _sequencerRef2.IsAvailable(Sequence);
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

        [Benchmark(Baseline = true)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void InvokeRef2()
        {
            var sequence = _sequencerRef2.Next();
            _sequencerRef2.Publish(sequence);
        }
    }
}
