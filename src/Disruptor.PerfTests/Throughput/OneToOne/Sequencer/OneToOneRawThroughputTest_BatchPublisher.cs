﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Processing;

namespace Disruptor.PerfTests.Throughput.OneToOne.Sequencer;

/// <summary>
/// Unicast a series of items between 1 publisher and 1 event processor.
/// Use batch publication (<see cref="RingBuffer.Next(int)"/>.
///
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
public class OneToOneRawThroughputTest_BatchPublisher : IThroughputTest
{
    private const int _bufferSize = 1024 * 64;
    private const long _iterations = 1000L * 1000L * 200L;

    private readonly SingleProducerSequencer _sequencer = new(_bufferSize, new YieldingWaitStrategy());
    private readonly MyRunnable _myRunnable;

    public OneToOneRawThroughputTest_BatchPublisher()
    {
        _myRunnable = new MyRunnable(_sequencer);
        _sequencer.AddGatingSequences(_myRunnable.Sequence);
    }

    public int RequiredProcessorCount => 2;

    public long Run(ThroughputSessionContext sessionContext)
    {
        int batchSize = 10;
        var latch = new ManualResetEvent(false);
        var expectedCount = _myRunnable.Sequence.Value + _iterations * batchSize;
        _myRunnable.Reset(latch, expectedCount);
        Task.Run(() => _myRunnable.Run());
        sessionContext.Start();

        var sequencer = _sequencer;

        for (long i = 0; i < _iterations; i++)
        {
            var next = sequencer.Next(batchSize);
            sequencer.Publish(next - (batchSize - 1), next);
        }

        latch.WaitOne();
        sessionContext.Stop();
        WaitForEventProcessorSequence(expectedCount);

        return _iterations * batchSize;
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
        public readonly Sequence Sequence = new(-1);
        private readonly SequenceBarrier _barrier;

        public MyRunnable(ISequencer sequencer)
        {
            _barrier = sequencer.NewBarrier(null);
        }

        public void Reset(ManualResetEvent latch, long expectedCount)
        {
            _latch = latch;
            _expectedCount = expectedCount;
        }

        public void Run()
        {
            Run(default(EventProcessorHelpers.NoopPublishedSequenceReader));
        }

        private void Run<T>(T publishedSequenceReader)
            where T : struct, IPublishedSequenceReader
        {
            var expected = _expectedCount;

            try
            {
                long processed;
                do
                {
                    var nextSequence = Sequence.Value + 1;
                    var availableSequence = _barrier.WaitFor(nextSequence).UnsafeAvailableSequence;
                    processed = publishedSequenceReader.GetHighestPublishedSequence(nextSequence, availableSequence);
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
