namespace Disruptor
{
    /// <summary>
    /// Implementors of this interface must provide a single long value
    /// that represents their current cursor value.Used during dynamic
    /// add/remove of Sequences from a
    /// <see cref="SequenceGroups.AddSequences"/>.
    /// </summary>
    public interface ICursored
    {
        /// <summary>
        /// Get the current cursor value.
        /// </summary>
        long Cursor { get; }
    }
}