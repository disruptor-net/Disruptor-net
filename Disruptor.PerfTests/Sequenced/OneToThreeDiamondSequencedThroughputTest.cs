using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.PerfTests.Support;

namespace Disruptor.PerfTests.Sequenced
{
    /// <summary>
    /// Produce an event replicated to two event proces
    ///           +-----+
    ///    +----->| EP1 |------+
    ///    |      +-----+      |
    ///    |                   v
    /// +----+              +-----+
    /// | P1 |              | EP3 |
    /// +----+              +-----+
    ///    |                   ^
    ///    |      +-----+      |
    ///    +----->| EP2 |------+
    ///           +-----+
    /// Disruptor:
    /// ==========
    ///                    track to prevent wrap
    ///              +-------------------------------+
    ///              |                               |
    ///              |                               v
    /// +----+    +====+               +=====+    +----
    /// | P1 |---\| RB |/--------------| SB2 |/---| EP3
    /// +----+    +====+               +=====+    +----
    ///      claim   ^  get               |   waitFor
    ///              |                    |
    ///           +=====+    +-----+      |
    ///           | SB1 |/---| EP1 |/-----+
    ///           +=====+    +-----+      |
    ///              ^                    |
    ///              |       +-----+      |
    ///              +-------| EP2 |/-----+
    ///             waitFor  +-----+
    /// 
    /// P1  - Publisher 1
    /// RB  - RingBuffer
    /// SB1 - SequenceBarrier 1
    /// EP1 - EventProcessor 1
    /// EP2 - EventProcessor 2
    /// SB2 - SequenceBarrier 2
    /// EP3 - EventProcessor 3
    /// </summary>
    public class OneToThreeDiamondSequencedThroughputTest : IThroughputfTest
    {
        private const int _bufferSize = 1024 * 8;
        private const long _iterations = 1000L * 1000L * 100L;

        private readonly long _expectedResult;

        private readonly RingBuffer<FizzBuzzEvent> _ringBuffer = RingBuffer<FizzBuzzEvent>.CreateSingleProducer(() => new FizzBuzzEvent(), _bufferSize, new YieldingWaitStrategy());
        private readonly BatchEventProcessor<FizzBuzzEvent> _batchProcessorFizz;
        private readonly BatchEventProcessor<FizzBuzzEvent> _batchProcessorBuzz;
        private readonly BatchEventProcessor<FizzBuzzEvent> _batchProcessorFizzBuzz;
        private readonly FizzBuzzEventHandler _fizzBuzzHandler;

        public OneToThreeDiamondSequencedThroughputTest()
        {
            var sequenceBarrier = _ringBuffer.NewBarrier();

            var fizzHandler = new FizzBuzzEventHandler(FizzBuzzStep.Fizz);
            _batchProcessorFizz = new BatchEventProcessor<FizzBuzzEvent>(_ringBuffer, sequenceBarrier, fizzHandler);

            var buzzHandler = new FizzBuzzEventHandler(FizzBuzzStep.Buzz);
            _batchProcessorBuzz = new BatchEventProcessor<FizzBuzzEvent>(_ringBuffer, sequenceBarrier, buzzHandler);

            var sequenceBarrierFizzBuzz = _ringBuffer.NewBarrier(_batchProcessorFizz.Sequence, _batchProcessorBuzz.Sequence);

            _fizzBuzzHandler = new FizzBuzzEventHandler(FizzBuzzStep.FizzBuzz);
            _batchProcessorFizzBuzz = new BatchEventProcessor<FizzBuzzEvent>(_ringBuffer, sequenceBarrierFizzBuzz, _fizzBuzzHandler);

            var temp = 0L;
            for (long i = 0; i < _iterations; i++)
            {
                var fizz = 0 == (i % 3L);
                var buzz = 0 == (i % 5L);

                if (fizz && buzz)
                {
                    ++temp;
                }
            }
            _expectedResult = temp;

            _ringBuffer.AddGatingSequences(_batchProcessorFizzBuzz.Sequence);
        }

        public int RequiredProcessorCount => 4;

        public long Run(Stopwatch stopwatch)
        {
            var latch = new ManualResetEvent(false);
            _fizzBuzzHandler.Reset(latch, _batchProcessorFizzBuzz.Sequence.Value + _iterations);

            Task.Run(() => _batchProcessorFizz.Run());
            Task.Run(() => _batchProcessorBuzz.Run());
            Task.Run(() => _batchProcessorFizzBuzz.Run());

            stopwatch.Start();

            for (long i = 0; i < _iterations; i++)
            {
                var sequence = _ringBuffer.Next();
                _ringBuffer[sequence].Value = i;
                _ringBuffer.Publish(sequence);
            }

            latch.WaitOne();
            stopwatch.Stop();

            _batchProcessorFizz.Halt();
            _batchProcessorBuzz.Halt();
            _batchProcessorFizzBuzz.Halt();

            PerfTestUtil.FailIfNot(_expectedResult, _fizzBuzzHandler.FizzBuzzCounter);

            return _iterations;
        }
    }
}