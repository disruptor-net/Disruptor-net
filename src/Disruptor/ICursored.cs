namespace Disruptor;

/// <summary>
/// Exposes a cursor value.
/// </summary>
public interface ICursored
{
    /// <summary>
    /// Get the current cursor value.
    /// </summary>
    long Cursor { get; }
}
