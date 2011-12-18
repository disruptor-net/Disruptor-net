using System.Runtime.InteropServices;
using System.Threading;

namespace Disruptor.MemoryLayout
{
    /// <summary>
    /// A <see cref="long"/> wrapped in PaddedLong is guaranteed to live on its own cache line
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 2 * CacheLine.Size)]
    internal struct PaddedAtomicLong
    {
        [FieldOffset(CacheLine.Size)]
        private long _value;

        ///<summary>
        /// Initialise a new instance of CacheLineStorage
        ///</summary>
        ///<param name="value">default value</param>
        public PaddedAtomicLong(long value)
        {
            _value = value;
        }

        ///<summary>
        /// Increments a specified variable and stores the result, as an atomic operation.
        ///</summary>
        ///<returns>incremented result</returns>
        public long IncrementAndGet()
        {
            return Interlocked.Increment(ref _value);
        }

        /// <summary>
        /// Increments a specified variable and stores the result, as an atomic operation.
        /// </summary>
        /// <param name="delta"></param>
        /// <returns></returns>
        public long IncrementAndGet(int delta)
        {
            return Interlocked.Add(ref _value, delta);
        }

        /// <summary>
        /// Expose data with full fence on read and write
        /// </summary>
        public long Value
        {
            get { return Thread.VolatileRead(ref _value); }
            set { Thread.VolatileWrite(ref _value, value); }
        }

        public void LazySet(long value)
        {
            _value = value;
        }

        public bool CompareAndSet(long comparand, long value)
        {
            return Interlocked.CompareExchange(ref _value, value, comparand) == comparand;
        }
    }
}