using System;

namespace Disruptor
{
    public enum PollState
    {
        Processing,
        Gating,
        Idle
    }

    /// <summary>
    /// Experimental poll-based interface for the Disruptor.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class EventPoller<T>
    {
        private readonly IDataProvider<T> _dataProvider;
        private readonly ISequencer _sequencer;
        private readonly Sequence _sequence;
        private readonly Sequence _gatingSequence;

        public EventPoller(IDataProvider<T> dataProvider,
                           ISequencer sequencer,
                           Sequence sequence,
                           Sequence gatingSequence)
        {
            _dataProvider = dataProvider;
            _sequencer = sequencer;
            _sequence = sequence;
            _gatingSequence = gatingSequence;
        }

        public PollState Poll(Func<T, long, bool, bool> eventHandler)
        {
            long currentSequence = _sequence.Value;
            long nextSequence = currentSequence + 1;
            long availableSequence = _sequencer.GetHighestPublishedSequence(nextSequence, _gatingSequence.Value);

            if (nextSequence <= availableSequence)
            {
                bool processNextEvent;
                long processedSequence = currentSequence;

                try
                {
                    do
                    {
                        T @event = _dataProvider[nextSequence];
                        processNextEvent = eventHandler(@event, nextSequence, nextSequence == availableSequence);
                        processedSequence = nextSequence;
                        nextSequence++;

                    } while (nextSequence <= availableSequence & processNextEvent);
                }
                finally
                {
                    _sequence.SetValue(processedSequence);
                }

                return PollState.Processing;
            }
            else if (_sequencer.Cursor >= nextSequence)
            {
                return PollState.Gating;
            }
            else
            {
                return PollState.Idle;
            }
        }

        public static EventPoller<T> NewInstance(IDataProvider<T> dataProvider,
                                                    ISequencer sequencer,
                                                    Sequence sequence,
                                                    Sequence cursorSequence,
                                                    params Sequence[] gatingSequences)
        {
            Sequence gatingSequence;
            if (gatingSequences.Length == 0)
            {
                gatingSequence = cursorSequence;
            }
            else if (gatingSequences.Length == 1)
            {
                gatingSequence = gatingSequences[0];
            }
            else
            {
                gatingSequence = new FixedSequenceGroup(gatingSequences);
            }

            return new EventPoller<T>(dataProvider, sequencer, sequence, gatingSequence);
        }

        public Sequence Sequence => _sequence;
    }
}