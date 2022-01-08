using System;

namespace Disruptor
{
    public static class EventPoller
    {
        /// <summary>
        /// Indicates the polling result result for  <see cref="EventPoller{T}"/> or <see cref="ValueEventPoller{T}"/>
        /// </summary>
        public enum PollState
        {
            /// <summary>
            /// The poller processed one or more events
            /// </summary>
            Processing,
            /// <summary>
            /// The poller is waiting for gated sequences to advance before events become available
            /// </summary>
            Gating,
            /// <summary>
            /// No events need to be processed
            /// </summary>
            Idle,
        }

        /// <summary>
        /// A callback used to process events
        /// </summary>
        public delegate bool Handler<T>(T data, long sequence, bool endOfBatch);

        /// <summary>
        /// A callback used to process events
        /// </summary>
        public delegate void BatchHandler<T>(EventBatch<T> batch, long sequence)
            where T : class;

        /// <summary>
        /// A callback used to process value events
        /// </summary>
        public delegate bool ValueHandler<T>(ref T data, long sequence, bool endOfBatch)
            where T : struct;

        public static EventPoller<T> Create<T>(IDataProvider<T> dataProvider, ISequencer sequencer, Sequence sequence, Sequence cursorSequence, params ISequence[] gatingSequences)
            where T : class
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
