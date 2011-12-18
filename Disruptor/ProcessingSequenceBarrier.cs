using System;

namespace Disruptor
{
    /// <summary>
    /// <see cref="ISequenceBarrier"/> handed out for gating <see cref="IEventProcessor"/> on a cursor sequence and optional dependent <see cref="IEventProcessor"/>s
    /// </summary>
    internal sealed class ProcessingSequenceBarrier : ISequenceBarrier
    {
        private readonly IWaitStrategy _waitStrategy;
        private readonly Sequence _cursorSequence;
        private readonly Sequence[] _dependentSequences;
        private volatile bool _alerted;

        public ProcessingSequenceBarrier(IWaitStrategy waitStrategy, 
                               Sequence cursorSequence, 
                               Sequence[] dependentSequences)
        {
            _waitStrategy = waitStrategy;
            _cursorSequence = cursorSequence;
            _dependentSequences = dependentSequences;
        }

        public long WaitFor(long sequence)
        {
            CheckAlert();

            return _waitStrategy.WaitFor(sequence, _cursorSequence, _dependentSequences, this);
        }

        public long WaitFor(long sequence, TimeSpan timeout)
        {
            CheckAlert();

            return _waitStrategy.WaitFor(sequence, _cursorSequence, _dependentSequences, this, timeout);
        }

        public long Cursor
        {
            get { return _cursorSequence.Value; }
        }

        public bool IsAlerted
        {
            get { return _alerted; }
        }

        public void Alert()
        {
            _alerted = true;
            _waitStrategy.SignalAllWhenBlocking();
        }

        public void ClearAlert()
        {
            _alerted = false;
        }

        public void CheckAlert()
        {
            if(_alerted)
            {
                throw AlertException.Instance;
            }
        }
    }
}