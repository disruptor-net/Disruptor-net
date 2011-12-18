using System.Runtime.InteropServices;

namespace Disruptor.MemoryLayout
{
    /// <summary>
    /// A <see cref="long"/> wrapped in PaddedLong is guaranteed to live on its own cache line
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 2 * CacheLine.Size)]
    public struct PaddedLong
    {
        [FieldOffset(CacheLine.Size)]
        private long _value;

        ///<summary>
        /// Initialise a new instance of CacheLineStorage
        ///</summary>
        ///<param name="value">default value of Value</param>
        public PaddedLong(long value)
        {
            _value = value;
        }

        /// <summary>
        /// Expose Value with full fence on read and write
        /// </summary>
        public long Value
        {
            get { return _value; }
            set { _value = value; }
        }
    }
}