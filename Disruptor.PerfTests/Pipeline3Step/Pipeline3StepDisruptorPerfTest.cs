using System.Diagnostics;
using System.Threading;
using Disruptor.Dsl;
using Disruptor.PerfTests.Support;
using NUnit.Framework;

namespace Disruptor.PerfTests.Pipeline3Step
{
    [TestFixture]
    public class Pipeline3StepDisruptorPerfTest : AbstractPipeline3StepPerfTest
    {
        private readonly RingBuffer<FunctionEvent> _ringBuffer;
        private readonly FunctionEventHandler _stepThreeFunctionEventHandler;
        private readonly Disruptor<FunctionEvent> _disruptor;
        private readonly ManualResetEvent _mru;

        public Pipeline3StepDisruptorPerfTest()
            : base(100 * Million)
        {
            _disruptor = new Disruptor<FunctionEvent>(() => new FunctionEvent(),
                                                      new SingleThreadedClaimStrategy(Size),
                                                      new YieldingWaitStrategy());

            _mru = new ManualResetEvent(false);
            _stepThreeFunctionEventHandler = new FunctionEventHandler(FunctionStep.Three, Iterations, _mru);

            _disruptor.HandleEventsWith(new FunctionEventHandler(FunctionStep.One, Iterations, _mru))
                .Then(new FunctionEventHandler(FunctionStep.Two, Iterations, _mru))
                .Then(_stepThreeFunctionEventHandler);

            _ringBuffer = _disruptor.RingBuffer;
        }

        public override long RunPass()
        {
            _disruptor.Start();

            var sw = Stopwatch.StartNew();

            var operandTwo = OperandTwoInitialValue;
            for (long i = 0; i < Iterations; i++)
            {
                var sequence = _ringBuffer.Next();
                var evt = _ringBuffer[sequence];
                evt.OperandOne = i;
                evt.OperandTwo = operandTwo--;
                _ringBuffer.Publish(sequence);
            }

            _mru.WaitOne();

            var opsPerSecond = (Iterations * 1000L) / sw.ElapsedMilliseconds;

            _disruptor.Shutdown();

            Assert.AreEqual(ExpectedResult, _stepThreeFunctionEventHandler.StepThreeCounter);

            return opsPerSecond;
        }

        [Test]
        public override void RunPerformanceTest()
        {
            RunAsUnitTest();
        }
    }
}