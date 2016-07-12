using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.PerfTests.Support;

namespace Disruptor.PerfTests.Sequenced
{
    /// <summary>
    /// Pipeline a series of stages from a publisher to ultimate event processor.
    /// Each event processor depends on the output of the event processor.
    /// 
    /// +----+    +-----+    +-----+    +-----+
    /// | P1 |--->| EP1 |--->| EP2 |--->| EP3 |
    /// +----+    +-----+    +-----+    +-----+
    /// 
    /// Disruptor:
    /// ==========
    ///                           track to prevent wrap
    ///              +----------------------------------------------------------------+
    ///              |                                                                |
    ///              |                                                                v
    /// +----+    +====+    +=====+    +-----+    +=====+    +-----+    +=====+    +-----+
    /// | P1 |--->| RB |    | SB1 |/---| EP1 |/---| SB2 |/---| EP2 |/---| SB3 |/---| EP3 |
    /// +----+    +====+    +=====+    +-----+    +=====+    +-----+    +=====+    +-----+
    ///      claim   ^  get    |   waitFor           |   waitFor           |  waitFor
    ///              |         |                     |                     |
    ///              +---------+---------------------+---------------------+
    ///        
    /// P1  - Publisher 1
    /// RB  - RingBuffer
    /// SB1 - SequenceBarrier 1
    /// EP1 - EventProcessor 1
    /// SB2 - SequenceBarrier 2
    /// EP2 - EventProcessor 2
    /// SB3 - SequenceBarrier 3
    /// EP3 - EventProcessor 3
    /// </summary>
    public class OneToThreePipelineSequencedThroughputTest : IPerfTest
    {
        private const int _bufferSize = 1024 * 8;
        private const long _iterations = 1000L * 1000L * 100L;
        private readonly ExecutorService<FunctionEvent> _executor = new ExecutorService<FunctionEvent>();

        private const long _operandTwoInitialValue = 777L;
        private readonly long _expectedResult;

        private readonly RingBuffer<FunctionEvent> _ringBuffer = RingBuffer<FunctionEvent>.CreateSingleProducer(() => new FunctionEvent(), _bufferSize, new YieldingWaitStrategy());

        private readonly BatchEventProcessor<FunctionEvent> _stepOneBatchProcessor;
        private readonly BatchEventProcessor<FunctionEvent> _stepTwoBatchProcessor;
        private readonly BatchEventProcessor<FunctionEvent> _stepThreeBatchProcessor;
        private readonly FunctionEventHandler _stepThreeFunctionHandler;

        public OneToThreePipelineSequencedThroughputTest()
        {
            var stepOneFunctionHandler = new FunctionEventHandler(FunctionStep.One);
            var stepTwoFunctionHandler = new FunctionEventHandler(FunctionStep.Two);
            _stepThreeFunctionHandler = new FunctionEventHandler(FunctionStep.Three);

            var stepOneSequenceBarrier = _ringBuffer.NewBarrier();
            _stepOneBatchProcessor = new BatchEventProcessor<FunctionEvent>(_ringBuffer, stepOneSequenceBarrier, stepOneFunctionHandler);

            var stepTwoSequenceBarrier = _ringBuffer.NewBarrier(_stepOneBatchProcessor.Sequence);
            _stepTwoBatchProcessor = new BatchEventProcessor<FunctionEvent>(_ringBuffer, stepTwoSequenceBarrier, stepTwoFunctionHandler);

            var stepThreeSequenceBarrier = _ringBuffer.NewBarrier(_stepTwoBatchProcessor.Sequence);
            _stepThreeBatchProcessor = new BatchEventProcessor<FunctionEvent>(_ringBuffer, stepThreeSequenceBarrier, _stepThreeFunctionHandler);

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

            _ringBuffer.AddGatingSequences(_stepThreeBatchProcessor.Sequence);
        }

        public int RequiredProcessorCount => 4;

        public long Run(Stopwatch stopwatch)
        {
            var latch = new ManualResetEvent(false);
            _stepThreeFunctionHandler.Reset(latch, _stepThreeBatchProcessor.Sequence.Value + _iterations);

            _executor.Submit(_stepOneBatchProcessor);
            _executor.Submit(_stepTwoBatchProcessor);
            _executor.Submit(_stepThreeBatchProcessor);

            stopwatch.Start();

            var operandTwo = _operandTwoInitialValue;
            for (long i = 0; i < _iterations; i++)
            {
                var sequence = _ringBuffer.Next();
                var @event =
                _ringBuffer[sequence];
                @event.OperandOne = i;
                @event.OperandTwo = operandTwo--;
                _ringBuffer.Publish(sequence);
            }

            latch.WaitOne();
            stopwatch.Stop();

            _stepOneBatchProcessor.Halt();
            _stepTwoBatchProcessor.Halt();
            _stepThreeBatchProcessor.Halt();

            PerfTestUtil.FailIfNot(_expectedResult, _stepThreeFunctionHandler.StepThreeCounter);

            return _iterations;
        }
    }

    class ExecutorService<T> where T : class
    {
        public void Submit(BatchEventProcessor<T> eventProcessor)
        {
            Task.Factory.StartNew(eventProcessor.Run);
        }
    }
}