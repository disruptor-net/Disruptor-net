namespace Disruptor;

/// <summary>
/// Exposes a cursor value.
/// </summary>
public interface ICursored
{
    /// <summary>
    /// Gets the current cursor value for the ring buffer. The cursor represents the head of the ring buffer,
    /// so the next published event will be available at <c>ringBuffer.Cursor + 1</c>.
    /// </summary>
    /// <remarks>
    /// Please note that the cursor might point to an unpublished event, so reading <c>ringBuffer[ringBuffer.Cursor]</c>
    /// is not correct.
    /// </remarks>
    long Cursor { get; }
}
