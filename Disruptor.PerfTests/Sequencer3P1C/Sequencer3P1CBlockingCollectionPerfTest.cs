using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Disruptor.PerfTests.Support;
using NUnit.Framework;

namespace Disruptor.PerfTests.Sequencer3P1C
{
    [TestFixture]
    public class Sequencer3P1CBlockingCollectionPerfTest : AbstractSequencer3P1CPerfTest
    {
        private readonly BlockingCollection<long> _blockingQueue = new BlockingCollection<long>(Size);
        private readonly ValueAdditionQueueEventProcessor _queueEventProcessor;
        private readonly ValueQueueProducer[] _valueQueueProducers;
        private readonly Barrier _testStartBarrier = new Barrier(NumProducers + 1);

        public Sequencer3P1CBlockingCollectionPerfTest()
            : base(1 * Million)
        {
            _queueEventProcessor = new ValueAdditionQueueEventProcessor(_blockingQueue, Iterations);
            _testStartBarrier = new Barrier(NumProducers + 1);
            _valueQueueProducers = new ValueQueueProducer[NumProducers];

            for (int i = 0; i < NumProducers; i++)
            {
                _valueQueueProducers[i] = new ValueQueueProducer(_testStartBarrier, _blockingQueue, Iterations);
            }
        }

        public override long RunPass()
        {
            _queueEventProcessor.Reset();

            for (var i = 0; i < NumProducers; i++)
            {
                (new Thread(_valueQueueProducers[i].Run) { Name = "Queue producer " + i }).Start();
            }
            (new Thread(_queueEventProcessor.Run) { Name = "Queue event processor" }).Start();

            var sw = Stopwatch.StartNew();
            _testStartBarrier.SignalAndWait();

            while (!_queueEventProcessor.Done)
            {
                // busy spin
            }

            var opsPerSecond = (NumProducers * Iterations * 1000L) / sw.ElapsedMilliseconds;

            return opsPerSecond;
        }

        [Test]
        public override void RunPerformanceTest()
        {
            RunAsUnitTest();
        }
    }
}