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
        public SingleProducerSequencer SingleProducerSequencer;

        [FieldOffset(64)]
        public MultiProducerSequencer MultiProducerSequencer;

        [FieldOffset(64)]
        public ISequencer Sequencer;

        [FieldOffset(72)]
        public RingBufferSequencerType SequencerType;

        // 56: protected long p1, p2, p3, p4, p5, p6, p7;

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
