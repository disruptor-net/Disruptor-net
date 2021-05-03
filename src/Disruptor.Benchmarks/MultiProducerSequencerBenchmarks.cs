using BenchmarkDotNet.Attributes;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Configs;

namespace Disruptor.Benchmarks
{
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class MultiProducerSequencerBenchmarks
    {
        private readonly MultiProducerSequencer _sequencer;
        private readonly MultiProducerSequencerRef _sequencerRef;
        private readonly MultiProducerSequencerPointer _sequencerPointer;
        private long _sequence;

        public MultiProducerSequencerBenchmarks()
        {
            _sequencer = new MultiProducerSequencer(1024, new BusySpinWaitStrategy());
            _sequencerRef = new MultiProducerSequencerRef(1024, new BusySpinWaitStrategy());
            _sequencerPointer = new MultiProducerSequencerPointer(1024, new BusySpinWaitStrategy());
            _sequence = _sequencer.Next();
            _sequencerRef.Next();
            _sequencerPointer.Next();
        }

        [Benchmark, BenchmarkCategory("Publish")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Publish()
        {
            _sequencer.Publish(_sequence);
        }

        [Benchmark(Baseline = true), BenchmarkCategory("Publish")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void PublishRef()
        {
            _sequencerRef.Publish(_sequence);
        }

        [Benchmark, BenchmarkCategory("Publish")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void PublishPointer()
        {
            _sequencerPointer.Publish(_sequence);
        }

        [Benchmark, BenchmarkCategory("IsAvailable")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool IsAvailable()
        {
            return _sequencer.IsAvailable(_sequence);
        }

        [Benchmark(Baseline = true), BenchmarkCategory("IsAvailable")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool IsAvailableRef()
        {
            return _sequencerRef.IsAvailable(_sequence);
        }

        [Benchmark, BenchmarkCategory("IsAvailable")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool IsAvailablePointer()
        {
            return _sequencerPointer.IsAvailable(_sequence);
        }
    }
}
