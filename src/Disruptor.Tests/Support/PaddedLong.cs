using System.Runtime.InteropServices;

namespace Disruptor.Tests.Support
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PaddedLong
    {
        [FieldOffset(0)]
        public long Value;
    }
}