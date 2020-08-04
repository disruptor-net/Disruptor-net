namespace Disruptor
{
    /// <summary>
    /// Experimental poll-based interface for the Disruptor.
    /// </summary>
    public class ValueEventPoller<T>
        where T : struct
    {
        private readonly IValueDataProvider<T> _dataProvider;
        private readonly ISequencer _sequencer;
        private readonly ISequence _sequence;
        private readonly ISequence _gatingSequence;

        public ValueEventPoller(IValueDataProvider<T> dataProvider, ISequencer sequencer, ISequence sequence, ISequence gatingSequence)
        {
            _dataProvider = dataProvider;
            _sequencer = sequencer;
            _sequence = sequence;
            _gatingSequence = gatingSequence;
        }

        public EventPoller.PollState Poll(EventPoller.ValueHandler<T> eventHandler)
        {
            var currentSequence = _sequence.Value;
            var nextSequence = currentSequence + 1;
            var availableSequence = _sequencer.GetHighestPublishedSequence(nextSequence, _gatingSequence.Value);

            if (nextSequence <= availableSequence)
            {
                var processedSequence = currentSequence;

                try
                {
                    bool processNextEvent;
                    do
                    {
                        ref var evt = ref _dataProvider[nextSequence];
                        processNextEvent = eventHandler(ref evt, nextSequence, nextSequence == availableSequence);
                        processedSequence = nextSequence;
                        nextSequence++;
                    }
                    while (nextSequence <= availableSequence & processNextEvent);
                }
                finally
                {
                    _sequence.SetValue(processedSequence);
                }

                return EventPoller.PollState.Processing;
            }

            if (_sequencer.Cursor >= nextSequence)
            {
                return EventPoller.PollState.Gating;
            }

            return EventPoller.PollState.Idle;
        }

        public ISequence Sequence => _sequence;
    }
}
