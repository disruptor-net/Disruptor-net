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
        private readonly RunningFlag _running = new RunningFlag();

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
            _running.MarkAsRunning();
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
            _running.MarkAsStopped();
        }

        public bool IsRunning => _running.IsRunning;

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