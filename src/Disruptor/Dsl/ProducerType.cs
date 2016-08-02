namespace Disruptor.Dsl
{
    /// <summary>
    /// Defines producer types to support creation of RingBuffer with correct sequencer and publisher.
    /// </summary>
    public enum ProducerType
    {
        /// <summary>
        /// Create a RingBuffer with a single event publisher to the RingBuffer
        /// </summary>
        Single,

        /// <summary>
        /// Create a RingBuffer supporting multiple event publishers to the one RingBuffer
        /// </summary>
        Multi
    }
}