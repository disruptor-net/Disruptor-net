using System;
using System.Runtime.InteropServices;

namespace Disruptor
{
    /// <summary>
    /// Base type for unmanaged-memory-backed ring buffers.
    ///
    /// <see cref="UnmanagedRingBuffer{T}"/>.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 148)]
    public abstract class UnmanagedRingBuffer : RingBuffer
    {
        [FieldOffset(56)]
        protected IntPtr _entries;

        [FieldOffset(72)]
        protected int _eventSize;

        protected UnmanagedRingBuffer(IntPtr entries, int eventSize, ISequencer sequencer)
            : base(sequencer)
        {
            if (eventSize < 1)
            {
                throw new ArgumentException("eventSize must not be less than 1");
            }

            _entries = entries;
            _eventSize = eventSize;
        }
    }
}
