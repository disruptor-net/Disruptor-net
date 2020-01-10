using BenchmarkDotNet.Attributes;

namespace Disruptor.Benchmarks
{
    public class RingBufferPublicationBenchmarks
    {
        private readonly RingBuffer<Event> _ringBuffer;
        private ISequenceBarrier _sequenceBarrier;

        public RingBufferPublicationBenchmarks()
        {
            _ringBuffer = new RingBuffer<Event>(() => new Event(), new SingleProducerSequencer(4096, new BusySpinWaitStrategy()));
            _sequenceBarrier = _ringBuffer.NewBarrier();
        }

        [Benchmark]
        public void PublishAndWait()
        {
            var sequence = _ringBuffer.Next();
            try
            {
                var data = _ringBuffer[sequence];
                data.L1 = 1;
                data.L2 = 2;
                data.L3 = 3;
                data.L4 = 4;
                data.L5 = 5;
                data.L6 = 6;
            }
            finally
            {
                _ringBuffer.Publish(sequence);
            }

            _sequenceBarrier.WaitFor(sequence);
        }

        // [Benchmark(Baseline = true)]
        public void PublishClassic()
        {
            var sequence = _ringBuffer.Next();
            try
            {
                var data = _ringBuffer[sequence];
                data.L1 = 1;
                data.L2 = 2;
                data.L3 = 3;
                data.L4 = 4;
                data.L5 = 5;
                data.L6 = 6;
            }
            finally
            {
                _ringBuffer.Publish(sequence);
            }
        }

        // [Benchmark]
        public void PublishScope()
        {
            using (var scope = _ringBuffer.PublishEvent())
            {
                var data = scope.Event();
                data.L1 = 1;
                data.L2 = 2;
                data.L3 = 3;
                data.L4 = 4;
                data.L5 = 5;
                data.L6 = 6;
            }
        }

        // [Benchmark]
        public void TryPublishClassic()
        {
            if (!_ringBuffer.TryNext(out var sequence))
                return;

            try
            {
                var data = _ringBuffer[sequence];
                data.L1 = 1;
                data.L2 = 2;
                data.L3 = 3;
                data.L4 = 4;
                data.L5 = 5;
                data.L6 = 6;
            }
            finally
            {
                _ringBuffer.Publish(sequence);
            }
        }

        // [Benchmark]
        public void TryPublishScope()
        {
            using (var scope = _ringBuffer.TryPublishEvent())
            {
                if (!scope.TryGetEvent(out var eventRef))
                    return;

                var data = eventRef.Event();
                data.L1 = 1;
                data.L2 = 2;
                data.L3 = 3;
                data.L4 = 4;
                data.L5 = 5;
                data.L6 = 6;
            }
        }

        // [Benchmark]
        public void PublishManyClassic()
        {
            var sequence = _ringBuffer.Next(2);
            try
            {
                var data = _ringBuffer[sequence - 1];
                data.L1 = 1;
                data.L2 = 2;
                data.L3 = 3;
                data.L4 = 4;
                data.L5 = 5;
                data.L6 = 6;
            }
            finally
            {
                _ringBuffer.Publish(sequence - 1, sequence);
            }
        }

        // [Benchmark]
        public void PublishManyScope()
        {
            using (var scope = _ringBuffer.PublishEvents(2))
            {
                var data = scope.Event(0);
                data.L1 = 1;
                data.L2 = 2;
                data.L3 = 3;
                data.L4 = 4;
                data.L5 = 5;
                data.L6 = 6;
            }
        }

        // [Benchmark]
        public void TryPublishManyClassic()
        {
            if (!_ringBuffer.TryNext(1, out var s))
                return;

            try
            {
                var data = _ringBuffer[s + 0];
                data.L1 = 1;
                data.L2 = 2;
                data.L3 = 3;
                data.L4 = 4;
                data.L5 = 5;
                data.L6 = 6;
            }
            finally
            {
                _ringBuffer.Publish(s - 1, s);
            }
        }

        // [Benchmark]
        public void TryPublishManyScope()
        {
            using (var scope = _ringBuffer.TryPublishEvents(2))
            {
                if (!scope.TryGetEvents(out var eventsRef))
                    return;

                var data = eventsRef.Event(0);
                data.L1 = 1;
                data.L2 = 2;
                data.L3 = 3;
                data.L4 = 4;
                data.L5 = 5;
                data.L6 = 6;
            }
        }

        public class Event
        {
            public long L1;
            public long L2;
            public long L3;
            public long L4;
            public long L5;
            public long L6;
        }
    }
}
