using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.PerfTests.Support;

namespace Disruptor.PerfTests.Sequenced
{
    /// <summary>
    /// MultiCast a series of items between 1 publisher and 3 event processors.
    ///           +-----+
    ///    +----->| EP1 |
    ///    |      +-----+
    ///    |
    /// +----+    +-----+
    /// | P1 |--->| EP2 |
    /// +----+    +-----+
    ///    |
    ///    |      +-----+
    ///    +----->| EP3 |
    ///           +-----+
    /// Disruptor:
    /// ==========
    ///                             track to prevent wrap
    ///             +--------------------+----------+----------+
    ///             |                    |          |          |
    ///             |                    v          v          v
    /// +----+    +====+    +====+    +-----+    +-----+    +-----+
    /// | P1 |---\| RB |/---| SB |    | EP1 |    | EP2 |    | EP3 |
    /// +----+    +====+    +====+    +-----+    +-----+    +-----+
    ///      claim      get    ^         |          |          |
    ///                        |         |          |          |
    ///                        +---------+----------+----------+
    ///                                      waitFor
    /// P1  - Publisher 1
    /// RB  - RingBuffer
    /// SB  - SequenceBarrier
    /// EP1 - EventProcessor 1
    /// EP2 - EventProcessor 2
    /// EP3 - EventProcessor 3
    /// </summary>
    public class OneToThreeSequencedThroughputTest : IThroughputTest
    {
        private const int _numEventProcessors = 3;
        private const int _bufferSize = 1024 * 8;
        private const long _iterations = 1000L * 1000L * 100L;

        private readonly RingBuffer<ValueEvent> _ringBuffer;
        private readonly IBatchEventProcessor<ValueEvent>[] _batchEventProcessors = new IBatchEventProcessor<ValueEvent>[_numEventProcessors];
        private readonly long[] _results = new long[_numEventProcessors];
        private readonly ValueMutationEventHandler[] _handlers = new ValueMutationEventHandler[_numEventProcessors];
        private readonly IExecutor _executor = new BasicExecutor(TaskScheduler.Current);
        
        public OneToThreeSequencedThroughputTest()
        {
            for (long i = 0; i < _iterations; i++)
            {
                _results[0] = Operation.Addition.Op(_results[0], i);
                _results[1] = Operation.Subtraction.Op(_results[1], i);
                _results[2] = Operation.And.Op(_results[2], i);
            }

            _ringBuffer = RingBuffer<ValueEvent>.CreateSingleProducer(ValueEvent.EventFactory, _bufferSize, new YieldingWaitStrategy());
            var sequenceBarrier = _ringBuffer.NewBarrier();

            _handlers[0] = new ValueMutationEventHandler(Operation.Addition);
            _handlers[1] = new ValueMutationEventHandler(Operation.Subtraction);
            _handlers[2] = new ValueMutationEventHandler(Operation.And);


            for (var i = 0; i < _numEventProcessors; i++)
            {
                _batchEventProcessors[i] = BatchEventProcessorFactory.Create(_ringBuffer, sequenceBarrier, _handlers[i]);
            }
            _ringBuffer.AddGatingSequences(_batchEventProcessors.Select(x => x.Sequence).ToArray());
        }

        public int RequiredProcessorCount => 4;

        public long Run(ThroughputSessionContext sessionContext)
        {
            var latch = new Barrier(_numEventProcessors + 1);

            var processorTasks = new List<Task>();
            for (var i = 0; i < _numEventProcessors; i++)
            {
                _handlers[i].Reset(latch, _batchEventProcessors[i].Sequence.Value + _iterations);
                processorTasks.Add(_executor.Execute(_batchEventProcessors[i].Run));
                _batchEventProcessors[i].WaitUntilStarted(TimeSpan.FromSeconds(5));
            }

            sessionContext.Start();

            for (long i = 0; i < _iterations; i++)
            {
                var sequence = _ringBuffer.Next();
                _ringBuffer[sequence].Value = i;
                _ringBuffer.Publish(sequence);
            }

            latch.SignalAndWait();
            sessionContext.Stop();

            for (var i = 0; i < _numEventProcessors; i++)
            {
                _batchEventProcessors[i].Halt();
                PerfTestUtil.FailIfNot(_results[i], _handlers[i].Value, $"Result {_results[i]} != {_handlers[i].Value}");
            }
            Task.WaitAll(processorTasks.ToArray());

            sessionContext.SetBatchData(_handlers.Sum(x => x.BatchesProcessedCount), _numEventProcessors * _iterations);

            return _numEventProcessors * _iterations;
        }
    }
}
