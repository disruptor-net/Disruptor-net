using System.Collections.Generic;
using System.Threading;

namespace Disruptor.Tests.Support
{
    internal class TestWaiter
    {
        private readonly Barrier _barrier;
        private readonly ISequenceBarrier _sequenceBarrier;
        private readonly long _initialSequence;
        private readonly long _toWaitForSequence;
        private readonly RingBuffer<StubEvent> _ringBuffer;

        public TestWaiter(Barrier barrier, ISequenceBarrier sequenceBarrier, RingBuffer<StubEvent> ringBuffer, long initialSequence, long toWaitForSequence)
        {
            _barrier = barrier;
            _sequenceBarrier = sequenceBarrier;
            _ringBuffer = ringBuffer;
            _initialSequence = initialSequence;
            _toWaitForSequence = toWaitForSequence;
        }

        public List<StubEvent> Call()
        {
            _barrier.SignalAndWait();
            _sequenceBarrier.WaitFor(_toWaitForSequence);

            var events = new List<StubEvent>();
            for (var l = _initialSequence; l <= _toWaitForSequence; l++)
            {
                events.Add(_ringBuffer[l]);
            }

            return events;
        }
    }
}