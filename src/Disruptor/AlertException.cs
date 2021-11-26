using System;
using System.Runtime.CompilerServices;
using Disruptor.Processing;

namespace Disruptor
{
    /// <summary>
    /// Used to alert <see cref="IEventProcessor"/>s waiting at a <see cref="ISequenceBarrier"/> of status changes.
    /// </summary>
    public class AlertException : Exception
    {
        /// <summary>
        /// Pre-allocated exception to avoid garbage generation
        /// </summary>
        public static readonly AlertException Instance = new AlertException();

        /// <summary>
        /// Private constructor so only a single instance exists.
        /// </summary>
        private AlertException()
        {
        }

        /// <summary>
        /// Non-inlinable method helper to throw the exception.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Throw()
        {
            throw Instance;
        }
    }
}
