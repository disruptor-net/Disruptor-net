using System.Runtime.InteropServices;
using System.Threading;

namespace Disruptor
{
    /// <summary>
    /// Cache line padded sequence counter.
    /// Can be used across threads without worrying about false sharing if a located adjacent to another counter in memory.
    /// </summary>
    public class Sequence : ISequence
    {
        /// <summary>
        /// Set to -1 as sequence starting point
        /// </summary>
        public const long InitialCursorValue = -1;

        private Fields _fields;

        /// <summary>
        /// Construct a new sequence counter that can be tracked across threads.
        /// </summary>
        /// <param name="initialValue">initial value for the counter</param>
        public Sequence(long initialValue = InitialCursorValue)
        {
            _fields = new Fields(initialValue);
        }

        /// <summary>
        /// Current sequence number
        /// </summary>
        public long Value => Volatile.Read(ref _fields.Value);

        /// <summary>
        /// Perform an ordered write of this sequence.  The intent is
        /// a Store/Store barrier between this write and any previous
        /// store.
        /// </summary>
        /// <param name="value">The new value for the sequence.</param>
        public void SetValue(long value)
        {
            // no synchronization required, the CLR memory model prevents Store/Store re-ordering
            _fields.Value = value;
        }

        /// <summary>
        /// Performs a volatile write of this sequence.  The intent is a Store/Store barrier between this write and any previous
        /// write and a Store/Load barrier between this write and any subsequent volatile read. 
        /// </summary>
        /// <param name="value"></param>
        public void SetValueVolatile(long value)
        {
            Volatile.Write(ref _fields.Value, value);
        }

        /// <summary>
        /// Atomically set the value to the given updated value if the current value == the expected value.
        /// </summary>
        /// <param name="expectedSequence">the expected value for the sequence</param>
        /// <param name="nextSequence">the new value for the sequence</param>
        /// <returns>true if successful. False return indicates that the actual value was not equal to the expected value.</returns>
        public bool CompareAndSet(long expectedSequence, long nextSequence)
        {
            return Interlocked.CompareExchange(ref _fields.Value, nextSequence, expectedSequence) == expectedSequence;
        }

        /// <summary>
        /// Value of the <see cref="Sequence"/> as a String.
        /// </summary>
        /// <returns>String representation of the sequence.</returns>
        public override string ToString()
        {
            return _fields.Value.ToString();
        }

        ///<summary>
        /// Increments the sequence and stores the result, as an atomic operation.
        ///</summary>
        ///<returns>incremented sequence</returns>
        public long IncrementAndGet()
        {
            return Interlocked.Increment(ref _fields.Value);
        }

        ///<summary>
        /// Increments the sequence and stores the result, as an atomic operation.
        ///</summary>
        ///<returns>incremented sequence</returns>
        public long AddAndGet(long value)
        {
            return Interlocked.Add(ref _fields.Value, value);
        }

        [StructLayout(LayoutKind.Explicit, Size = 120)]
        private struct Fields
        {
            [FieldOffset(0)]
            private Padding56 _beforePadding;

            /// <summary>Volatile in the Java version => always use Volatile.Read/Write or Interlocked methods to access this field.</summary>
            [FieldOffset(56)]
            public long Value;

            [FieldOffset(64)]
            private Padding56 _afterPadding;

            public Fields(long value)
            {
                _beforePadding = default(Padding56);
                Value = value;
                _afterPadding = default(Padding56);
            }
        }
    }
}