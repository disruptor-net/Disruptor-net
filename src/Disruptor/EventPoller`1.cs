using Disruptor.Processing;

namespace Disruptor;

/// <summary>
/// Experimental poll-based API for the Disruptor. Unlike a <see cref="IEventProcessor{T}"/>,
/// an event poller allows the user to control the flow of execution. This makes it ideal
/// for interoperability with existing threads whose lifecycle is not controlled by the
/// disruptor DSL.
/// </summary>
/// <remarks>
/// Consider using <see cref="RingBuffer{T}.NewPoller"/> to get an instance of this type.
/// </remarks>
public class EventPoller<T>
    where T : class
{
    private readonly IDataProvider<T> _dataProvider;
    private readonly ISequencer _sequencer;
    private readonly ISequence _sequence;
    private readonly DependentSequenceGroup _dependentSequences;

    public EventPoller(IDataProvider<T> dataProvider, ISequencer sequencer, ISequence sequence, DependentSequenceGroup dependentSequences)
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
    /// events are consumed or <see cref="EventPoller.Handler{T}"/> returns false.
    /// </para>
    /// <para>
    /// Note that it is possible for more events to become available while the current events
    /// are being processed. A further call to this method will process such events.
    /// </para>
    /// </summary>
    public EventPoller.PollState Poll(EventPoller.Handler<T> eventHandler)
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

    /// <summary>
    /// <para>
    /// Polls for events using the given handler.
    /// </para>
    /// <para>
    /// This poller will feed a single event batch to the given handler, even if more events
    /// are already available due to ring buffer wrap around.
    /// </para>
    /// <para>
    /// Note that it is possible for more events to become available while the current events
    /// are being processed. A further call to this method will process such events.
    /// </para>
    /// </summary>
    public EventPoller.PollState Poll(EventPoller.BatchHandler<T> eventHandler)
    {
        var currentSequence = _sequence.Value;
        var nextSequence = currentSequence + 1;
        var availableSequence = _sequencer.GetHighestPublishedSequence(nextSequence, _dependentSequences.Value);

        if (nextSequence <= availableSequence)
        {
            var processedSequence = currentSequence;

            try
            {
                var batch = _dataProvider.GetBatch(nextSequence, availableSequence);
                eventHandler(batch, nextSequence);
                processedSequence = nextSequence + batch.Length - 1;
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
