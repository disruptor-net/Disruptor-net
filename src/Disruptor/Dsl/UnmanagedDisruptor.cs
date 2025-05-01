using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Disruptor.Processing;

namespace Disruptor.Dsl;

/// <summary>
/// A DSL-style API for setting up the disruptor pattern around a ring buffer
/// (aka the Builder pattern).
/// </summary>
/// <typeparam name="T">the type of event used.</typeparam>
public class UnmanagedDisruptor<T> : ValueTypeDisruptor<T>
    where T : unmanaged
{
    /// <summary>
    /// Create a new UnmanagedDisruptor using <see cref="SequencerFactory.DefaultWaitStrategy"/> and <see cref="SequencerFactory.DefaultProducerType"/>.
    /// </summary>
    /// <param name="pointer">pointer to the first event of the buffer</param>
    /// <param name="eventSize">size of each event</param>
    /// <param name="ringBufferSize">the number of events of the ring buffer, must be power of 2</param>
    public UnmanagedDisruptor(IntPtr pointer, int eventSize, int ringBufferSize)
        : this(pointer, eventSize, ringBufferSize, TaskScheduler.Default)
    {
    }

    /// <summary>
    /// Create a new UnmanagedDisruptor using <see cref="SequencerFactory.DefaultWaitStrategy"/> and <see cref="SequencerFactory.DefaultProducerType"/>.
    /// </summary>
    /// <param name="pointer">pointer to the first event of the buffer</param>
    /// <param name="eventSize">size of each event</param>
    /// <param name="ringBufferSize">the number of events of the ring buffer, must be power of 2</param>
    /// <param name="taskScheduler">a <see cref="TaskScheduler"/> to create threads for processors</param>
    public UnmanagedDisruptor(IntPtr pointer, int eventSize, int ringBufferSize, TaskScheduler taskScheduler)
        : this(new UnmanagedRingBuffer<T>(pointer, eventSize, SequencerFactory.Create(SequencerFactory.DefaultProducerType, ringBufferSize)), taskScheduler)
    {
    }

    /// <summary>
    /// Create a new UnmanagedDisruptor.
    /// </summary>
    /// <param name="pointer">pointer to the first event of the buffer</param>
    /// <param name="eventSize">size of each event</param>
    /// <param name="ringBufferSize">the number of events of the ring buffer, must be power of 2</param>
    /// <param name="producerType">the claim strategy to use for the ring buffer</param>
    /// <param name="waitStrategy">the wait strategy to use for the ring buffer</param>
    public UnmanagedDisruptor(IntPtr pointer, int eventSize, int ringBufferSize, ProducerType producerType, IWaitStrategy waitStrategy)
        : this(new UnmanagedRingBuffer<T>(pointer, eventSize, SequencerFactory.Create(producerType, ringBufferSize, waitStrategy)), TaskScheduler.Default)
    {
    }

    /// <summary>
    /// Create a new UnmanagedDisruptor.
    ///
    /// The <see cref="UnmanagedRingBufferMemory"/> is not owned by the disruptor and should be disposed after shutdown.
    /// </summary>
    /// <param name="memory">block of memory that will store the events</param>
    /// <param name="producerType">the claim strategy to use for the ring buffer</param>
    /// <param name="waitStrategy">the wait strategy to use for the ring buffer</param>
    public UnmanagedDisruptor(UnmanagedRingBufferMemory memory, ProducerType producerType, IWaitStrategy waitStrategy)
        : this(new UnmanagedRingBuffer<T>(memory, producerType, waitStrategy), TaskScheduler.Default)
    {
    }

    /// <summary>
    /// Create a new UnmanagedDisruptor.
    /// </summary>
    /// <param name="pointer">pointer to the first event of the buffer</param>
    /// <param name="eventSize">size of each event</param>
    /// <param name="ringBufferSize">the number of events of the ring buffer, must be power of 2</param>
    /// <param name="taskScheduler">a <see cref="TaskScheduler"/> to create threads for processors</param>
    /// <param name="producerType">the claim strategy to use for the ring buffer</param>
    /// <param name="waitStrategy">the wait strategy to use for the ring buffer</param>
    public UnmanagedDisruptor(IntPtr pointer, int eventSize, int ringBufferSize, TaskScheduler taskScheduler, ProducerType producerType, IWaitStrategy waitStrategy)
        : this(new UnmanagedRingBuffer<T>(pointer, eventSize, SequencerFactory.Create(producerType, ringBufferSize, waitStrategy)), taskScheduler)
    {
    }

    private UnmanagedDisruptor(UnmanagedRingBuffer<T> ringBuffer, TaskScheduler taskScheduler)
        : base(ringBuffer, taskScheduler)
    {
    }

    /// <summary>
    /// The <see cref="UnmanagedRingBuffer{T}"/> used by this disruptor. This is useful for creating custom
    /// event processors if the behaviour of <see cref="IValueEventProcessor{T}"/> is not suitable.
    /// </summary>
    public new UnmanagedRingBuffer<T> RingBuffer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var ringBuffer = base.RingBuffer;
            return Unsafe.As<IValueRingBuffer<T>, UnmanagedRingBuffer<T>>(ref ringBuffer);
        }
    }

    /// <summary>
    /// Get the value of the cursor indicating the published sequence.
    /// </summary>
    public long Cursor => RingBuffer.Cursor;

    /// <summary>
    /// The capacity of the data structure to hold entries.
    /// </summary>
    public long BufferSize => RingBuffer.BufferSize;

    /// <summary>
    /// Get the event for a given sequence in the RingBuffer.
    /// </summary>
    /// <param name="sequence">sequence for the event</param>
    /// <returns>event for the sequence</returns>
    public ref T this[long sequence] => ref RingBuffer[sequence];

    /// <inheritdoc cref="UnmanagedRingBuffer{T}.PublishEvent"/>.
    public UnmanagedRingBuffer<T>.UnpublishedEventScope PublishEvent() => RingBuffer.PublishEvent();

    /// <inheritdoc cref="UnmanagedRingBuffer{T}.PublishEvents"/>.
    public UnmanagedRingBuffer<T>.UnpublishedEventBatchScope PublishEvents(int count) => RingBuffer.PublishEvents(count);
}
