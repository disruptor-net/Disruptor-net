using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.PerfTests.Support;

namespace Disruptor.PerfTests.Queue
{
    public class OneToThreeDiamondQueueThroughputTest : IThroughputTest, IQueueTest
    {
        private const int _eventProcessorCount = 3;
        private const int _bufferSize = 1024 * 8;
        private const long _iterations = 1000 * 1000 * 100;
        private readonly IExecutor _executor = new BasicExecutor(TaskScheduler.Current);

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

        public long Run(Stopwatch stopwatch)
        {
            var signal = new ManualResetEvent(false);
            _fizzBuzzQueueProcessor.Reset(signal);
            var tasks = new Task[_eventProcessorCount];
            tasks[0] = _executor.Execute(_fizzQueueProcessor.Run);
            tasks[1] = _executor.Execute(_buzzQueueProcessor.Run);
            tasks[2] = _executor.Execute(_fizzBuzzQueueProcessor.Run);

            stopwatch.Start();

            for (var i = 0; i < _iterations; i++)
            {
                _fizzInputQueue.Enqueue(i);
                _buzzInputQueue.Enqueue(i);
            }

            signal.WaitOne();
            stopwatch.Stop();

            _fizzQueueProcessor.Halt();
            _buzzQueueProcessor.Halt();
            _fizzBuzzQueueProcessor.Halt();

            Task.WaitAll(tasks);

            PerfTestUtil.FailIf(_expectedResult, 0);

            return _iterations;
        }
    }
}