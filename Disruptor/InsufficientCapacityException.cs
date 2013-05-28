using System;

namespace Disruptor
{
    /// <summary>
    /// Used to alert <see cref="IEventProcessor"/>s waiting at a <see cref="ISequenceBarrier"/> of status changes.
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
        }
    }
}