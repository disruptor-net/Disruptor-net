using System;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Util;

namespace Disruptor.Processing
{
    /// <summary>
    /// No operation version of a <see cref="IEventProcessor"/> that simply tracks a <see cref="Disruptor.Sequence"/>.
    /// This is useful in tests or for pre-filling a <see cref="RingBuffer{T}"/> from a producer.
    /// </summary>
    public sealed class NoOpEventProcessor<T> : IEventProcessor
    {
        private readonly SequencerFollowingSequence _sequence;
        private volatile int _running;

        /// <summary>
        /// Construct a <see cref="IEventProcessor"/> that simply tracks a <see cref="Disruptor.Sequence"/>.
        /// </summary>
        /// <param name="sequencer">sequencer to track.</param>
        public NoOpEventProcessor(ICursored sequencer)
        {
            _sequence = new SequencerFollowingSequence(sequencer);
        }

        /// <summary>
        /// <inheritdoc cref="IEventProcessor.Start"/>.
        /// </summary>
        public Task Start(TaskScheduler taskScheduler, TaskCreationOptions taskCreationOptions)
        {
            return Task.Factory.StartNew(Run, CancellationToken.None, taskCreationOptions, taskScheduler);
        }

        /// <summary>
        /// NoOp
        /// </summary>
        public void Run()
        {
            if (Interlocked.Exchange(ref _running, 1) != 0)
            {
                throw new InvalidOperationException("Thread is already running");
            }
        }

        /// <summary>
        /// <see cref="IEventProcessor.Sequence"/>
        /// </summary>
        public ISequence Sequence => _sequence;

        /// <summary>
        /// NoOp
        /// </summary>
        public void Halt()
        {
            _running = 0;
        }

        /// <summary>
        /// <see cref="IEventProcessor.IsRunning"/>
        /// </summary>
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
