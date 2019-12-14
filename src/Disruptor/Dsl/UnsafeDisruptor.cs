using System;
using System.Threading.Tasks;

namespace Disruptor.Dsl
{
    /// <summary>
    /// A DSL-style API for setting up the disruptor pattern around a ring buffer
    /// (aka the Builder pattern).
    /// </summary>
    /// <typeparam name="T">the type of event used.</typeparam>
    public class UnsafeDisruptor<T> : ValueDisruptor<T, UnsafeRingBuffer<T>>
        where T : unmanaged
    {
        /// <summary>
        /// Create a new UnsafeRingBuffer. Will default to <see cref="BlockingWaitStrategy"/> and <see cref="ProducerType.Multi"/>.
        /// </summary>
        /// <param name="pointer">pointer to the first event of the buffer</param>
        /// <param name="eventSize">size of each event</param>
        /// <param name="ringBufferSize">the number of events of the ring buffer, must be power of 2</param>
        public UnsafeDisruptor(IntPtr pointer, int eventSize, int ringBufferSize)
            : this(pointer, eventSize, ringBufferSize, TaskScheduler.Default)
        {
        }

        /// <summary>
        /// Create a new ValueDisruptor. Will default to <see cref="BlockingWaitStrategy"/> and <see cref="ProducerType.Multi"/>.
        /// </summary>
        /// <param name="pointer">pointer to the first event of the buffer</param>
        /// <param name="eventSize">size of each event</param>
        /// <param name="ringBufferSize">the number of events of the ring buffer, must be power of 2</param>
        /// <param name="taskScheduler">a <see cref="TaskScheduler"/> to create threads for processors</param>
        public UnsafeDisruptor(IntPtr pointer, int eventSize, int ringBufferSize, TaskScheduler taskScheduler)
            : this(pointer, eventSize, ringBufferSize, new BasicExecutor(taskScheduler))
        {
        }

        /// <summary>
        /// Create a new ValueDisruptor. Will default to <see cref="BlockingWaitStrategy"/> and <see cref="ProducerType.Multi"/>.
        /// </summary>
        /// <param name="pointer">pointer to the first event of the buffer</param>
        /// <param name="eventSize">size of each event</param>
        /// <param name="ringBufferSize">the number of events of the ring buffer, must be power of 2</param>
        /// <param name="executor">an <see cref="IExecutor"/> to create threads for processors</param>
        public UnsafeDisruptor(IntPtr pointer, int eventSize, int ringBufferSize, IExecutor executor)
            : this(new UnsafeRingBuffer<T>(pointer, eventSize, Sequencer.Create(ProducerType.Multi, ringBufferSize)), executor)
        {
        }

        /// <summary>
        /// Create a new ValueDisruptor.
        /// </summary>
        /// <param name="pointer">pointer to the first event of the buffer</param>
        /// <param name="eventSize">size of each event</param>
        /// <param name="ringBufferSize">the number of events of the ring buffer, must be power of 2</param>
        /// <param name="producerType">the claim strategy to use for the ring buffer</param>
        /// <param name="waitStrategy">the wait strategy to use for the ring buffer</param>
        public UnsafeDisruptor(IntPtr pointer, int eventSize, int ringBufferSize, ProducerType producerType, IWaitStrategy waitStrategy)
            : this(new UnsafeRingBuffer<T>(pointer, eventSize, Sequencer.Create(producerType, ringBufferSize, waitStrategy)), new BasicExecutor(TaskScheduler.Default))
        {
        }

        /// <summary>
        /// Create a new ValueDisruptor.
        ///
        /// The <see cref="UnsafeRingBufferMemory"/> is not owned by the disruptor and should be disposed after shutdown.
        /// </summary>
        /// <param name="memory">block of memory that will store the events</param>
        /// <param name="producerType">the claim strategy to use for the ring buffer</param>
        /// <param name="waitStrategy">the wait strategy to use for the ring buffer</param>
        public UnsafeDisruptor(UnsafeRingBufferMemory memory, ProducerType producerType, IWaitStrategy waitStrategy)
            : this(new UnsafeRingBuffer<T>(memory, producerType, waitStrategy), new BasicExecutor(TaskScheduler.Default))
        {
        }

        /// <summary>
        /// Create a new ValueDisruptor.
        /// </summary>
        /// <param name="pointer">pointer to the first event of the buffer</param>
        /// <param name="eventSize">size of each event</param>
        /// <param name="ringBufferSize">the number of events of the ring buffer, must be power of 2</param>
        /// <param name="taskScheduler">a <see cref="TaskScheduler"/> to create threads for processors</param>
        /// <param name="producerType">the claim strategy to use for the ring buffer</param>
        /// <param name="waitStrategy">the wait strategy to use for the ring buffer</param>
        public UnsafeDisruptor(IntPtr pointer, int eventSize, int ringBufferSize, TaskScheduler taskScheduler, ProducerType producerType, IWaitStrategy waitStrategy)
            : this(new UnsafeRingBuffer<T>(pointer, eventSize, Sequencer.Create(producerType, ringBufferSize, waitStrategy)), new BasicExecutor(taskScheduler))
        {
        }

        private UnsafeDisruptor(UnsafeRingBuffer<T> ringBuffer, IExecutor executor)
            : base(ringBuffer, executor)
        {
        }

        /// <summary>
        /// The <see cref="UnsafeRingBuffer{T}"/> used by this disruptor. This is useful for creating custom
        /// event processors if the behaviour of <see cref="IValueBatchEventProcessor{T}"/> is not suitable.
        /// </summary>
        public UnsafeRingBuffer<T> RingBuffer => _ringBuffer;

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
        /// <see cref="UnsafeRingBuffer{T}.PublishEvent"/>
        /// </summary>
        public UnsafeRingBuffer<T>.UnpublishedEventScope PublishEvent() => _ringBuffer.PublishEvent();

        /// <summary>
        /// <see cref="UnsafeRingBuffer{T}.PublishEvents"/>
        /// </summary>
        public UnsafeRingBuffer<T>.UnpublishedEventBatchScope PublishEvents(int count) => _ringBuffer.PublishEvents(count);
    }
}
