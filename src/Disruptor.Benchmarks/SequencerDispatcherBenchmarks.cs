using BenchmarkDotNet.Attributes;

namespace Disruptor.Benchmarks
{
    public class SequencerDispatcherBenchmarks
    {
        private const int _operationsPerInvoke = 100;

        private readonly SingleProducerSequencer _singleProducerSequencer;
        private readonly ISequencer _sequencer;
        private readonly SequencerDispatcher _dispatcher;

        public SequencerDispatcherBenchmarks()
        {
            _singleProducerSequencer = new SingleProducerSequencer(1024, new YieldingWaitStrategy());
            _sequencer = _singleProducerSequencer;
            _dispatcher = new SequencerDispatcher(_sequencer);
        }

        [Benchmark(Baseline = true, OperationsPerInvoke = _operationsPerInvoke)]
        public void UseInterface()
        {
            for (var i = 0; i < _operationsPerInvoke; i++)
            {
                var sequence = _sequencer.Next();
                _sequencer.Publish(sequence);
            }
        }

        [Benchmark(OperationsPerInvoke = _operationsPerInvoke)]
        public void UseDispatcher()
        {
            for (var i = 0; i < _operationsPerInvoke; i++)
            {
                var sequence = _dispatcher.Next();
                _dispatcher.Publish(sequence);
            }
        }

        [Benchmark(OperationsPerInvoke = _operationsPerInvoke)]
        public void UseDirectCalls()
        {
            for (var i = 0; i < _operationsPerInvoke; i++)
            {
                var sequence = _singleProducerSequencer.Next();
                _singleProducerSequencer.Publish(sequence);
            }
        }
    }
}
