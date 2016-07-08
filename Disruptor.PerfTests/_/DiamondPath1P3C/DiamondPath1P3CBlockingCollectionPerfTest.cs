using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Disruptor.PerfTests.Support;
using NUnit.Framework;

namespace Disruptor.PerfTests.DiamondPath1P3C
{
    [TestFixture]
    public class DiamondPath1P3CBlockingCollectionPerfTest:AbstractDiamondPath1P3CPerfTest
    {
        private readonly BlockingCollection<long> _fizzInputQueue = new BlockingCollection<long>(Size);
        private readonly BlockingCollection<long> _buzzInputQueue = new BlockingCollection<long>(Size);
        private readonly BlockingCollection<bool> _fizzOutputQueue = new BlockingCollection<bool>(Size);
        private readonly BlockingCollection<bool> _buzzOutputQueue = new BlockingCollection<bool>(Size);

        private readonly FizzBuzzQueueEventProcessor _fizzQueueEventProcessor;
        private readonly FizzBuzzQueueEventProcessor _buzzQueueEventProcessor;
        private readonly FizzBuzzQueueEventProcessor _fizzBuzzQueueEventProcessor;

        public DiamondPath1P3CBlockingCollectionPerfTest()
            : base(1 * Million)
        {
            _fizzQueueEventProcessor = new FizzBuzzQueueEventProcessor(FizzBuzzStep.Fizz, _fizzInputQueue, _buzzInputQueue, _fizzOutputQueue, _buzzOutputQueue, Iterations);
            _buzzQueueEventProcessor = new FizzBuzzQueueEventProcessor(FizzBuzzStep.Buzz, _fizzInputQueue, _buzzInputQueue, _fizzOutputQueue, _buzzOutputQueue, Iterations);
            _fizzBuzzQueueEventProcessor = new FizzBuzzQueueEventProcessor(FizzBuzzStep.FizzBuzz, _fizzInputQueue, _buzzInputQueue, _fizzOutputQueue, _buzzOutputQueue, Iterations);
        }

        public override long RunPass()
        {
            _fizzBuzzQueueEventProcessor.Reset();

            (new Thread(_fizzQueueEventProcessor.Run) { Name = "Fizz" }).Start();
            (new Thread(_buzzQueueEventProcessor.Run) { Name = "Buzz" }).Start();
            (new Thread(_fizzBuzzQueueEventProcessor.Run) { Name = "FizzBuzz" }).Start();

            var sw = Stopwatch.StartNew();

            for (long i = 0; i < Iterations; i++)
            {
                _fizzInputQueue.Add(i);
                _buzzInputQueue.Add(i);
            }

            while (!_fizzBuzzQueueEventProcessor.Done)
            {
                // busy spin
            }

            var opsPerSecond = (Iterations * 1000L) / sw.ElapsedMilliseconds;

            Assert.AreEqual(ExpectedResult, _fizzBuzzQueueEventProcessor.FizzBuzzCounter);

            return opsPerSecond;
        }

        [Test]
        public override void RunPerformanceTest()
        {
            RunAsUnitTest();
        }
    }
}