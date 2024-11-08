namespace Disruptor;

public interface IPublishedSequenceReader
{
    /// <summary>
    /// Get the highest sequence number that can be safely read from the ring buffer. Depending
    /// on the implementation of the sequencer this call may need to scan a number of values
    /// in the Sequencer. The scan will range from nextSequence to availableSequence.  If
    /// there are no available values <code>>= nextSequence</code> the return value will be
    /// <code>nextSequence - 1</code>.  To work correctly a consumer should pass a value that
    /// it 1 higher than the last sequence that was successfully processed.
    /// </summary>
    /// <param name="nextSequence">The sequence to start scanning from.</param>
    /// <param name="availableSequence">The sequence to scan to.</param>
    /// <returns>The highest value that can be safely read, will be at least <code>nextSequence - 1</code>.</returns>
    long GetHighestPublishedSequence(long nextSequence, long availableSequence);
}
