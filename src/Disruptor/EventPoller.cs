namespace Disruptor
{
    public static class EventPoller
    {
        public enum PollState
        {
            Processing,
            Gating,
            Idle
        }

        public delegate bool Handler<T>(T data, long sequence, bool endOfBatch);

        public delegate bool ValueHandler<T>(ref T data, long sequence, bool endOfBatch)
            where T : struct;

        public static EventPoller<T> Create<T>(IDataProvider<T> dataProvider, ISequencer sequencer, Sequence sequence, Sequence cursorSequence, params ISequence[] gatingSequences)
        {
            var gatingSequence = SequenceGroups.CreateReadOnlySequence(cursorSequence, gatingSequences);

            return new EventPoller<T>(dataProvider, sequencer, sequence, gatingSequence);
        }

        public static ValueEventPoller<T> Create<T>(IValueDataProvider<T> dataProvider, ISequencer sequencer, Sequence sequence, Sequence cursorSequence, params ISequence[] gatingSequences)
            where T : struct
        {
            var gatingSequence = SequenceGroups.CreateReadOnlySequence(cursorSequence, gatingSequences);

            return new ValueEventPoller<T>(dataProvider, sequencer, sequence, gatingSequence);
        }
    }
}
