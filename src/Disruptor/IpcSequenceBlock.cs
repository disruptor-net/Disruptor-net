using System.Runtime.InteropServices;

namespace Disruptor;

[StructLayout(LayoutKind.Explicit, Size = 64)]
internal struct IpcSequenceBlock
{
    [FieldOffset(0)]
    public long SequenceValue;
}
