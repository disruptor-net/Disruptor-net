using System;
using System.Runtime.InteropServices;
using static Disruptor.Constants;

namespace Disruptor
{
    [StructLayout(LayoutKind.Explicit, Size = CacheLineSize * 2 + 32)]
    internal struct RingBufferFields
    {
        public static readonly int BufferPad = 128 / IntPtr.Size;

        // padding: CacheLineSize

        [FieldOffset(CacheLineSize)]
        public object[] Entries;

        [FieldOffset(CacheLineSize + 8)]
        public long IndexMask;

        [FieldOffset(CacheLineSize + 16)]
        public int BufferSize;

        [FieldOffset(CacheLineSize + 20)]
        public RingBufferSequencerType SequencerType;

        // padding: 3

        [FieldOffset(CacheLineSize + 24)]
        public SingleProducerSequencer SingleProducerSequencer;

        [FieldOffset(CacheLineSize + 24)]
        public MultiProducerSequencer MultiProducerSequencer;

        [FieldOffset(CacheLineSize + 24)]
        public ISequencer Sequencer;

        // padding: CacheLineSize

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
}
