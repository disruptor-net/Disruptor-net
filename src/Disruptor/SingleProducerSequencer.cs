using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Disruptor.Dsl;

namespace Disruptor
{
    [StructLayout(LayoutKind.Explicit, Size = 160)]
    public class SingleProducerSequencer : ISequencer
    {
        // padding: 56

        [FieldOffset(56)]
        private readonly IWaitStrategy _waitStrategy;

        [FieldOffset(64)]
        private readonly Sequence _cursor = new Sequence();

        [FieldOffset(72)]
        // volatile in the Java version => always use Volatile.Read/Write or Interlocked methods to access this field
        private ISequence[] _gatingSequences = new ISequence[0];

        [FieldOffset(80)]
        private readonly int _bufferSize;

        [FieldOffset(84)]
        private readonly bool _isBlockingWaitStrategy;

        // padding: 3

        [FieldOffset(88)]
        private long _nextValue = Sequence.InitialCursorValue;

        [FieldOffset(96)]
        private long _cachedValue = Sequence.InitialCursorValue;

        // padding: 56

        public SingleProducerSequencer(int bufferSize)
            : this(bufferSize, SequencerFactory.DefaultWaitStrategy())
        {
        }

        public SingleProducerSequencer(int bufferSize, IWaitStrategy waitStrategy)
        {
            if (bufferSize < 1)
            {
                throw new ArgumentException("bufferSize must not be less than 1");
            }
            if (!bufferSize.IsPowerOf2())
            {
                throw new ArgumentException("bufferSize must be a power of 2");
            }

            _bufferSize = bufferSize;
            _waitStrategy = waitStrategy;
            _isBlockingWaitStrategy = !(waitStrategy is INonBlockingWaitStrategy);
        }

        /// <summary>
        /// <see cref="ISequencer.NewBarrier"/>
        /// </summary>
        public ISequenceBarrier NewBarrier(params ISequence[] sequencesToTrack)
        {
            return ProcessingSequenceBarrierFactory.Create(this, _waitStrategy, _cursor, sequencesToTrack);
        }

        /// <summary>
        /// <see cref="ISequenced.BufferSize"/>.
        /// </summary>
        public int BufferSize => _bufferSize;

        /// <summary>
        /// <see cref="ICursored.Cursor"/>.
        /// </summary>
        public long Cursor => _cursor.Value;

        /// <summary>
        /// <see cref="ISequenced.HasAvailableCapacity"/>.
        /// </summary>
        public bool HasAvailableCapacity(int requiredCapacity)
        {
            return HasAvailableCapacity(requiredCapacity, false);
        }

