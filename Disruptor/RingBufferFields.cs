using System.Runtime.InteropServices;

namespace Disruptor
{
    [StructLayout(LayoutKind.Explicit, Size = 144)]
    internal struct RingBufferFields
    {
        // 56: protected long p1, p2, p3, p4, p5, p6, p7;

        [FieldOffset(56)]
        public int IndexMask;

        [FieldOffset(64)]
        public object[] Entries;

        [FieldOffset(72)]
        public int BufferSize;

        [FieldOffset(80)]
        public ISequencer Sequencer;

        // 56: protected long p1, p2, p3, p4, p5, p6, p7;
    }
}