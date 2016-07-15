﻿using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.PerfTests.Support;
using LongArrayPublisher = System.Action<System.Threading.CountdownEvent, Disruptor.RingBuffer<long[]>, long, long>;

namespace Disruptor.PerfTests.Sequenced
{
    /// <summary>
    /// Sequence a series of events from multiple publishers going to one event processor.
    /// Disruptor:
    /// ==========
    ///             track to prevent wrap
    ///             +--------------------+
    ///             |                    |
    ///             |                    |
    /// +----+    +====+    +====+       |
    /// | P1 |--->| RB |--->| SB |--+    |
    /// +----+    +====+    +====+  |    |
    ///                             |    v
    /// +----+    +====+    +====+  | +----+
    /// | P2 |--->| RB |--->| SB |--+>| EP |
    /// +----+    +====+    +====+  | +----+
    ///                             |
    /// +----+    +====+    +====+  |
    /// | P3 |--->| RB |--->| SB |--+
    /// +----+    +====+    +====+
    /// P1 - Publisher 1
    /// P2 - Publisher 2
    /// P3 - Publisher 3
    /// RB - RingBuffer
    /// SB - SequenceBarrier
    /// EP - EventProcessor
    /// </summary>
    public class ThreeToThreeSequencedThroughputTest : IThroughputTest
    {
        private const int _numPublishers = 3;
        private const int _arraySize = 3;
        private const int _bufferSize = 1024 * 64;
        private const long _iterations = 1000L * 1000L * 180L;
        private readonly CountdownEvent _cyclicBarrier = new CountdownEvent(_numPublishers + 1);

        private readonly RingBuffer<long[]>[] _buffers = new RingBuffer<long[]>[_numPublishers];
        private readonly ISequenceBarrier[] _barriers = new ISequenceBarrier[_numPublishers];
        private readonly LongArrayPublisher[] _valuePublishers = new LongArrayPublisher[_numPublishers];

        private readonly LongArrayEventHandler _handler = new LongArrayEventHandler();
        private readonly MultiBufferBatchEventProcessor<long[]> _batchEventProcessor;

        public ThreeToThreeSequencedThroughputTest()
        {
            for (var i = 0; i < _numPublishers; i++)
            {
                _buffers[i] = RingBuffer<long[]>.CreateSingleProducer(() => new long[_arraySize], _bufferSize, new YieldingWaitStrategy());
                _barriers[i] = _buffers[i].NewBarrier();
                _valuePublishers[i] = ValuePublisher;
            }

            _batchEventProcessor = new MultiBufferBatchEventProcessor<long[]>(_buffers, _barriers, _handler);

            for (var i = 0; i < _numPublishers; i++)
            {
                _buffers[i].AddGatingSequences(_batchEventProcessor.GetSequences()[i]);
            }
        }

        public int RequiredProcessorCount => 4;

        public long Run(Stopwatch stopwatch)
        {
            _cyclicBarrier.Reset();

            var latch = new ManualResetEvent(false);
            _handler.Reset(latch, _iterations);

            var futures = new Task[_numPublishers];
            for (var i = 0; i < _numPublishers; i++)
            {
                var index = i;
                futures[i] = Task.Run(() => _valuePublishers[index](_cyclicBarrier, _buffers[index], _iterations / _numPublishers, _arraySize));
            }
            var processorTask = Task.Run(() => _batchEventProcessor.Run());

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

            return _iterations * _arraySize;
        }

        private static void ValuePublisher(CountdownEvent countdownEvent, RingBuffer<long[]> ringBuffer, long iterations, long arraySize)
        {
            countdownEvent.Signal();
            countdownEvent.Wait();

            for (long i = 0; i < iterations; i++)
            {
                var sequence = ringBuffer.Next();
                var eventData = ringBuffer[sequence];
                for (var j = 0; j < arraySize; j++)
                {
                    eventData[j] = i + j;
                }
                ringBuffer.Publish(sequence);
            }
        }
    }
}