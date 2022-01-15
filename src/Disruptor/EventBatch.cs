using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Disruptor.Util;

// It would be much cleaner to include the batch sequence in the struct, as it would enable
// to add BeginSequence and EndSequence properties, and also a EventAndSequences() enumerator.
// Yet, adding a long to this struct negatively impacts the benchmarks or performance tests.
// I suspect that the JIT has optimizations for small structs that do not apply beyond 16 bytes.

namespace Disruptor
{
    /// <summary>
    /// Contiguous batch of events from a <see cref="RingBuffer{T}"/>.
    /// </summary>
    /// <remarks>
    /// It is slightly faster to enumerate this collection using <see cref="AsSpan()"/>.
    /// Please consider <c>foreach (var data in batch.AsSpan())</c> instead of <c>foreach (var data in batch)</c>.
    /// </remarks>
    public readonly struct EventBatch<T>
        where T : class
    {
        private readonly object _array;
        private readonly int _beginIndex;
        private readonly int _length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal EventBatch(object array, int beginIndex, int length)
        {
            _array = array;
            _beginIndex = beginIndex;
            _length = length;
        }

        public EventBatch(T[] array, int beginIndex, int length)
        {
            if ((uint)beginIndex > (uint)array.Length || (uint)length > (uint)(array.Length - beginIndex))
                ThrowHelper.ThrowArgumentOutOfRangeException();

            _array = array;
            _beginIndex = beginIndex;
            _length = length;
        }

        /// <summary>
        /// Number of events in the batch.
        /// </summary>
        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _length;
        }

        /// <summary>
        /// Gets an event for a given index in the batch.
        /// </summary>
        public T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_length)
                    ThrowHelper.ThrowArgumentOutOfRangeException();

                return InternalUtil.Read<T>(_array, _beginIndex + index);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator() => new(_array, _beginIndex, _beginIndex + _length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<T> AsSpan() => InternalUtil.ReadSpan<T>(_array, _beginIndex, _length);

        public IEnumerable<T> AsEnumerable()
        {
            foreach (var data in this)
            {
                yield return data;
            }
        }

        /// <summary>
        /// Copies the content of the batch into a new array.
        /// </summary>
        public T[] ToArray() => AsSpan().ToArray();

        public struct Enumerator
        {
            private readonly object _array;
            private readonly int _endIndex;
            private int _index;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Enumerator(object array, int index, int endIndex)
            {
                _array = array;
                _index = index - 1;
                _endIndex = endIndex;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                var index = _index + 1;
                if (index < _endIndex)
                {
                    _index = index;
                    return true;
                }

                return false;
            }

            public readonly T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => InternalUtil.Read<T>(_array, _index);
            }
        }
    }
}
