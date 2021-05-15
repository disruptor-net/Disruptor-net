using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Disruptor.Dsl;

namespace Disruptor
{
    /// <summary>
    /// <para>Coordinator for claiming sequences for access to a data structure while tracking dependent <see cref="Sequence"/>s.
    /// Suitable for use for sequencing across multiple publisher threads.</para>
    /// <para/>
    /// <para/>Note on <see cref="ICursored.Cursor"/>:  With this sequencer the cursor value is updated after the call
    /// to <see cref="ISequenced.Next()"/>, to determine the highest available sequence that can be read, then
    /// <see cref="GetHighestPublishedSequence"/> should be used.
    /// </summary>
    public unsafe class MultiProducerSequencer : ISequencer
    {
        private readonly int _bufferSize;
        private readonly IWaitStrategy _waitStrategy;
        private readonly bool _isBlockingWaitStrategy;
        private readonly Sequence _cursor = new Sequence();

        // volatile in the Java version => always use Volatile.Read/Write or Interlocked methods to access this field
        private ISequence[] _gatingSequences = new ISequence[0];

        private readonly Sequence _gatingSequenceCache = new Sequence();
        private readonly int[] _availableBuffer;
#if NETCOREAPP
        private readonly int* _availableBufferPointer;
#endif
        private readonly int _indexMask;
        private readonly int _indexShift;

        public MultiProducerSequencer(int bufferSize)
            : this(bufferSize, SequencerFactory.DefaultWaitStrategy())
        {
        }

        public MultiProducerSequencer(int bufferSize, IWaitStrategy waitStrategy)
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
#if NETCOREAPP
            _availableBuffer = GC.AllocateArray<int>(bufferSize, pinned: true);
            _availableBufferPointer = (int*)Unsafe.AsPointer(ref _availableBuffer[0]);
#else
            _availableBuffer = new int[bufferSize];
#endif
            _indexMask = bufferSize - 1;
            _indexShift = DisruptorUtil.Log2(bufferSize);

            InitialiseAvailableBuffer();
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasAvailableCapacity(int requiredCapacity)
        {
            return HasAvailableCapacity(requiredCapacity, _cursor.Value);
        }

        private bool HasAvailableCapacity(int requiredCapacity, long cursorValue)
        {
            var wrapPoint = (cursorValue + requiredCapacity) - _bufferSize;
            var cachedGatingSequence = _gatingSequenceCache.Value;

            if (wrapPoint > cachedGatingSequence || cachedGatingSequence > cursorValue)
            {
                var minSequence = DisruptorUtil.GetMinimumSequence(Volatile.Read(ref _gatingSequences), cursorValue);
                _gatingSequenceCache.SetValue(minSequence);

                if (wrapPoint > minSequence)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// <see cref="ISequencer.Claim"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Claim(long sequence)
        {
            _cursor.SetValue(sequence);
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
            if ((uint)(n - 1) >= _bufferSize)
            {
                ThrowHelper.ThrowArgMustBeGreaterThanZeroAndLessThanBufferSize();
            }

            return NextInternal(n);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal long NextInternal(int n)
        {
            var nextSequence = _cursor.AddAndGet(n);
            var current = nextSequence - n;
            var wrapPoint = nextSequence - _bufferSize;
            var cachedGatingSequence = _gatingSequenceCache.Value;

            if (wrapPoint > cachedGatingSequence || cachedGatingSequence > current)
            {
                var spinWait = default(AggressiveSpinWait);
                long gatingSequence;

                while (wrapPoint > (gatingSequence = DisruptorUtil.GetMinimumSequence(Volatile.Read(ref _gatingSequences), current)))
                {
                    spinWait.SpinOnce();
                }

                _gatingSequenceCache.SetValue(gatingSequence);
            }

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
            long current;
            long next;

            do
            {
                current = _cursor.Value;
                next = current + n;

                if (!HasAvailableCapacity(n, current))
                {
                    sequence = default;
                    return false;
                }
            }
            while (!_cursor.CompareAndSet(current, next));

            sequence = next;
            return true;
        }

        /// <summary>
        /// <see cref="ISequenced.GetRemainingCapacity"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetRemainingCapacity()
        {
            var consumed = DisruptorUtil.GetMinimumSequence(Volatile.Read(ref _gatingSequences), _cursor.Value);
            var produced = _cursor.Value;
            return BufferSize - (produced - consumed);
        }

        private void InitialiseAvailableBuffer()
        {
#if NETCOREAPP
            _availableBuffer.AsSpan().Fill(-1);
#else
            for (int i = _availableBuffer.Length - 1; i != 0; i--)
            {
                SetAvailableBufferValue(i, -1);
            }

            SetAvailableBufferValue(0, -1);
#endif
        }

        /// <summary>
        /// <see cref="ISequenced.Publish(long)"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Publish(long sequence)
        {
            SetAvailableBufferValue(CalculateIndex(sequence), CalculateAvailabilityFlag(sequence));

            if (_isBlockingWaitStrategy)
            {
                _waitStrategy.SignalAllWhenBlocking();
            }
        }

        /// <summary>
        /// <see cref="ISequenced.Publish(long, long)"/>.
        /// </summary>
        public void Publish(long lo, long hi)
        {
            for (var l = lo; l <= hi; l++)
            {
                SetAvailableBufferValue(CalculateIndex(l), CalculateAvailabilityFlag(l));
            }

            if (_isBlockingWaitStrategy)
            {
                _waitStrategy.SignalAllWhenBlocking();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetAvailableBufferValue(int index, int flag)
        {
#if NETCOREAPP
            _availableBufferPointer[index] = flag;
#else
            _availableBuffer[index] = flag;
#endif
        }

        /// <summary>
        /// <see cref="ISequencer.IsAvailable"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAvailable(long sequence)
        {
            var index = CalculateIndex(sequence);
            var flag = CalculateAvailabilityFlag(sequence);

#if NETCOREAPP
            return Volatile.Read(ref _availableBufferPointer[index]) == flag;
#else
            return Volatile.Read(ref _availableBuffer[index]) == flag;
#endif
        }

        /// <summary>
        /// <see cref="ISequencer.GetHighestPublishedSequence"/>.
        /// </summary>
        public long GetHighestPublishedSequence(long lowerBound, long availableSequence)
        {
            for (var sequence = lowerBound; sequence <= availableSequence; sequence++)
            {
                if (!IsAvailable(sequence))
                {
                    return sequence - 1;
                }
            }

            return availableSequence;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int CalculateAvailabilityFlag(long sequence)
        {
            return (int)((ulong)sequence >> _indexShift);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int CalculateIndex(long sequence)
        {
            return (int)sequence & _indexMask;
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