        private bool HasAvailableCapacity(int requiredCapacity, bool doStore)
        {
            long nextValue = _nextValue;

            long wrapPoint = (nextValue + requiredCapacity) - _bufferSize;
            long cachedGatingSequence = _cachedValue;

            if (wrapPoint > cachedGatingSequence || cachedGatingSequence > nextValue)
            {
                if (doStore)
                {
                    _cursor.SetValueVolatile(nextValue);
                }

                long minSequence = DisruptorUtil.GetMinimumSequence(Volatile.Read(ref _gatingSequences), nextValue);
                _cachedValue = minSequence;

                if (wrapPoint > minSequence)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// <see cref="ISequenced.Next()"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long Next()
        {
            return NextInternal(1);
        }

        /// <summary>
        /// <see cref="ISequenced.Next(int)"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long Next(int n)
        {
            if (n < 1 || n > _bufferSize)
            {
                ThrowHelper.ThrowArgMustBeGreaterThanZeroAndLessThanBufferSize();
            }

            return NextInternal(n);
        }

        internal long NextInternal(int n)
        {
            long nextValue = _nextValue;

            long nextSequence = nextValue + n;
            long wrapPoint = nextSequence - _bufferSize;
            long cachedGatingSequence = _cachedValue;

            if (wrapPoint > cachedGatingSequence || cachedGatingSequence > nextValue)
            {
                _cursor.SetValueVolatile(nextValue);

                var spinWait = default(AggressiveSpinWait);
                long minSequence;
                while (wrapPoint > (minSequence = DisruptorUtil.GetMinimumSequence(Volatile.Read(ref _gatingSequences), nextValue)))
                {
                    spinWait.SpinOnce();
                }

                _cachedValue = minSequence;
            }

            _nextValue = nextSequence;

            return nextSequence;
        }

        /// <summary>
        /// <see cref="ISequenced.TryNext(out long)"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryNext(out long sequence)
        {
            return TryNextInternal(1, out sequence);
        }

        /// <summary>
        /// <see cref="ISequenced.TryNext(int, out long)"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryNext(int n, out long sequence)
        {
            if (n < 1 || n > _bufferSize)
            {
                ThrowHelper.ThrowArgMustBeGreaterThanZeroAndLessThanBufferSize();
            }

            return TryNextInternal(n, out sequence);
        }

        internal bool TryNextInternal(int n, out long sequence)
        {
            if (!HasAvailableCapacity(n, true))
            {
                sequence = default(long);
                return false;
            }

            var nextSequence = _nextValue + n;
            _nextValue = nextSequence;

            sequence = nextSequence;
            return true;
        }

        /// <summary>
        /// <see cref="ISequenced.GetRemainingCapacity"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetRemainingCapacity()
        {
            var nextValue = _nextValue;

            var consumed = DisruptorUtil.GetMinimumSequence(Volatile.Read(ref _gatingSequences), nextValue);
            var produced = nextValue;
            return BufferSize - (produced - consumed);
        }

        /// <summary>
        /// <see cref="ISequencer.Claim"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Claim(long sequence)
        {
            _nextValue = sequence;
        }

        /// <summary>
        /// <see cref="ISequenced.Publish(long)"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Publish(long sequence)
        {
            _cursor.SetValue(sequence);

            if (_isBlockingWaitStrategy)
            {
                _waitStrategy.SignalAllWhenBlocking();
            }
        }

        /// <summary>
        /// <see cref="ISequenced.Publish(long, long)"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Publish(long lo, long hi)
        {
            Publish(hi);
        }

        /// <summary>
        /// <see cref="ISequencer.IsAvailable"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAvailable(long sequence)
        {
            var currentSequence = _cursor.Value;
            return sequence <= currentSequence && sequence > currentSequence - _bufferSize;
        }

        /// <summary>
        /// <see cref="ISequencer.GetHighestPublishedSequence"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetHighestPublishedSequence(long nextSequence, long availableSequence)
        {
            return availableSequence;
        }

        /// <summary>
        /// <see cref="ISequencer.AddGatingSequences"/>.
        /// </summary>
        public void AddGatingSequences(params ISequence[] gatingSequences)
        {
            SequenceGroups.AddSequences(ref _gatingSequences, this, gatingSequences);
        }

        /// <summary>
        /// <see cref="ISequencer.RemoveGatingSequence"/>.
        /// </summary>
        public bool RemoveGatingSequence(ISequence sequence)
        {
            return SequenceGroups.RemoveSequence(ref _gatingSequences, sequence);
        }

        /// <summary>
        /// <see cref="ISequencer.GetMinimumSequence"/>.
        /// </summary>
        public long GetMinimumSequence()
        {
            return DisruptorUtil.GetMinimumSequence(Volatile.Read(ref _gatingSequences), _cursor.Value);
        }

        /// <summary>
        /// <see cref="ISequencer.NewPoller{T}(IDataProvider{T}, ISequence[])"/>.
        /// </summary>
        public EventPoller<T> NewPoller<T>(IDataProvider<T> provider, params ISequence[] gatingSequences)
        {
            return EventPoller.Create(provider, this, new Sequence(), _cursor, gatingSequences);
        }

        /// <summary>
        /// <see cref="ISequencer.NewPoller{T}(IValueDataProvider{T}, ISequence[])"/>.
        /// </summary>
        public ValueEventPoller<T> NewPoller<T>(IValueDataProvider<T> provider, params ISequence[] gatingSequences)
            where T : struct
        {
            return EventPoller.Create(provider, this, new Sequence(), _cursor, gatingSequences);
        }
    }
}
