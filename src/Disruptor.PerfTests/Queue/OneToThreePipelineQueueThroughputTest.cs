using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.PerfTests.Support;

namespace Disruptor.PerfTests.Queue
{
    public class OneToThreePipelineQueueThroughputTest : IThroughputTest, IQueueTest
    {
        private const int _eventProcessorCount = 3;
        private const int _bufferSize = 1024 * 8;
        private const long _iterations = 1000 * 1000 * 10;
        private const long _operandTwoInitialValue = 777;
        private readonly long _expectedResult;
        private readonly IExecutor _executor = new BasicExecutor(TaskScheduler.Current);

        private readonly ConcurrentQueue<long[]> _stepOneQueue = new ConcurrentQueue<long[]>();
        private readonly ConcurrentQueue<long> _stepTwoQueue = new ConcurrentQueue<long>();
        private readonly ConcurrentQueue<long> _stepThreeQueue = new ConcurrentQueue<long>();

        private readonly FunctionQueueProcessor _stepOneQueueProcessor;
        private readonly FunctionQueueProcessor _stepTwoQueueProcessor;
        private readonly FunctionQueueProcessor _stepThreeQueueProcessor;

        public OneToThreePipelineQueueThroughputTest()
        {
            var temp = 0L;
            var operandTwo = _operandTwoInitialValue;

            for (long i = 0; i < _iterations; i++)
            {
                var stepOneResult = i + operandTwo--;
                var stepTwoResult = stepOneResult + 3;

                if ((stepTwoResult & 4L) == 4L)
                {
                    ++temp;
                }
            }

            _expectedResult = temp;

            _stepOneQueueProcessor = new FunctionQueueProcessor(FunctionStep.One, _stepOneQueue, _stepTwoQueue, _stepThreeQueue, _iterations - 1);
            _stepTwoQueueProcessor = new FunctionQueueProcessor(FunctionStep.Two, _stepOneQueue, _stepTwoQueue, _stepThreeQueue, _iterations - 1);
            _stepThreeQueueProcessor = new FunctionQueueProcessor(FunctionStep.Three, _stepOneQueue, _stepTwoQueue, _stepThreeQueue, _iterations - 1);
        }

        public int RequiredProcessorCount => 4;

        public long Run(Stopwatch stopwatch)
        {
            var signal = new ManualResetEvent(false);
            _stepThreeQueueProcessor.Reset(signal);

            var tasks = new Task[_eventProcessorCount];
            tasks[0] = _executor.Execute(_stepOneQueueProcessor.Run);
            tasks[1] = _executor.Execute(_stepTwoQueueProcessor.Run);
            tasks[2] = _executor.Execute(_stepThreeQueueProcessor.Run);

            stopwatch.Start();

            long operandTwo = _operandTwoInitialValue;
            for (long i = 0; i < _iterations; i++)
            {
                long[] values = new long[2];
                values[0] = i;
                values[1] = operandTwo--;
                _stepOneQueue.Enqueue(values);
            }

            signal.WaitOne();
            stopwatch.Stop();

            _stepOneQueueProcessor.Halt();
            _stepTwoQueueProcessor.Halt();
            _stepThreeQueueProcessor.Halt();

            Task.WaitAll(tasks);

            PerfTestUtil.FailIf(_expectedResult, 0);

            return _iterations;
        }
    }
}