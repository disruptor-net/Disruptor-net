using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Scheduler;

namespace Disruptor.PerfTests.Raw
{
    /// <summary>
    /// UniCast a series of items between 1 publisher and 1 event processor.
    /// +----+    +-----+
    /// | P1 |--->| EP1 |
    /// +----+    +-----+
    /// 
    /// Queue Based:
    /// ============
    /// 
    ///        put take
    /// +----+    +====+    +-----+
    /// | P1 |---\| Q1 |/---| EP1 |
    /// +----+    +====+    +-----+
    /// 
    /// P1  - Publisher 1
    /// Q1  - Queue 1
    /// EP1 - EventProcessor 1
    /// 
    /// Disruptor:
    /// ==========
    ///              track to prevent wrap
    ///              +------------------+
    ///              |                  |
    ///              |                  v
    /// +----+    +====+    +====+   +-----+
    /// | P1 |--->| RB |/---| SB |   | EP1 |
    /// +----+    +====+    +====+   +-----+
    ///      claim get    ^        |
    ///                        |        |
    ///                        +--------+
    ///                          waitFor
    /// P1  - Publisher 1
    /// RB  - RingBuffer
    /// SB  - SequenceBarrier
    /// EP1 - EventProcessor 1
    /// </summary>
    public class OneToOneRawThroughputTest : IThroughputTest
    {
        private const int _bufferSize = 1024 * 64;
        private const long _iterations = 1000L * 1000L * 200L;

        private readonly SingleProducerSequencer _sequencer = new SingleProducerSequencer(_bufferSize, new YieldingWaitStrategy());
        private readonly MyRunnable _myRunnable;
        private readonly TaskScheduler _taskScheduler;

        public OneToOneRawThroughputTest()
        {
            _taskScheduler = RoundRobinThreadAffinedTaskScheduler.IsSupported ? new RoundRobinThreadAffinedTaskScheduler(2) : TaskScheduler.Default;
            _myRunnable = new MyRunnable(_sequencer);
            _sequencer.AddGatingSequences(_myRunnable.Sequence);
        }

        public int RequiredProcessorCount => 2;

        public long Run(ThroughputSessionContext sessionContext)
        {
            var latch = new ManualResetEvent(false);
            var expectedCount = _myRunnable.Sequence.Value + _iterations;
            _myRunnable.Reset(latch, expectedCount);
            var consumerTask = Task.Factory.StartNew(() => _myRunnable.Run(), CancellationToken.None, TaskCreationOptions.None, _taskScheduler);
            sessionContext.Start();

            var sequencer = _sequencer;

            var producerTask = Task.Factory.StartNew(() =>
            {
                for (long i = 0; i < _iterations; i++)
                {
                    var next = sequencer.Next();
                    sequencer.Publish(next);
                }

                latch.WaitOne();
            }, CancellationToken.None, TaskCreationOptions.None, _taskScheduler);

            producerTask.Wait();
            sessionContext.Stop();
            WaitForEventProcessorSequence(expectedCount);

            consumerTask.Wait();

            return _iterations;
        }

        private void WaitForEventProcessorSequence(long expectedCount)
        {
            while (_myRunnable.Sequence.Value != expectedCount)
            {
                Thread.Sleep(1);
            }
        }

        private class MyRunnable
        {
            private ManualResetEvent _latch;
            private long _expectedCount;
            public readonly Sequence Sequence = new Sequence(-1);
            private readonly ISequenceBarrier _barrier;

            public MyRunnable(ISequencer sequencer)
            {
                _barrier = sequencer.NewBarrier();
            }

            public void Reset(ManualResetEvent latch, long expectedCount)
            {
                _latch = latch;
                _expectedCount = expectedCount;
            }

            public void Run()
            {
                var expected = _expectedCount;

                try
                {
                    long processed;
                    do
                    {
                        processed = _barrier.WaitFor(Sequence.Value + 1);
                        Sequence.SetValue(processed);
                    }
                    while (processed < expected);

                    _latch.Set();
                    Sequence.SetValueVolatile(processed);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }
    }
}
