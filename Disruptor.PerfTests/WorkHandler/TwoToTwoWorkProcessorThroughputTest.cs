using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.PerfTests.Support;

namespace Disruptor.PerfTests.WorkHandler
{
    /// <summary>
    /// Sequence a series of events from multiple publishers going to multiple work processors.
    /// 
    /// +----+                  +-----+
    /// | P1 |---+          +-->| WP1 |
    /// +----+   |  +-----+ |   +-----+
    ///          +->| RB1 |-+
    /// +----+   |  +-----+ |   +-----+
    /// | P2 |---+          +-->| WP2 |
    /// +----+                  +-----+
    /// 
    /// P1  - Publisher 1
    /// P2  - Publisher 2
    /// RB  - RingBuffer
    /// WP1 - EventProcessor 1
    /// WP2 - EventProcessor 2
    /// </summary>
    public class TwoToTwoWorkProcessorThroughputTest : IPerfTest
    {
        private const int _numPublishers = 2;
        private const int _bufferSize = 1024 * 64;
        private const long _iterations = 1000L * 1000L * 1L;
        private readonly CountdownEvent _cyclicBarrier = new CountdownEvent(_numPublishers + 1);

        private readonly RingBuffer<ValueEvent> _ringBuffer = RingBuffer<ValueEvent>.CreateMultiProducer(() => new ValueEvent(), _bufferSize, new BusySpinWaitStrategy());
        private readonly Sequence _workSequence = new Sequence();
        private readonly ValueAdditionWorkHandler[] _handlers = new ValueAdditionWorkHandler[2];
        private readonly WorkProcessor<ValueEvent>[] _workProcessors = new WorkProcessor<ValueEvent>[2];
        private readonly ValuePublisher[] _valuePublishers = new ValuePublisher[_numPublishers];

        public TwoToTwoWorkProcessorThroughputTest()
        {
            var sequenceBarrier = _ringBuffer.NewBarrier();
            _handlers[0] = new ValueAdditionWorkHandler();
            _handlers[1] = new ValueAdditionWorkHandler();

            _workProcessors[0] = new WorkProcessor<ValueEvent>(_ringBuffer, sequenceBarrier, _handlers[0], new IgnoreExceptionHandler(), _workSequence);
            _workProcessors[1] = new WorkProcessor<ValueEvent>(_ringBuffer, sequenceBarrier, _handlers[1], new IgnoreExceptionHandler(), _workSequence);

            for (var i = 0; i < _numPublishers; i++)
            {
                _valuePublishers[i] = new ValuePublisher(_cyclicBarrier, _ringBuffer, _iterations);
            }

            _ringBuffer.AddGatingSequences(_workProcessors[0].Sequence, _workProcessors[1].Sequence);
        }

        public int RequiredProcessorCount => 4;

        public long Run(Stopwatch stopwatch)
        {
            _cyclicBarrier.Reset();

            var expected = _ringBuffer.Cursor + (_numPublishers * _iterations);
            var futures = new Task[_numPublishers];
            for (var i = 0; i < _numPublishers; i++)
            {
                var index = i;
                futures[i] = Task.Run(() => _valuePublishers[index].Run());
            }

            foreach (var processor in _workProcessors)
            {
                Task.Run(() => processor.Run());
            }

            stopwatch.Start();
            _cyclicBarrier.Signal();
            _cyclicBarrier.Wait();

            for (var i = 0; i < _numPublishers; i++)
            {
                futures[i].Wait();
            }

            while (_workSequence.Value < expected)
            {
                Thread.Yield();
            }

            stopwatch.Stop();

            Thread.Sleep(1000);

            foreach (var processor in _workProcessors)
            {
                processor.Halt();
            }

            return _iterations;
        }

        private class ValuePublisher
        {
            private readonly CountdownEvent _cyclicBarrier;
            private readonly RingBuffer<ValueEvent> _ringBuffer;
            private readonly long _iterations;

            public ValuePublisher(CountdownEvent cyclicBarrier, RingBuffer<ValueEvent> ringBuffer, long iterations)
            {
                _cyclicBarrier = cyclicBarrier;
                _ringBuffer = ringBuffer;
                _iterations = iterations;
            }

            public void Run()
            {
                _cyclicBarrier.Signal();
                _cyclicBarrier.Wait();

                for (long i = 0; i < _iterations; i++)
                {
                    var sequence = _ringBuffer.Next();
                    var @event = _ringBuffer[sequence];
                    @event.Value = i;
                    _ringBuffer.Publish(sequence);
                }
            }
        }

        private class ValueAdditionWorkHandler : IWorkHandler<ValueEvent>
        {
            public long Total { get; private set; }

            public void OnEvent(ValueEvent evt)
            {
                var value = evt.Value;
                Total += value;
            }
        }
    }
}