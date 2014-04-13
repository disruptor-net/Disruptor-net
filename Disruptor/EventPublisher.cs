using System;

namespace Disruptor
{
    /// <summary>
    /// Utility class for simplifying publication to the ring buffer.
    /// </summary>
    public sealed class EventPublisher<T> where T : class
    {
        private readonly RingBuffer<T> _ringBuffer;

        /// <summary>
        /// Construct from the ring buffer to be published to.
        /// </summary>
        /// <param name="ringBuffer">ringBuffer into which events will be published.</param>
        public EventPublisher(RingBuffer<T> ringBuffer)
        {
            _ringBuffer = ringBuffer;
        }

        /// <summary>
        /// Publishes an event to the ring buffer.  It handles
        /// claiming the next sequence, getting the current (uninitialized) 
        /// event from the ring buffer and publishing the claimed sequence
        /// after translation.
        /// </summary>
        /// <param name="translator">The user specified translation for the event</param>
        public void PublishEvent(Func<T,long,T> translator)
        {
            long sequence = _ringBuffer.Next();
            TranslateAndPublish(translator, sequence);
        }
        /// <summary>
        /// <param name="translator">The user specified translation for the event</param>
        /// <param name="capacity">The capacity that should be available before publishing</param>
        /// <returns>true if the value was published, false if there was insufficient
        /// capacity.</returns>
        /// </summary>
        public bool TryPublishEvent(Func<T, long, T> translator, int capacity)
        {
            try
            {
                long sequence = _ringBuffer.TryNext(capacity);
                TranslateAndPublish(translator, sequence);
                return true;
            }
            catch (InsufficientCapacityException)
            {
                return false;
            }
        }


        private void TranslateAndPublish(Func<T, long, T> translator, long sequence)
        {
            try
            {
                translator(_ringBuffer[sequence], sequence);
            }
            finally
            {
                _ringBuffer.Publish(sequence);
            }
        }
    }
}