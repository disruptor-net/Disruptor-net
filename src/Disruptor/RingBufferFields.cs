using System.Runtime.InteropServices;

namespace Disruptor
{
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct RingBufferFields
    {
        // 56: protected long p1, p2, p3, p4, p5, p6, p7;

        [FieldOffset(56)]
        public object[] Entries;

        [FieldOffset(64)]
        public ISequencer Sequencer;

        // 56: protected long p1, p2, p3, p4, p5, p6, p7;
    }
}