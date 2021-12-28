using System.Runtime.CompilerServices;
using Disruptor.Util;

namespace Disruptor
{
    // TODO: implement IEnumerable or add AsEnumerable()
    public readonly struct EventBatch<T>
        where T : class
    {
        private readonly object _array;
        private readonly int _beginIndex;
        private readonly int _endIndex;
        private readonly long _beginSequence;

        internal EventBatch(object array, int beginIndex, int endIndex, long beginSequence)
        {
            _array = array;
            _beginIndex = beginIndex;
            _endIndex = endIndex;
            _beginSequence = beginSequence;
        }

        public EventBatch(T[] array, int beginIndex, int endIndex, long beginSequence)
        {
            _array = array;
            _beginIndex = beginIndex;
            _endIndex = endIndex;
            _beginSequence = beginSequence;
        }

        public long BeginSequence => _beginSequence;
        public long EndSequence => _beginSequence + _endIndex - _beginIndex;
        public int Length => (int)(1 + _endIndex - _beginIndex);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator() => new Enumerator(_array, _beginIndex, _endIndex);

        public struct Enumerator
        {
            private readonly object _array;
            private readonly int _endIndex;
            private int _index;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Enumerator(object array, int beginIndex, int endIndex)
            {
                _array = array;
                _index = beginIndex - 1;
                _endIndex = endIndex;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                return ++_index <= _endIndex;
            }

            public T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => InternalUtil.Read<T>(_array, _index);
            }
            //
            // [MethodImpl(MethodImplOptions.AggressiveInlining)]
            // public void Reset() => _index = -1;
        }
    }
}
