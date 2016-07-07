using System;
using System.Threading;

namespace Disruptor
{
    /// <summary>
    /// No operation version of a <see cref="IEventProcessor"/> that simply tracks a <see cref="Disruptor.Sequence"/>.
    /// This is useful in tests or for pre-filling a <see cref="RingBuffer{T}"/> from a producer.
    /// </summary>
    public sealed class NoOpEventProcessor<T> : IEventProcessor where T : class 
    {
        private readonly SequencerFollowingSequence _sequence;
        private volatile int _running;

        /// <summary>
        /// Construct a <see cref="IEventProcessor"/> that simply tracks a <see cref="Disruptor.Sequence"/>.
        /// </summary>
        /// <param name="sequencer">sequencer to track.</param>
        public NoOpEventProcessor(RingBuffer<T> sequencer)
        {
            _sequence = new SequencerFollowingSequence(sequencer);
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
        /// 
        /// </summary>
        public Sequence Sequence => _sequence;

        /// <summary>
        /// NoOp
        /// </summary>
        public void Halt()
        {
            _running = 0;
        }

        public bool IsRunning => _running == 1;

        private sealed class SequencerFollowingSequence : Sequence
        {
            private readonly RingBuffer<T> _sequencer;

            public SequencerFollowingSequence(RingBuffer<T> sequencer)
                : base(InitialCursorValue)
            {
                _sequencer = sequencer;
            }

            public override long Value => _sequencer.Cursor;
        }
    }
}