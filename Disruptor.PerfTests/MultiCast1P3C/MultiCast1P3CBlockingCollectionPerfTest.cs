using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Disruptor.PerfTests.Support;
using NUnit.Framework;

namespace Disruptor.PerfTests.MultiCast1P3C
{
    [TestFixture]
    public class MultiCast1P3CBlockingCollectionPerfTest : AbstractMultiCast1P3CPerfTest
    {
        private readonly BlockingCollection<long>[] _blockingQueues = new[]
                                                                {
                                                                    new BlockingCollection<long>(Size),
                                                                    new BlockingCollection<long>(Size),
                                                                    new BlockingCollection<long>(Size)
                                                                };

        private readonly ValueMutationQueueEventProcessor[] _queueEventProcessors = new ValueMutationQueueEventProcessor[NumEventProcessors];

        public MultiCast1P3CBlockingCollectionPerfTest()
            : base(1 * Million)
        {
            _queueEventProcessors[0] = new ValueMutationQueueEventProcessor(_blockingQueues[0], Operation.Addition, Iterations);
            _queueEventProcessors[1] = new ValueMutationQueueEventProcessor(_blockingQueues[1], Operation.Substraction, Iterations);
            _queueEventProcessors[2] = new ValueMutationQueueEventProcessor(_blockingQueues[2], Operation.And, Iterations);
        }

        public override long RunPass()
        {
            for (var i = 0; i < NumEventProcessors; i++)
            {
                _queueEventProcessors[i].Reset();
                (new Thread(_queueEventProcessors[i].Run) { Name = string.Format("Queue event processor {0}", i) }).Start();
            }

            var sw = Stopwatch.StartNew();

            for (long i = 0; i < Iterations; i++)
            {
                _blockingQueues[0].Add(i);
                _blockingQueues[1].Add(i);
                _blockingQueues[2].Add(i);
            }

            while (!AllEventProcessorsAreDone())
            {
                // busy spin
            }

            var opsPerSecond = (Iterations * 1000L) / sw.ElapsedMilliseconds;
            for (var i = 0; i < NumEventProcessors; i++)
            {
                Assert.AreEqual(ExpectedResults[i], _queueEventProcessors[i].Value);
            }

            return opsPerSecond;
        }

        [Test]
        public override void RunPerformanceTest()
        {
            RunAsUnitTest();
        }


        private bool AllEventProcessorsAreDone()
        {
            return _queueEventProcessors[0].Done && _queueEventProcessors[1].Done && _queueEventProcessors[2].Done;
        }
    }
}