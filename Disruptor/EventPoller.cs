namespace Disruptor
{
    public class EventPoller<T>
    {
        private IDataProvider<T> _dataProvider;
        private Sequencer _sequencer;
        private Sequence _sequence;
        private Sequence _gatingSequence;

        public EventPoller(IDataProvider<T> dataProvider,
                           Sequencer sequencer,
                           Sequence sequence,
                           Sequence gatingSequence)
        {
            _dataProvider = dataProvider;
            _sequencer = sequencer;
            _sequence = sequence;
            _gatingSequence = gatingSequence;
        }

        public static EventPoller<T> NewInstance<T>(IDataProvider<T> dataProvider,
                                                    Sequencer sequencer,
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
    }
}