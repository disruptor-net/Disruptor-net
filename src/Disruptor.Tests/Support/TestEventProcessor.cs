using System;
using System.Threading;
using Disruptor.Processing;

namespace Disruptor.Tests.Support
{
    public class TestEventProcessor : IEventProcessor
    {
        private readonly ISequenceBarrier _sequenceBarrier;
        private volatile int _running;

        public TestEventProcessor(ISequenceBarrier sequenceBarrier)
        {
            _sequenceBarrier = sequenceBarrier;
        }

        public ISequence Sequence { get; } = new Sequence();
        public bool IsRunning => _running != 0;

        public void Halt()
        {
            _running = 0;
        }

        public void Run()
        {
            if (Interlocked.Exchange(ref _running, 1) != 0)
                throw new InvalidOperationException("Already running");

            _sequenceBarrier.WaitFor(0L);
            Sequence.SetValue(Sequence.Value + 1);
        }
    }
}

