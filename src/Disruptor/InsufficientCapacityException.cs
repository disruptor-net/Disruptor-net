using System;

namespace Disruptor
{
    /// <summary>
    /// Exception thrown when it is not possible to insert a value into
    /// the ring buffer without it wrapping the consuming sequences. Used
    /// specifically when claiming with the <see cref="RingBuffer{T}.TryNext()"/> call.
    /// </summary>
    public class InsufficientCapacityException : Exception
    {
        /// <summary>
        /// Pre-allocated exception to avoid garbage generation
        /// </summary>
        public static readonly InsufficientCapacityException Instance = new InsufficientCapacityException();

        /// <summary>
        /// Private constructor so only a single instance exists.
        /// </summary>
        private InsufficientCapacityException()
        {
            // Singleton
        }
    }
}