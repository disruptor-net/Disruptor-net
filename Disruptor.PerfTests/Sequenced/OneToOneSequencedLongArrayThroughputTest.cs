﻿using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.PerfTests.Support;

namespace Disruptor.PerfTests.Sequenced
{
    /// <summary>
    /// UniCast a series of items between 1 publisher and 1 event processor.
    /// 
    /// <code>
    /// +----+    +-----+
    /// | P1 |--->| EP1 |
    /// +----+    +-----+
    /// Disruptor:
    /// ==========
    ///              track to prevent wrap
    ///              +------------------+
    ///              |                  |
    ///              |                  v
    /// +----+    +====+    +====+   +-----+
    /// | P1 |---›| RB |‹---| SB |   | EP1 |
    /// +----+    +====+    +====+   +-----+
    ///      claim      get    ^        |
    ///                        |        |
    ///                        +--------+
    ///                          waitFor
    /// P1  - Publisher 1
    /// RB  - RingBuffer
    /// SB  - SequenceBarrier
    /// EP1 - EventProcessor 1    
    /// </code>
    /// </summary>
    public class OneToOneSequencedLongArrayThroughputTest : IThroughputTest
    {
        private const int _bufferSize = 1024 * 1;
        private const long _iterations = 1000L * 1000L * 1L;
        private const int _arraySize = 2 * 1024;
        private static readonly IExecutor _executor = new BasicExecutor(TaskScheduler.Current);

        ///////////////////////////////////////////////////////////////////////////////////////////////

        private readonly RingBuffer<long[]> _ringBuffer;
        private readonly LongArrayEventHandler _handler;
        private readonly BatchEventProcessor<long[]> _batchEventProcessor;

        public OneToOneSequencedLongArrayThroughputTest()
        {
            _ringBuffer = RingBuffer<long[]>.CreateSingleProducer(() => new long[_arraySize], _bufferSize, new YieldingWaitStrategy());
            var sequenceBarrier = _ringBuffer.NewBarrier();
            _handler = new LongArrayEventHandler();
            _batchEventProcessor = new BatchEventProcessor<long[]>(_ringBuffer, sequenceBarrier, _handler);
            _ringBuffer.AddGatingSequences(_batchEventProcessor.Sequence);
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////

        public int RequiredProcessorCount => 2;

        public long Run(Stopwatch stopwatch)
        {
            var signal = new ManualResetEvent(false);
            var expectedCount = _batchEventProcessor.Sequence.Value + _iterations;
            _handler.Reset(signal, _iterations);
            var processorTask = _executor.Execute(_batchEventProcessor.Run);
            stopwatch.Start();

            var rb = _ringBuffer;
            for (var i = 0; i < _iterations; i++)
            {
                var next = rb.Next();
                var @event = rb[next];
                for (var j = 0; j < @event.Length; j++)
                {
                    @event[j] = i;
                }
                rb.Publish(next);
            }

            signal.WaitOne();
            stopwatch.Stop();
            WaitForEventProcessorSequence(expectedCount);
            _batchEventProcessor.Halt();
            processorTask.Wait(2000);

            PerfTestUtil.FailIf(0, _handler.Value, "Handler has not processed any event");

            return _iterations * _arraySize;
        }

        private void WaitForEventProcessorSequence(long expectedCount)
        {
            while (_batchEventProcessor.Sequence.Value != expectedCount)
            {
                Thread.Sleep(1);
            }
        }
    }
}