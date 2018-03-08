using System;
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
    public class OneToThreePipelineSequencedThroughputTest : IThroughputTest
    {
        private const int _bufferSize = 1024 * 8;
        private const long _iterations = 1000L * 1000L * 100L;
        private readonly ExecutorService<FunctionEvent> _executor = new ExecutorService<FunctionEvent>();

        private const long _operandTwoInitialValue = 777L;
        private readonly long _expectedResult;

        private readonly RingBuffer<FunctionEvent> _ringBuffer = RingBuffer<FunctionEvent>.CreateSingleProducer(FunctionEvent.EventFactory, _bufferSize, new YieldingWaitStrategy());

        private readonly IBatchEventProcessor<FunctionEvent> _stepOneBatchProcessor;
        private readonly IBatchEventProcessor<FunctionEvent> _stepTwoBatchProcessor;
        private readonly IBatchEventProcessor<FunctionEvent> _stepThreeBatchProcessor;
        private readonly FunctionEventHandler _stepThreeFunctionHandler;

        public OneToThreePipelineSequencedThroughputTest()
        {
            var stepOneFunctionHandler = new FunctionEventHandler(FunctionStep.One);
            var stepTwoFunctionHandler = new FunctionEventHandler(FunctionStep.Two);
            _stepThreeFunctionHandler = new FunctionEventHandler(FunctionStep.Three);

            var stepOneSequenceBarrier = _ringBuffer.NewBarrier();
            _stepOneBatchProcessor = BatchEventProcessorFactory.Create(_ringBuffer, stepOneSequenceBarrier, stepOneFunctionHandler);

            var stepTwoSequenceBarrier = _ringBuffer.NewBarrier(_stepOneBatchProcessor.Sequence);
            _stepTwoBatchProcessor = BatchEventProcessorFactory.Create(_ringBuffer, stepTwoSequenceBarrier, stepTwoFunctionHandler);

            var stepThreeSequenceBarrier = _ringBuffer.NewBarrier(_stepTwoBatchProcessor.Sequence);
            _stepThreeBatchProcessor = BatchEventProcessorFactory.Create(_ringBuffer, stepThreeSequenceBarrier, _stepThreeFunctionHandler);

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

        public long Run(ThroughputSessionContext sessionContext)
        {
            var latch = new ManualResetEvent(false);
            _stepThreeFunctionHandler.Reset(latch, _stepThreeBatchProcessor.Sequence.Value + _iterations);

            var processorTask1 = _executor.Submit(_stepOneBatchProcessor);
            var processorTask2 = _executor.Submit(_stepTwoBatchProcessor);
            var processorTask3 = _executor.Submit(_stepThreeBatchProcessor);

            _stepOneBatchProcessor.WaitUntilStarted(TimeSpan.FromSeconds(5));
            _stepTwoBatchProcessor.WaitUntilStarted(TimeSpan.FromSeconds(5));
            _stepThreeBatchProcessor.WaitUntilStarted(TimeSpan.FromSeconds(5));

            sessionContext.Start();

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
            sessionContext.Stop();

            _stepOneBatchProcessor.Halt();
            _stepTwoBatchProcessor.Halt();
            _stepThreeBatchProcessor.Halt();
            Task.WaitAll(processorTask1, processorTask2, processorTask3);

            PerfTestUtil.FailIfNot(_expectedResult, _stepThreeFunctionHandler.StepThreeCounter);

            return _iterations;
        }
    }
}
