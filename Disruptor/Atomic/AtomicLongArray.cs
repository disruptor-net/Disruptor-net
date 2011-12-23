using System.Threading;

namespace Disruptor.Atomic
{
    internal class AtomicLongArray
    {
        private readonly long[] _array;

        public AtomicLongArray(int length)
        {
            _array = new long[length];
        }

        public long this[int index]
        {
            get { return Thread.VolatileRead(ref _array[index]); }
            set { Thread.VolatileWrite(ref _array[index], value); }
        }

        public int Length
        {
            get { return _array.Length; }
        }
    }
}