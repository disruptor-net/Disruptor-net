using System;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Processing;

namespace Disruptor.Tests.Support
{
    public sealed class NoOpEventProcessor<T> : IEventProcessor
    {
        private readonly SequencerFollowingSequence _sequence;
        private volatile int _running;

        public NoOpEventProcessor(ICursored sequencer)
        {
            _sequence = new SequencerFollowingSequence(sequencer);
        }

        public Task Start(TaskScheduler taskScheduler, TaskCreationOptions taskCreationOptions)
        {
            return Task.Factory.StartNew(Run, CancellationToken.None, taskCreationOptions, taskScheduler);
        }

        public void Run()
        {
            if (Interlocked.Exchange(ref _running, 1) != 0)
            {
                throw new InvalidOperationException("Thread is already running");
            }
        }

        public ISequence Sequence => _sequence;

        public void Halt()
        {
            _running = 0;
        }

        public bool IsRunning => _running == 1;

        private sealed class SequencerFollowingSequence : ISequence
        {
            private readonly ICursored _sequencer;

            public SequencerFollowingSequence(ICursored sequencer)
            {
                _sequencer = sequencer;
            }

            public long Value => _sequencer.Cursor;

            public void SetValue(long value)
            {
            }

            public void SetValueVolatile(long value)
            {
            }

            public bool CompareAndSet(long expectedSequence, long nextSequence)
            {
                return false;
            }

            public long IncrementAndGet()
            {
                return 0;
            }

            public long AddAndGet(long value)
            {
                return 0;
            }
        }
    }
}
