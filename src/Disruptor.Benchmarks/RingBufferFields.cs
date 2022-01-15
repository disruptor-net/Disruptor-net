using System;
using System.Runtime.InteropServices;

namespace Disruptor;

[StructLayout(LayoutKind.Explicit, Size = 144)]
internal struct RingBufferFields
{
    public static readonly int BufferPad = 128 / IntPtr.Size;

    // padding: 56

    [FieldOffset(56)]
    public object[] Entries;

    [FieldOffset(64)]
    public long IndexMask;

    [FieldOffset(72)]
    public int BufferSize;

    [FieldOffset(76)]
    public RingBufferSequencerType SequencerType;

    // padding: 3

    [FieldOffset(80)]
    public SingleProducerSequencer SingleProducerSequencer;

    [FieldOffset(80)]
    public MultiProducerSequencer MultiProducerSequencer;

    [FieldOffset(80)]
    public ISequencer Sequencer;

    // padding: 56

    public enum RingBufferSequencerType : byte
    {
        SingleProducer,
        MultiProducer,
        Unknown,
    }

    public static RingBufferSequencerType GetSequencerType(ISequencer sequencer)
    {
        switch (sequencer)
        {
            case SingleProducerSequencer s:
                return RingBufferSequencerType.SingleProducer;
            case MultiProducerSequencer m:
                return RingBufferSequencerType.MultiProducer;
            default:
                return RingBufferSequencerType.Unknown;
        }
    }
}