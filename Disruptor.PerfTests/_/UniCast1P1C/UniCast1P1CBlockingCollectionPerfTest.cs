using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.PerfTests.Support;
using NUnit.Framework;

namespace Disruptor.PerfTests.UniCast1P1C
{
    [TestFixture]
    public class UniCast1P1CBlockingCollectionPerfTest : AbstractUniCast1P1CPerfTest
    {
        private readonly BlockingCollection<long> _queue = new BlockingCollection<long>(BufferSize);
        private readonly ValueAdditionQueueEventProcessor _queueEventProcessor;

        public UniCast1P1CBlockingCollectionPerfTest() : base(2 * Million)
        {
            _queueEventProcessor = new ValueAdditionQueueEventProcessor(_queue, Iterations);
        }

        public override long RunPass()
        {
            _queueEventProcessor.Reset();

            var cts = new CancellationTokenSource();
            Task.Factory.StartNew(_queueEventProcessor.Run, cts.Token);

            var sw = Stopwatch.StartNew();

            for (var i = 0; i < Iterations; i++)
            {
                _queue.Add(i);
            }

            while (!_queueEventProcessor.Done)
            {
                // busy spin
            }

            var opsPerSecond = (Iterations * 1000L) / (sw.ElapsedMilliseconds);

            cts.Cancel(true);

            Assert.AreEqual(ExpectedResult, _queueEventProcessor.Value, "RunQueuePass");

            return opsPerSecond;
        }

        [Test]
        public override void RunPerformanceTest()
        {
            RunAsUnitTest();
        }
    }
}