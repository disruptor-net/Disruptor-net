namespace Disruptor;

public interface ISequence
{
    /// <summary>
    /// Current sequence number
    /// </summary>
    long Value { get; }

    /// <summary>
    /// Perform an ordered write of this sequence.  The intent is
    /// a Store/Store barrier between this write and any previous
    /// store.
    /// </summary>
    /// <param name="value">The new value for the sequence.</param>
    void SetValue(long value);
}
