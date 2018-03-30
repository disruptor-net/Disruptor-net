using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

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
            _sequencePointer = _sequencerPointer.Next();
        }

        [Benchmark]
        public void Publish()
        {
            _sequencer.Publish(_sequence);
        }

        [Benchmark]
        public bool IsAvailable()
        {
            return _sequencer.IsAvailable(_sequence);
        }

        [Benchmark]
        public void PublishPointer()
        {
            _sequencerPointer.Publish(_sequence);
        }

        [Benchmark]
        public bool IsAvailablePointer()
        {
            return _sequencerPointer.IsAvailable(_sequence);
        }
    }
}
