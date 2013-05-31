using System.Threading;

namespace Disruptor
{
    /// <summary>
    /// Cache line padded sequence counter.
    /// Can be used across threads without worrying about false sharing if a located adjacent to another counter in memory.
    /// </summary>
    public class Sequence
    {
        private Volatile.PaddedLong _value = new Volatile.PaddedLong(Sequencer.InitialCursorValue);

        /// <summary>
        /// Default Constructor that uses an initial value of <see cref="Sequencer.InitialCursorValue"/>
        /// </summary>
        public Sequence()
        {
        }

        /// <summary>
        /// Construct a new sequence counter that can be tracked across threads.
        /// </summary>
        /// <param name="initialValue">initial value for the counter</param>
        public Sequence(long initialValue)
        {
            _value.WriteCompilerOnlyFence(initialValue);
        }

        /// <summary>
        /// Current sequence number
        /// </summary>
        public virtual long Value
        {
            get { return _value.ReadFullFence(); }
            set { _value.WriteFullFence(value); }
        }

        /// <summary>
        /// Eventually sets to the given value.
        /// </summary>
        /// <param name="value">the new value</param>
        public virtual void LazySet(long value)
        {
            _value.WriteCompilerOnlyFence(value);
        }

        /// <summary>
        /// Atomically set the value to the given updated value if the current value == the expected value.
        /// </summary>
        /// <param name="expectedSequence">the expected value for the sequence</param>
        /// <param name="nextSequence">the new value for the sequence</param>
        /// <returns>true if successful. False return indicates that the actual value was not equal to the expected value.</returns>
        public virtual bool CompareAndSet(long expectedSequence, long nextSequence)
        {
            return _value.AtomicCompareExchange(nextSequence, expectedSequence);
        }

        /// <summary>
        /// Value of the <see cref="Sequence"/> as a String.
        /// </summary>
        /// <returns>String representation of the sequence.</returns>
        public override string ToString()
        {
            return _value.ToString();
        }

        ///<summary>
        /// Increments the sequence and stores the result, as an atomic operation.
        ///</summary>
        ///<returns>incremented sequence</returns>
        public long IncrementAndGet()
        {
            return AddAndGet(1);
        }

        ///<summary>
        /// Increments the sequence and stores the result, as an atomic operation.
        ///</summary>
        ///<returns>incremented sequence</returns>
        public long AddAndGet(long value)
        {
            return _value.AtomicAddAndGet(value);
        }
    }
}