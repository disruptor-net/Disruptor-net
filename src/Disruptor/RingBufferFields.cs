using System.Runtime.InteropServices;

namespace Disruptor
{
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct RingBufferFields
    {
        // padding: 56

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
}
