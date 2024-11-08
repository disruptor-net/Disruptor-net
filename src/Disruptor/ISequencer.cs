namespace Disruptor;

/// <summary>
/// Coordinator for claiming sequences for access to a data structure while tracking dependent <see cref="Sequence"/>s
/// </summary>
public interface ISequencer : ISequenced, ICursored, IPublishedSequenceReader
{
    /// <summary>
    /// Claim a specific sequence when only one publisher is involved.
    /// </summary>
    /// <param name="sequence">sequence to be claimed.</param>
    void Claim(long sequence);

    /// <summary>
    /// Confirms if a sequence is published and the event is available for use; non-blocking.
    /// </summary>
    /// <param name="sequence">sequence of the buffer to check</param>
    /// <returns>true if the sequence is available for use, false if not</returns>
    bool IsAvailable(long sequence);

    /// <summary>
    /// Add the specified gating sequences to this instance of the Disruptor.  They will
    /// safely and atomically added to the list of gating sequences.
    /// </summary>
    /// <param name="gatingSequences">The sequences to add.</param>
    void AddGatingSequences(params Sequence[] gatingSequences);

    /// <summary>
    /// Remove the specified sequence from this sequencer.
    /// </summary>
    /// <param name="sequence">to be removed.</param>
    /// <returns>true if this sequence was found, false otherwise.</returns>
    bool RemoveGatingSequence(Sequence sequence);

    /// <summary>
    /// Create a <see cref="SequenceBarrier"/> that gates on the cursor and a list of <see cref="Sequence"/>s
    /// </summary>
    /// <param name="eventHandler">The event handler of the target event processor. Can be null for custom event processors or if the event processor is a <see cref="IWorkHandler{T}"/> processor.</param>
    /// <param name="sequencesToTrack">All the sequences that the newly constructed barrier will wait on.</param>
    /// <returns>A sequence barrier that will track the specified sequences.</returns>
    SequenceBarrier NewBarrier(IEventHandler? eventHandler, params Sequence[] sequencesToTrack);

    /// <summary>
    /// Create a <see cref="AsyncSequenceBarrier"/> that gates on the cursor and a list of <see cref="Sequence"/>s
    /// </summary>
    /// <param name="eventHandler">The event handler of the target event processor. Can be null for custom event processors or if the event processor is a <see cref="IWorkHandler{T}"/> processor.</param>
    /// <param name="sequencesToTrack">All the sequences that the newly constructed barrier will wait on.</param>
    /// <returns>A sequence barrier that will track the specified sequences.</returns>
    AsyncSequenceBarrier NewAsyncBarrier(IEventHandler? eventHandler, params Sequence[] sequencesToTrack);

    /// <summary>
    /// Get the minimum sequence value from all the gating sequences
    /// added to this ringBuffer.
    /// </summary>
    /// <returns>The minimum gating sequence or the cursor sequence if no sequences have been added.</returns>
    long GetMinimumSequence();

    /// <summary>
    /// Creates an event poller for this sequence that will use the supplied data provider and
    /// gating sequences.
    /// </summary>
    EventPoller<T> NewPoller<T>(IDataProvider<T> provider, params Sequence[] gatingSequences)
        where T : class;

    /// <summary>
    /// Creates an event poller for this sequence that will use the supplied data provider and
    /// gating sequences.
    /// </summary>
    ValueEventPoller<T> NewPoller<T>(IValueDataProvider<T> provider, params Sequence[] gatingSequences)
        where T : struct;

    /// <summary>
    /// Creates an event stream for this sequence that will use the supplied data provider and
    /// gating sequences.
    /// </summary>
    AsyncEventStream<T> NewAsyncEventStream<T>(IDataProvider<T> provider, Sequence[] gatingSequences)
        where T : class;
}
