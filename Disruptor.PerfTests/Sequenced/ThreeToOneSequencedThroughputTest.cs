﻿using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.PerfTests.Support;
using Disruptor.Scheduler;
using ValuePublisher = System.Action<System.Threading.CountdownEvent, Disruptor.RingBuffer<Disruptor.PerfTests.Support.ValueEvent>, long>;

namespace Disruptor.PerfTests.Sequenced
{
    /// <summary>
    /// Sequence a series of events from multiple publishers going to one event processor.
    /// +----+
    /// | P1 |------+
    /// +----+      |
    ///             v
    /// +----+    +-----+
    /// | P1 |--->| EP1 |
    /// +----+    +-----+
    ///             ^
    /// +----+      |
    /// | P3 |------+
    /// +----+
    /// Disruptor:
    /// ==========
    ///             track to prevent wrap
    ///             +--------------------+
    ///             |                    |
    ///             |                    v
    /// +----+    +====+    +====+    +-----+
    /// | P1 |--->| RB |/---| SB |    | EP1 |
    /// +----+    +====+    +====+    +-----+
    ///             ^   get    ^         |
    /// +----+      |          |         |
    /// | P2 |------+          +---------+
    /// +----+      |            waitFor
    ///             |
    /// +----+      |
    /// | P3 |------+
    /// +----+
    /// P1  - Publisher 1
    /// P2  - Publisher 2
    /// P3  - Publisher 3
    /// RB  - RingBuffer
    /// SB  - SequenceBarrier
    /// EP1 - EventProcessor 1
    /// </summary>
    public class ThreeToOneSequencedThroughputTest : IThroughputTest
    {
        private const int _numPublishers = 3;
        private const int _bufferSize = 1024 * 64;
        private const long _iterations = 1000L * 1000L * 20L;

        private readonly CountdownEvent _cyclicBarrier = new CountdownEvent(_numPublishers + 1);
        private readonly RingBuffer<ValueEvent> _ringBuffer = RingBuffer<ValueEvent>.CreateMultiProducer(() => new ValueEvent(), _bufferSize, new BusySpinWaitStrategy());
        private readonly TaskScheduler _scheduler = new RoundRobinThreadAffinedTaskScheduler(5);
        private readonly ISequenceBarrier _sequenceBarrier;
        private readonly ValueAdditionEventHandler _handler = new ValueAdditionEventHandler();
        private readonly BatchEventProcessor<ValueEvent> _batchEventProcessor;
        private readonly ValuePublisher[] _valuePublishers = new ValuePublisher[_numPublishers];

        public ThreeToOneSequencedThroughputTest()
        {
            _sequenceBarrier = _ringBuffer.NewBarrier();
            _batchEventProcessor = new BatchEventProcessor<ValueEvent>(_ringBuffer, _sequenceBarrier, _handler);
            for (var i = 0; i < _numPublishers; i++)
            {
                _valuePublishers[i] = ValuePublisher;
            }
            _ringBuffer.AddGatingSequences(_batchEventProcessor.Sequence);
        }

        public int RequiredProcessorCount => 4;

        public long Run(Stopwatch stopwatch)
        {
            _cyclicBarrier.Reset();
            var latch = new ManualResetEvent(false);
            _handler.Reset(latch, _batchEventProcessor.Sequence.Value + ((_iterations / _numPublishers) * _numPublishers));

            var futures = new Task[_numPublishers];
            for (var i = 0; i < _numPublishers; i++)
            {
                var index = i;
                futures[i] = Task.Factory.StartNew(() => _valuePublishers[index](_cyclicBarrier, _ringBuffer, _iterations), CancellationToken.None, TaskCreationOptions.None, _scheduler);
            }
            var processorTask = Task.Factory.StartNew(() => _batchEventProcessor.Run(), CancellationToken.None, TaskCreationOptions.None, _scheduler);

            stopwatch.Start();
            _cyclicBarrier.Signal();
            _cyclicBarrier.Wait();

            for (var i = 0; i < _numPublishers; i++)
            {
                futures[i].Wait();
            }

            latch.WaitOne();

            stopwatch.Stop();
            _batchEventProcessor.Halt();
            processorTask.Wait(2000);

            return _iterations;
        }

        private static void ValuePublisher(CountdownEvent countdownEvent, RingBuffer<ValueEvent> ringBuffer, long iterations)
        {
            countdownEvent.Signal();
            countdownEvent.Wait();

            for (long i = 0; i < iterations; i++)
            {
                var sequence = ringBuffer.Next();
                var eventData = ringBuffer[sequence];
                eventData.Value = i;
                ringBuffer.Publish(sequence);
            }
        }
    }
}