using BenchmarkDotNet.Attributes;

namespace Disruptor.Benchmarks
{
    public class SequencerDispatcherBenchmarks
    {
        private readonly ISequencer _sequencer;
        private readonly SequencerDispatcher _dispatcher;

        public SequencerDispatcherBenchmarks()
        {
            _sequencer = new SingleProducerSequencer(1024);
            _dispatcher = new SequencerDispatcher(_sequencer);
        }

        [Benchmark(Baseline = true)]
        public void UseInterface()
        {
            var sequence = _sequencer.Next();
            _sequencer.Publish(sequence);
        }

        [Benchmark]
        public void UseDispatcher()
        {
            var sequence = _dispatcher.Next();
            _dispatcher.Publish(sequence);
        }
    }
}
