using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Disruptor.PerfTests.Support;
using NUnit.Framework;

namespace Disruptor.PerfTests.Pipeline3Step
{
    [TestFixture]
    public class Pipeline3StepBlockingCollectionPerfTest:AbstractPipeline3StepPerfTest
    {
        private readonly BlockingCollection<long[]> _stepOneQueue = new BlockingCollection<long[]>(Size);
        private readonly BlockingCollection<long> _stepTwoQueue = new BlockingCollection<long>(Size);
        private readonly BlockingCollection<long> _stepThreeQueue = new BlockingCollection<long>(Size);

        private readonly FunctionQueueEventProcessor _stepOneQueueEventProcessor;
        private readonly FunctionQueueEventProcessor _stepTwoQueueEventProcessor;
        private readonly FunctionQueueEventProcessor _stepThreeQueueEventProcessor;

        public Pipeline3StepBlockingCollectionPerfTest() : base(1*Million)
        {
            _stepOneQueueEventProcessor = new FunctionQueueEventProcessor(FunctionStep.One, _stepOneQueue, _stepTwoQueue, _stepThreeQueue, Iterations);
            _stepTwoQueueEventProcessor = new FunctionQueueEventProcessor(FunctionStep.Two, _stepOneQueue, _stepTwoQueue, _stepThreeQueue, Iterations);
            _stepThreeQueueEventProcessor = new FunctionQueueEventProcessor(FunctionStep.Three, _stepOneQueue, _stepTwoQueue, _stepThreeQueue, Iterations);
        }

        public override long RunPass()
        {
            _stepThreeQueueEventProcessor.Reset();

            ThreadPool.QueueUserWorkItem(_ => _stepOneQueueEventProcessor.Run());
            ThreadPool.QueueUserWorkItem(_ => _stepTwoQueueEventProcessor.Run());
            ThreadPool.QueueUserWorkItem(_ => _stepThreeQueueEventProcessor.Run());

            var sw = Stopwatch.StartNew();

            var operandTwo = OperandTwoInitialValue;
            for (long i = 0; i < Iterations; i++)
            {
                var values = new long[2];
                values[0] = i;
                values[1] = operandTwo--;
                _stepOneQueue.Add(values);
            }

            while (!_stepThreeQueueEventProcessor.Done)
            {
                // busy spin
            }

            var opsPerSecond = (Iterations * 1000L) / sw.ElapsedMilliseconds;

            Assert.AreEqual(ExpectedResult, _stepThreeQueueEventProcessor.StepThreeCounter);

            return opsPerSecond;
        }

        [Test]
        public override void RunPerformanceTest()
        {
            RunAsUnitTest();
        }
    }
}