using System.Runtime.InteropServices;

namespace Disruptor.Tests.Support
{
    // TODO: expand to cache line and create a PR on java version
    [StructLayout(LayoutKind.Explicit, Size = 56)]
    public struct PaddedLong
    {
        [FieldOffset(0)]
        public long Value;

        public PaddedLong(long value)
        {
            Value = value;
        }
    }
}