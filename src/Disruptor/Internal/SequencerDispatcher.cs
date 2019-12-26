using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Disruptor.Internal
{
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public readonly struct SequencerDispatcher
    {
        [FieldOffset(0)]
        private readonly  SingleProducerSequencer _singleProducerSequencer;

        [FieldOffset(0)]
        private readonly MultiProducerSequencer _multiProducerSequencer;

        [FieldOffset(0)]
        public readonly ISequencer Sequencer;

        [FieldOffset(8)]
        private readonly SequencerType _type;

        // padding: 7

        public SequencerDispatcher(ISequencer sequencer)
        {
            _singleProducerSequencer = default;
            _multiProducerSequencer = default;
            _type = sequencer switch
            {
                SingleProducerSequencer _ => SequencerType.SingleProducer,
                MultiProducerSequencer _  => SequencerType.MultiProducer,
                _                         => SequencerType.Unknown,
            };

            Sequencer = sequencer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long Next()
        {
            switch (_type)
            {
                case SequencerType.SingleProducer:
                    return _singleProducerSequencer.NextInternal(1);
                case SequencerType.MultiProducer:
                    return _multiProducerSequencer.NextInternal(1);
                default:
                    return Sequencer.Next();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long Next(int n)
        {
            switch (_type)
            {
                case SequencerType.SingleProducer:
                    return _singleProducerSequencer.NextInternal(n);
                case SequencerType.MultiProducer:
                    return _multiProducerSequencer.NextInternal(n);
                default:
                    return Sequencer.Next(n);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryNext(out long sequence)
        {
            switch (_type)
            {
                case SequencerType.SingleProducer:
                    return _singleProducerSequencer.TryNextInternal(1, out sequence);
                case SequencerType.MultiProducer:
                    return _multiProducerSequencer.TryNextInternal(1, out sequence);
                default:
                    return Sequencer.TryNext(out sequence);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryNext(int n, out long sequence)
        {
            switch (_type)
            {
                case SequencerType.SingleProducer:
                    return _singleProducerSequencer.TryNextInternal(n, out sequence);
                case SequencerType.MultiProducer:
                    return _multiProducerSequencer.TryNextInternal(n, out sequence);
                default:
                    return Sequencer.TryNext(n, out sequence);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Publish(long sequence)
        {
            switch (_type)
            {
                case SequencerType.SingleProducer:
                    _singleProducerSequencer.Publish(sequence);
                    break;
                case SequencerType.MultiProducer:
                    _multiProducerSequencer.Publish(sequence);
                    break;
                default:
                    Sequencer.Publish(sequence);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Publish(long lo, long hi)
        {
            switch (_type)
            {
                case SequencerType.SingleProducer:
                    _singleProducerSequencer.Publish(hi);
                    break;
                case SequencerType.MultiProducer:
                    _multiProducerSequencer.Publish(lo, hi);
                    break;
                default:
                    Sequencer.Publish(lo, hi);
                    break;
            }
        }

        private enum SequencerType : byte
        {
            SingleProducer,
            MultiProducer,
            Unknown,
        }
    }
}
