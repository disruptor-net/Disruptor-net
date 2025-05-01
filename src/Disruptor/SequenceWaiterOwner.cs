namespace Disruptor;

/// <summary>
/// Represents the owner of a <see cref="ISequenceWaiter"/>.
/// </summary>
/// <remarks>
/// The owner can be either an <see cref="IEventHandler"/>, an <see cref="IWorkHandler{T}"/>,
/// or unknown (typically, for custom event processors or for polling consumers).
/// </remarks>
public class SequenceWaiterOwner
{
    private SequenceWaiterOwner(object? handler, SequenceWaiterOwnerType type)
    {
        Handler = handler;
        Type = type;
    }

    /// <summary>
    /// Gets the owner object of the sequence waiter. Can be either an <see cref="IEventHandler"/>,
    /// an <see cref="IWorkHandler{T}"/>, or null if the handler is unknown.
    /// </summary>
    public object? Handler { get; }

    /// <summary>
    /// Gets the type of the owner object.
    /// </summary>
    public SequenceWaiterOwnerType Type { get; }

    /// <summary>
    /// Gets the <see cref="SequenceWaiterOwner"/> representing an unknown owner.
    /// </summary>
    public static SequenceWaiterOwner Unknown { get; } = new(null, SequenceWaiterOwnerType.Unknown);

    /// <summary>
    /// Creates a <see cref="SequenceWaiterOwner"/> for an <see cref="IEventHandler"/>.
    /// </summary>
    public static SequenceWaiterOwner EventHandler(IEventHandler eventHandler)
        => new(eventHandler, SequenceWaiterOwnerType.EventHandler);

    /// <summary>
    /// Creates a <see cref="SequenceWaiterOwner"/> for an <see cref="IWorkHandler{T}"/>.
    /// </summary>
    public static SequenceWaiterOwner WorkHandler<T>(IWorkHandler<T> workHandler)
        => new(workHandler, SequenceWaiterOwnerType.WorkHandler);
}
