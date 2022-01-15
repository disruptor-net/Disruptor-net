using System;
using System.Threading.Tasks;
using Disruptor.Processing;

namespace Disruptor.Dsl;

/// <summary>
/// A DSL-style API for setting up the disruptor pattern around a ring buffer
/// (aka the Builder pattern).
///
/// A simple example of setting up the disruptor with two event handlers that
/// must process events in order:
/// <code>var disruptor = new ValueDisruptor{MyEvent}(() => new MyEvent(), 32, TaskScheduler.Default);
/// var handler1 = new EventHandler1{MyEvent}() { ... };
/// var handler2 = new EventHandler2{MyEvent}() { ... };
/// disruptor.HandleEventsWith(handler1).Then(handler2);
///
/// var ringBuffer = disruptor.Start();</code>
/// </summary>
/// <typeparam name="T">the type of event used.</typeparam>
public class ValueDisruptor<T> : ValueDisruptor<T, ValueRingBuffer<T>>
    where T : struct
{
    /// <summary>
    /// Create a new ValueDisruptor. Will default to <see cref="BlockingWaitStrategy"/> and <see cref="ProducerType.Multi"/>.
    /// </summary>
    /// <param name="eventFactory">the factory to create events in the ring buffer</param>
    /// <param name="ringBufferSize">the size of the ring buffer, must be power of 2</param>
    public ValueDisruptor(Func<T> eventFactory, int ringBufferSize)
        : this(eventFactory, ringBufferSize, TaskScheduler.Default)
    {
    }

    /// <summary>
    /// Create a new ValueDisruptor.
    /// </summary>
    /// <param name="eventFactory">the factory to create events in the ring buffer</param>
    /// <param name="ringBufferSize">the size of the ring buffer, must be power of 2</param>
    /// <param name="taskScheduler">a <see cref="TaskScheduler"/> to create threads for processors</param>
    /// <param name="producerType">the claim strategy to use for the ring buffer</param>
    /// <param name="waitStrategy">the wait strategy to use for the ring buffer</param>
    public ValueDisruptor(Func<T> eventFactory, int ringBufferSize, TaskScheduler taskScheduler, ProducerType producerType, IWaitStrategy waitStrategy)
        : this(new ValueRingBuffer<T>(eventFactory, SequencerFactory.Create(producerType, ringBufferSize, waitStrategy)), taskScheduler)
    {
    }

    /// <summary>
    /// Create a new ValueDisruptor. Will default to <see cref="BlockingWaitStrategy"/> and <see cref="ProducerType.Multi"/>.
    /// </summary>
    /// <param name="eventFactory">the factory to create events in the ring buffer</param>
    /// <param name="ringBufferSize">the size of the ring buffer, must be power of 2</param>
    /// <param name="taskScheduler">an <see cref="TaskScheduler"/> to create threads for processors</param>
    public ValueDisruptor(Func<T> eventFactory, int ringBufferSize, TaskScheduler taskScheduler)
        : this(ValueRingBuffer<T>.CreateMultiProducer(eventFactory, ringBufferSize), taskScheduler)
    {
    }

    private ValueDisruptor(ValueRingBuffer<T> ringBuffer, TaskScheduler taskScheduler)
        : base(ringBuffer, taskScheduler)
    {
    }

    /// <summary>
    /// The <see cref="ValueRingBuffer{T}"/> used by this disruptor. This is useful for creating custom
    /// event processors if the behaviour of <see cref="IValueEventProcessor{T}"/> is not suitable.
    /// </summary>
    public ValueRingBuffer<T> RingBuffer => _ringBuffer;

    /// <summary>
    /// Get the value of the cursor indicating the published sequence.
    /// </summary>
    public long Cursor => _ringBuffer.Cursor;

    /// <summary>
    /// The capacity of the data structure to hold entries.
    /// </summary>
    public long BufferSize => _ringBuffer.BufferSize;

    /// <summary>
    /// Get the event for a given sequence in the RingBuffer.
    /// </summary>
    /// <param name="sequence">sequence for the event</param>
    /// <returns>event for the sequence</returns>
    public ref T this[long sequence] => ref _ringBuffer[sequence];

    /// <summary>
    /// <see cref="ValueRingBuffer{T}.PublishEvent"/>
    /// </summary>
    public ValueRingBuffer<T>.UnpublishedEventScope PublishEvent() => _ringBuffer.PublishEvent();

    /// <summary>
    /// <see cref="ValueRingBuffer{T}.PublishEvents"/>
    /// </summary>
    public ValueRingBuffer<T>.UnpublishedEventBatchScope PublishEvents(int count) => _ringBuffer.PublishEvents(count);
}