namespace System.Threading
{
    ///<summary>
    /// A small toolkit of classes that support lock-free thread-safe programming on single variables.
    ///</summary>
    public static class Volatile
    {
        /// <summary>
        /// An integer value that may be updated atomically
        /// </summary>
        public struct Integer
        {
            private int _value;

            /// <summary>
            /// Create a new <see cref="Integer"/> with the given initial value.
            /// </summary>
            /// <param name="value">Initial value</param>
            public Integer(int value)
            {
                _value = value;
            }

            /// <summary>
            /// Read the value without applying any fence
            /// </summary>
            /// <returns>The current value</returns>
            public int ReadUnfenced()
            {
                return _value;
            }

            /// <summary>
            /// Read the value applying acquire fence semantic
            /// </summary>
            /// <returns>The current value</returns>
            public int ReadAcquireFence()
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Read the value applying full fence semantic
            /// </summary>
            /// <returns>The current value</returns>
            public int ReadFullFence()
            {
                return Thread.VolatileRead(ref _value);
            }

            /// <summary>
            /// Read the value applying a compiler only fence, no CPU fence is applied
            /// </summary>
            /// <returns>The current value</returns>
            public int ReadCompilerOnlyFence()
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Write the value applying release fence semantic
            /// </summary>
            /// <param name="newValue">The new value</param>
            public void WriteReleaseFence(int newValue)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Write the value applying full fence semantic
            /// </summary>
            /// <param name="newValue">The new value</param>
            public void WriteFullFence(int newValue)
            {
                Thread.VolatileWrite(ref _value, newValue);
            }

            /// <summary>
            /// Write the value applying a compiler fence only, no CPU fence is applied
            /// </summary>
            /// <param name="newValue">The new value</param>
            public void WriteCompilerOnlyFence(int newValue)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Write without applying any fence
            /// </summary>
            /// <param name="newValue">The new value</param>
            public void WriteUnfenced(int newValue)
            {
                _value = newValue;
            }

            /// <summary>
            /// Atomically set the value to the given updated value if the current value equals the comparand
            /// </summary>
            /// <param name="newValue">The new value</param>
            /// <param name="comparand">The comparand (expected value)</param>
            /// <returns></returns>
            public bool AtomicCompareExchange(int newValue, int comparand)
            {
                return Interlocked.CompareExchange(ref _value, newValue, comparand) == comparand;
            }

            /// <summary>
            /// Atomically set the value to the given updated value
            /// </summary>
            /// <param name="newValue">The new value</param>
            /// <returns>The original value</returns>
            public int AtomicExchange(int newValue)
            {
                return Interlocked.Exchange(ref _value, newValue);
            }

            /// <summary>
            /// Atomically add the given value to the current value and return the sum
            /// </summary>
            /// <param name="delta">The value to be added</param>
            /// <returns>The sum of the current value and the given value</returns>
            public int AtomicAddAndGet(int delta)
            {
                return Interlocked.Add(ref _value, delta);
            }

            /// <summary>
            /// Atomically increment the current value and return the new value
            /// </summary>
            /// <returns>The incremented value.</returns>
            public int AtomicIncrementAndGet()
            {
                return Interlocked.Increment(ref _value);
            }

            /// <summary>
            /// Atomically increment the current value and return the new value
            /// </summary>
            /// <returns>The decremented value.</returns>
            public int AtomicDecrementAndGet()
            {
                return Interlocked.Decrement(ref _value);
            }

            /// <summary>
            /// Returns the string representation of the current value.
            /// </summary>
            /// <returns>the string representation of the current value.</returns>
            public override string ToString()
            {
                var value = ReadFullFence();
                return value.ToString();
            }
        }

        /// <summary>
        /// A long value that may be updated atomically
        /// </summary>
        public struct Long
        {
            private long _value;

            /// <summary>
            /// Create a new <see cref="Long"/> with the given initial value.
            /// </summary>
            /// <param name="value">Initial value</param>
            public Long(long value)
            {
                _value = value;
            }

            /// <summary>
            /// Read the value without applying any fence
            /// </summary>
            /// <returns>The current value</returns>
            public long ReadUnfenced()
            {
                return _value;
            }

            /// <summary>
            /// Read the value applying acquire fence semantic
            /// </summary>
            /// <returns>The current value</returns>
            public long ReadAcquireFence()
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Read the value applying full fence semantic
            /// </summary>
            /// <returns>The current value</returns>
            public long ReadFullFence()
            {
                return Thread.VolatileRead(ref _value);
            }

            /// <summary>
            /// Read the value applying a compiler only fence, no CPU fence is applied
            /// </summary>
            /// <returns>The current value</returns>
            public long ReadCompilerOnlyFence()
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Write the value applying release fence semantic
            /// </summary>
            /// <param name="newValue">The new value</param>
            public void WriteReleaseFence(long newValue)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Write the value applying full fence semantic
            /// </summary>
            /// <param name="newValue">The new value</param>
            public void WriteFullFence(long newValue)
            {
                Thread.VolatileWrite(ref _value, newValue);
            }

            /// <summary>
            /// Write the value applying a compiler fence only, no CPU fence is applied
            /// </summary>
            /// <param name="newValue">The new value</param>
            public void WriteCompilerOnlyFence(long newValue)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Write without applying any fence
            /// </summary>
            /// <param name="newValue">The new value</param>
            public void WriteUnfenced(long newValue)
            {
                _value = newValue;
            }

            /// <summary>
            /// Atomically set the value to the given updated value if the current value equals the comparand
            /// </summary>
            /// <param name="newValue">The new value</param>
            /// <param name="comparand">The comparand (expected value)</param>
            /// <returns></returns>
            public bool AtomicCompareExchange(long newValue, long comparand)
            {
                return Interlocked.CompareExchange(ref _value, newValue, comparand) == comparand;
            }

            /// <summary>
            /// Atomically set the value to the given updated value
            /// </summary>
            /// <param name="newValue">The new value</param>
            /// <returns>The original value</returns>
            public long AtomicExchange(long newValue)
            {
                return Interlocked.Exchange(ref _value, newValue);
            }

            /// <summary>
            /// Atomically add the given value to the current value and return the sum
            /// </summary>
            /// <param name="delta">The value to be added</param>
            /// <returns>The sum of the current value and the given value</returns>
            public long AtomicAddAndGet(long delta)
            {
                return Interlocked.Add(ref _value, delta);
            }

            /// <summary>
            /// Atomically increment the current value and return the new value
            /// </summary>
            /// <returns>The incremented value.</returns>
            public long AtomicIncrementAndGet()
            {
                return Interlocked.Increment(ref _value);
            }

            /// <summary>
            /// Atomically increment the current value and return the new value
            /// </summary>
            /// <returns>The decremented value.</returns>
            public long AtomicDecrementAndGet()
            {
                return Interlocked.Decrement(ref _value);
            }

            /// <summary>
            /// Returns the string representation of the current value.
            /// </summary>
            /// <returns>the string representation of the current value.</returns>
            public override string ToString()
            {
                var value = ReadFullFence();
                return value.ToString();
            }
        }
    }
}
