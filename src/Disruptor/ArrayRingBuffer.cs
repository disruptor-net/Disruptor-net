using System;
using System.Runtime.InteropServices;

namespace Disruptor
{
    /// <summary>
    /// Base type for array-backed ring buffers.
    ///
    /// <see cref="RingBuffer{T}"/> and <see cref="ValueRingBuffer{T}"/>.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 148)]
    public abstract class ArrayRingBuffer : RingBuffer
    {
        protected static readonly int _bufferPadRef = Util.GetRingBufferPaddingEventCount(IntPtr.Size);

        [FieldOffset(56)]
        protected object _entries;

        protected ArrayRingBuffer(ISequencer sequencer, Type elementType, int padding)
            : base(sequencer)
        {
            _entries = Array.CreateInstance(elementType, _bufferSize + 2 * padding);
        }
    }
}
