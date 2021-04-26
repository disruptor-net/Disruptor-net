using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.PerfTests.Support;

namespace Disruptor.PerfTests.External
{
    public class OneToThreeDiamondQueueThroughputTest : IThroughputTest, IExternalTest
    {
        private const int _eventProcessorCount = 3;
        private const int _bufferSize = 1024 * 8;
        private const long _iterations = 1000 * 1000 * 100;

        private readonly long _expectedResult;
        private readonly ConcurrentQueue<long> _fizzInputQueue = new ConcurrentQueue<long>();
        private readonly ConcurrentQueue<long> _buzzInputQueue = new ConcurrentQueue<long>();
        private readonly ConcurrentQueue<bool> _fizzOutputQueue = new ConcurrentQueue<bool>();
        private readonly ConcurrentQueue<bool> _buzzOutputQueue = new ConcurrentQueue<bool>();

        private readonly FizzBuzzQueueProcessor _fizzQueueProcessor;
        private readonly FizzBuzzQueueProcessor _buzzQueueProcessor;
        private readonly FizzBuzzQueueProcessor _fizzBuzzQueueProcessor;

        public OneToThreeDiamondQueueThroughputTest()
        {
            var temp = 0L;
            for (var i = 0; i < _iterations; i++)
            {
                var fizz = 0 == (i % 3L);
                var buzz = 0 == (i % 5L);
                if (fizz && buzz)
                {
                    ++temp;
                }
            }
            _expectedResult = temp;

            _fizzQueueProcessor = new FizzBuzzQueueProcessor(FizzBuzzStep.Fizz, _fizzInputQueue, _buzzInputQueue, _fizzOutputQueue, _buzzOutputQueue, _iterations - 1);
            _buzzQueueProcessor = new FizzBuzzQueueProcessor(FizzBuzzStep.Buzz, _fizzInputQueue, _buzzInputQueue, _fizzOutputQueue, _buzzOutputQueue, _iterations - 1);
            _fizzBuzzQueueProcessor = new FizzBuzzQueueProcessor(FizzBuzzStep.FizzBuzz, _fizzInputQueue, _buzzInputQueue, _fizzOutputQueue, _buzzOutputQueue, _iterations - 1);
        }

        public int RequiredProcessorCount => 4;

        public long Run(ThroughputSessionContext sessionContext)
        {
            var signal = new ManualResetEvent(false);
            _fizzBuzzQueueProcessor.Reset(signal);
            var tasks = new Task[_eventProcessorCount];
            tasks[0] = _fizzQueueProcessor.Start();
            tasks[1] = _buzzQueueProcessor.Start();
            tasks[2] = _fizzBuzzQueueProcessor.Start();

            sessionContext.Start();

            for (var i = 0; i < _iterations; i++)
            {
                _fizzInputQueue.Enqueue(i);
                _buzzInputQueue.Enqueue(i);
            }

            signal.WaitOne();
            sessionContext.Stop();

            _fizzQueueProcessor.Halt();
            _buzzQueueProcessor.Halt();
            _fizzBuzzQueueProcessor.Halt();

            Task.WaitAll(tasks);

            PerfTestUtil.FailIf(_expectedResult, 0);

            return _iterations;
        }
    }
}
