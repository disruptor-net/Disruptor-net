using Disruptor.Processing;

namespace Disruptor;

/// <summary>
/// Experimental poll-based interface for the Disruptor. Unlike a <see cref="IValueEventProcessor{T}"/>,
/// an event poller allows the user to control the flow of execution. This makes it ideal
/// for interoperability with existing threads whose lifecycle is not controlled by the
/// disruptor DSL.
/// </summary>
/// <remarks>
/// Consider using <see cref="ValueRingBuffer{T}.NewPoller"/> to get an instance of this type.
/// </remarks>
public class ValueEventPoller<T>
    where T : struct
{
    private readonly IValueDataProvider<T> _dataProvider;
    private readonly ISequencer _sequencer;
    private readonly ISequence _sequence;
    private readonly DependentSequenceGroup _dependentSequences;

    public ValueEventPoller(IValueDataProvider<T> dataProvider, ISequencer sequencer, ISequence sequence, DependentSequenceGroup dependentSequences)
    {
        _dataProvider = dataProvider;
        _sequencer = sequencer;
        _sequence = sequence;
        _dependentSequences = dependentSequences;
    }

    /// <summary>
    /// <para>
    /// Polls for events using the given handler.
    /// </para>
    /// <para>
    /// This poller will continue to feed events to the given handler until known available
    /// events are consumed or <see cref="EventPoller.ValueHandler{T}"/> returns false.
    /// </para>
    /// <para>
    /// Note that it is possible for more events to become available while the current events
    /// are being processed. A further call to this method will process such events.
    /// </para>
    /// </summary>
    public EventPoller.PollState Poll(EventPoller.ValueHandler<T> eventHandler)
    {
        var currentSequence = _sequence.Value;
        var nextSequence = currentSequence + 1;
        var availableSequence = _sequencer.GetHighestPublishedSequence(nextSequence, _dependentSequences.Value);

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

    /// <summary>
    /// Gets the sequence being used by this event poller
    /// </summary>
    public ISequence Sequence => _sequence;
}
