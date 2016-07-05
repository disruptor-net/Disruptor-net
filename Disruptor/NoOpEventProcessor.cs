using System;
using System.Threading;

namespace Disruptor
{
    /// <summary>
    /// No operation version of a <see cref="IEventProcessor"/> that simply tracks a <see cref="Sequencer"/>.
    /// This is useful in tests or for pre-filling a <see cref="RingBuffer{T}"/> from a producer.
    /// </summary>
    public sealed class NoOpEventProcessor : IEventProcessor
    {
        private readonly SequencerFollowingSequence _sequence;
        private volatile int _running;

        /// <summary>
        /// Construct a <see cref="IEventProcessor"/> that simply tracks a <see cref="Sequencer"/>.
        /// </summary>
        /// <param name="sequencer">sequencer to track.</param>
        public NoOpEventProcessor(Sequencer sequencer)
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
	        private readonly Sequencer _sequencer;

            public SequencerFollowingSequence(Sequencer sequencer)
                : base(InitialCursorValue)
	        {
                _sequencer = sequencer;
	        }

            public override long Value => _sequencer.Cursor;
	    }
    }
}