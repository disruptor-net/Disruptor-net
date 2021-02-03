using System.Runtime.CompilerServices;

namespace Disruptor
{
    /// <summary>
    /// <see cref="ISequenceBarrier"/> handed out for gating <see cref="IEventProcessor"/> on a cursor sequence and optional dependent <see cref="IEventProcessor"/>s,
    ///  using the given WaitStrategy.
    /// </summary>
    /// <typeparam name="TSequencer">the type of the <see cref="ISequencer"/> used.</typeparam>
    /// <typeparam name="TWaitStrategy">the type of the <see cref="IWaitStrategy"/> used.</typeparam>
    internal sealed class ProcessingSequenceBarrier<TSequencer, TWaitStrategy> : ISequenceBarrier
        where TWaitStrategy : IWaitStrategy
        where TSequencer : ISequencer
    {
        // ReSharper disable FieldCanBeMadeReadOnly.Local (performance: the runtime type will be a struct)
        private TWaitStrategy _waitStrategy;
        private TSequencer _sequencer;
        // ReSharper restore FieldCanBeMadeReadOnly.Local

        private readonly ISequence _dependentSequence;
        private readonly Sequence _cursorSequence;
        private volatile bool _alerted;

        public ProcessingSequenceBarrier(TSequencer sequencer, TWaitStrategy waitStrategy, Sequence cursorSequence, ISequence[] dependentSequences)
        {
            _sequencer = sequencer;
            _waitStrategy = waitStrategy;
            _cursorSequence = cursorSequence;
            _dependentSequence = SequenceGroups.CreateReadOnlySequence(cursorSequence, dependentSequences);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long WaitFor(long sequence)
        {
            CheckAlert();

            var availableSequence = _waitStrategy.WaitFor(sequence, _cursorSequence, _dependentSequence, this);

            if (availableSequence < sequence)
                return availableSequence;

            return _sequencer.GetHighestPublishedSequence(sequence, availableSequence);
        }

        public long Cursor => _dependentSequence.Value;

        public bool IsAlerted => _alerted;

        public void Alert()
        {
            _alerted = true;
            _waitStrategy.SignalAllWhenBlocking();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearAlert()
        {
            _alerted = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CheckAlert()
        {
            if(_alerted)
            {
                AlertException.Throw();
            }
        }
    }
}
