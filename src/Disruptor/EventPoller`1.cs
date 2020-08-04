namespace Disruptor
{
    /// <summary>
    /// Experimental poll-based interface for the Disruptor.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class EventPoller<T>
    {
        private readonly IDataProvider<T> _dataProvider;
        private readonly ISequencer _sequencer;
        private readonly ISequence _sequence;
        private readonly ISequence _gatingSequence;

        public EventPoller(IDataProvider<T> dataProvider, ISequencer sequencer, ISequence sequence, ISequence gatingSequence)
        {
            _dataProvider = dataProvider;
            _sequencer = sequencer;
            _sequence = sequence;
            _gatingSequence = gatingSequence;
        }

        public EventPoller.PollState Poll(EventPoller.Handler<T> eventHandler)
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
                        var evt = _dataProvider[nextSequence];
                        processNextEvent = eventHandler(evt, nextSequence, nextSequence == availableSequence);
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
