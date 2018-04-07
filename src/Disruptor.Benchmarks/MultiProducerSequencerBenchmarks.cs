using BenchmarkDotNet.Attributes;
using System.Runtime.CompilerServices;

namespace Disruptor.Benchmarks
{
    public unsafe class MultiProducerSequencerBenchmarks
    {
        private MultiProducerSequencer _sequencer;
        private MultiProducerSequencerPointer _sequencerPointer;
        private long _sequence;
        private long _sequencePointer;

        public MultiProducerSequencerBenchmarks()
        {
            _sequencer = new MultiProducerSequencer(1024, new BusySpinWaitStrategy());
            _sequencerPointer = new MultiProducerSequencerPointer(1024, new BusySpinWaitStrategy());
            _sequence = _sequencer.Next();
            _sequencerPointer.Next();
        }

        [Benchmark]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Publish()
        {
            _sequencer.Publish(_sequence);
        }

        [Benchmark]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool IsAvailable()
        {
            return _sequencer.IsAvailable(_sequence);
        }

        [Benchmark]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void PublishPointer()
        {
            _sequencerPointer.Publish(_sequence);
        }

        [Benchmark]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool IsAvailablePointer()
        {
            return _sequencerPointer.IsAvailable(_sequence);
        }
    }
}
