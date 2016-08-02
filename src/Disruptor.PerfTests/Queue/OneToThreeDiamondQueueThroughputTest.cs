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
        private readonly BlockingCollection<long> _fizzInputQueue = new BlockingCollection<long>(new LockFreeBoundedQueue<long>(_bufferSize), _bufferSize);
        private readonly BlockingCollection<long> _buzzInputQueue = new BlockingCollection<long>(new LockFreeBoundedQueue<long>(_bufferSize), _bufferSize);
        private readonly BlockingCollection<bool> _fizzOutputQueue = new BlockingCollection<bool>(new LockFreeBoundedQueue<bool>(_bufferSize), _bufferSize);
        private readonly BlockingCollection<bool> _buzzOutputQueue = new BlockingCollection<bool>(new LockFreeBoundedQueue<bool>(_bufferSize), _bufferSize);

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
                while (!_fizzInputQueue.TryAdd(i))
                    Thread.Yield();
                while (!_buzzInputQueue.TryAdd(i))
                    Thread.Yield();
            }

            signal.WaitOne();
            stopwatch.Stop();

            _fizzQueueProcessor.Halt();
            _buzzQueueProcessor.Halt();
            _fizzBuzzQueueProcessor.Halt();

            Task.WaitAll(tasks);

            return _iterations;
        }

        public int RequiredProcessorCount { get; } = 4;
    }
}