using BenchmarkDotNet.Attributes;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Configs;
using Disruptor.Benchmarks.Reference;

namespace Disruptor.Benchmarks
{
    public class MultiProducerSequencerBenchmarks
    {
        private readonly MultiProducerSequencer _sequencer;
        private readonly MultiProducerSequencerRef1 _sequencerRef1;
        private readonly MultiProducerSequencerRef2 _sequencerRef2;

        public MultiProducerSequencerBenchmarks()
        {
            _sequencer = new MultiProducerSequencer(1024, new BusySpinWaitStrategy());
            _sequencerRef1 = new MultiProducerSequencerRef1(1024, new BusySpinWaitStrategy());
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

            [Benchmark]
            [MethodImpl(MethodImplOptions.NoInlining)]
            public void InvokeRef1()
            {
                _sequencerRef1.Publish(Sequence);
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

            [Benchmark]
            [MethodImpl(MethodImplOptions.NoInlining)]
            public bool InvokeRef1()
            {
                return _sequencerRef1.IsAvailable(Sequence);
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

            [Benchmark]
            [MethodImpl(MethodImplOptions.NoInlining)]
            public void InvokeRef1()
            {
                var sequence = _sequencerRef1.Next();
                _sequencerRef1.Publish(sequence);
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
}
